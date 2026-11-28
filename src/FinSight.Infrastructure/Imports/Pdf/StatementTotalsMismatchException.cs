namespace FinSight.Infrastructure.Imports.Pdf;

/// <summary>Thrown when parsed transactions don't reconcile with the statement's own totals.</summary>
public sealed class StatementTotalsMismatchException(decimal difference) : Exception
{
    public decimal Difference { get; } = difference;
}
