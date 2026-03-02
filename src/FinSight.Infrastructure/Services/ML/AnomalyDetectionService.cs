using FinSight.Application.Common.Interfaces;
using FinSight.Domain.Entities;
using FinSight.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.ML;
using Microsoft.ML.TimeSeries;

namespace FinSight.Infrastructure.Services.ML;

/// <summary>
/// Uses ML.NET's SR-CNN time-series algorithm to detect spending anomalies.
///
/// Strategy:
///   1. Group transactions by day into a time-series of daily spending totals
///   2. Run Spike Detection (IID) for sudden one-day bursts
///   3. Also flag individual transactions that are 3+ standard deviations from
///      the user's mean transaction size for that category
///
/// Both signals are combined — a transaction is an anomaly if either fires.
/// </summary>
public sealed class AnomalyDetectionService(ILogger<AnomalyDetectionService> logger) : IAnomalyDetectionService
{
    // SR-CNN / IID configuration
    private const int PValueHistoryLength = 30;  // days of context
    private const double Confidence = 0.95;
    private const int SeasonalityWindowSize = 7; // weekly seasonality

    // Z-score threshold: flag if > 3 std devs from category mean
    private const double ZScoreThreshold = 3.0;

    public Task<IReadOnlyList<Guid>> DetectAnomaliesAsync(
        IReadOnlyList<Transaction> transactions,
        CancellationToken cancellationToken = default)
    {
        if (transactions.Count < 5)
        {
            // Not enough data for meaningful anomaly detection
            return Task.FromResult<IReadOnlyList<Guid>>(Array.Empty<Guid>());
        }

        var anomalousIds = new HashSet<Guid>();

        try
        {
            // Signal 1: Time-series spike detection on daily totals
            var timeSeriesAnomalyDates = DetectTimeSeriesSpikes(transactions);
            foreach (var date in timeSeriesAnomalyDates)
            {
                // Flag all debit transactions on anomalous dates
                var flagged = transactions
                    .Where(t => t.Type == TransactionType.Debit
                             && DateOnly.FromDateTime(t.OccurredAt.Date) == date)
                    .Select(t => t.Id);
                foreach (var id in flagged) anomalousIds.Add(id);
            }

            // Signal 2: Z-score per category
            var zScoreAnomalies = DetectZScoreAnomalies(transactions);
            foreach (var id in zScoreAnomalies) anomalousIds.Add(id);

            logger.LogInformation(
                "Anomaly detection complete. {TotalTransactions} transactions analysed, {AnomalyCount} flagged.",
                transactions.Count, anomalousIds.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Anomaly detection failed — returning empty result.");
        }

        return Task.FromResult<IReadOnlyList<Guid>>(anomalousIds.ToList());
    }

    // ─── Time-series Spike Detection ────────────────────────────────────────

    private static IReadOnlyList<DateOnly> DetectTimeSeriesSpikes(IReadOnlyList<Transaction> transactions)
    {
        // Aggregate to daily debit totals, filling gaps with 0
        var debits = transactions
            .Where(t => t.Type == TransactionType.Debit)
            .GroupBy(t => t.OccurredAt.Date)
            .OrderBy(g => g.Key)
            .Select(g => new DailySpending { Date = g.Key, TotalAmount = (float)g.Sum(t => t.Amount.Amount) })
            .ToList();

        if (debits.Count < PValueHistoryLength) return Array.Empty<DateOnly>();

        var mlContext = new MLContext(seed: 42);
        var data = mlContext.Data.LoadFromEnumerable(debits);

        var pipeline = mlContext.Transforms.DetectIidSpike(
            outputColumnName: nameof(SpikePrediction.Prediction),
            inputColumnName: nameof(DailySpending.TotalAmount),
            confidence: Confidence,
            pvalueHistoryLength: Math.Min(PValueHistoryLength, debits.Count / 2));

        var model = pipeline.Fit(data);
        var predictions = model.Transform(data);

        var results = mlContext.Data
            .CreateEnumerable<SpikePrediction>(predictions, reuseRowObject: false)
            .ToList();

        var anomalousDates = new List<DateOnly>();
        for (int i = 0; i < results.Count && i < debits.Count; i++)
        {
            // Prediction[0] = alert (1 = spike), [1] = score, [2] = p-value
            if (results[i].Prediction?[0] == 1)
                anomalousDates.Add(DateOnly.FromDateTime(debits[i].Date));
        }

        return anomalousDates;
    }

    // ─── Z-score per Category ───────────────────────────────────────────────

    private static IReadOnlyList<Guid> DetectZScoreAnomalies(IReadOnlyList<Transaction> transactions)
    {
        var anomalous = new List<Guid>();

        var debitsByCategory = transactions
            .Where(t => t.Type == TransactionType.Debit)
            .GroupBy(t => t.Category);

        foreach (var group in debitsByCategory)
        {
            var amounts = group.Select(t => (double)t.Amount.Amount).ToList();
            if (amounts.Count < 4) continue;  // need enough data for meaningful stats

            var mean = amounts.Average();
            var variance = amounts.Sum(a => Math.Pow(a - mean, 2)) / amounts.Count;
            var stdDev = Math.Sqrt(variance);

            if (stdDev < 0.01) continue;  // all identical amounts — skip

            foreach (var tx in group)
            {
                var zScore = Math.Abs((double)tx.Amount.Amount - mean) / stdDev;
                if (zScore >= ZScoreThreshold)
                    anomalous.Add(tx.Id);
            }
        }

        return anomalous;
    }

    // ─── ML.NET data classes ─────────────────────────────────────────────────

    private sealed class DailySpending
    {
        public DateTime Date { get; set; }
        public float TotalAmount { get; set; }
    }

    private sealed class SpikePrediction
    {
        [Microsoft.ML.Data.VectorType(3)]
        public double[]? Prediction { get; set; }
    }
}
