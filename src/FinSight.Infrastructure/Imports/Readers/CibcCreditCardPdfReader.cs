using System.Globalization;
using System.Text.RegularExpressions;
using FinSight.Application.Common.Exceptions;
using FinSight.Application.Common.Interfaces;
using FinSight.Domain.Enums;
using FinSight.Domain.ValueObjects;
using FinSight.Infrastructure.Imports.Pdf;

namespace FinSight.Infrastructure.Imports.Readers;

/// <summary>
/// CIBC credit card (Dividend Visa) PDF statement. Rows are read from the "Your payments", "Your interest" and
/// "Your new charges and credits" sections using the column positions of each page's own header line (odd pages
/// are shifted right), and the result is refused unless it reconciles with every total the statement prints.
/// </summary>
public sealed partial class CibcCreditCardPdfReader : IPdfStatementReader
{
    private const int RawLineMax = 1000;

    /// <summary>Right-aligned amount words may start this far left of the "Amount($)" header.</summary>
    private const double AmountColumnSlack = 10;

    /// <summary>A continuation line (foreign-currency detail) sits directly under its row, closer than a row pitch.</summary>
    private const double MaxContinuationGap = 18;

    /// <summary>The cash-back marker sits up to this far left of the "Description" header, right of the post date.</summary>
    private const double MarkerSlack = 16;

    private static readonly string[] Months = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

    [GeneratedRegex(@"^-?[\d,]+\.\d{2}(\*{3})?$")]
    private static partial Regex AmountWord();

    [GeneratedRegex(@"^\d{1,2}$")]
    private static partial Regex DayWord();

    [GeneratedRegex(@"-?\$?-?[\d,]+\.\d{2}")]
    private static partial Regex MoneyValue();

    [GeneratedRegex(@"Transactions from ([A-Za-z]+) (\d{1,2})(?:\s*,\s*(\d{4}))? to ([A-Za-z]+) (\d{1,2})\s*,\s*(\d{4})")]
    private static partial Regex PeriodLine();

    [GeneratedRegex(@"Account number:?\s+\d{4}\s+X{4}\s+X{4}\s+(\d{4})")]
    private static partial Regex AccountNumberInline();

    [GeneratedRegex(@"^\d{4}\s+X{4}\s+X{4}\s+(\d{4})$")]
    private static partial Regex MaskedCard();

    [GeneratedRegex(@"(\d{4})\s*$")]
    private static partial Regex LastFour();

    private enum Section { None, Payments, Interest, Charges }

    private sealed record Columns(double Description, double? Category, double Amount)
    {
        /// <summary>Description words end before this x; category/rate words (if the section has them) start here.</summary>
        public double DescriptionEnd => Category ?? Amount - AmountColumnSlack;
    }

    private sealed class RowDraft(PdfLine anchor, Section section, string? card, DateOnly date, string description,
        string? category, decimal signedAmount, bool tripleStar)
    {
        public PdfLine Anchor { get; } = anchor;
        public PdfLine Last { get; set; } = anchor;
        public Section Section { get; } = section;
        public string? Card { get; } = card;
        public DateOnly Date { get; } = date;
        public string Description { get; } = description;
        public string? Category { get; } = category;
        public decimal SignedAmount { get; } = signedAmount;
        public bool TripleStar { get; } = tripleStar;
        public string RawLine { get; set; } = anchor.Text;
    }

    public Institution Institution => Institution.Cibc;

    public bool CanRead(PdfText text) =>
        text.Lines.Any(l => l.Text.Contains("CIBC", StringComparison.Ordinal))
        && text.Lines.Any(l => l.Text.StartsWith("Account number", StringComparison.Ordinal))
        && text.Lines.Any(l => PeriodLine().IsMatch(l.Text));

    public ParsedStatement Read(PdfText text)
    {
        var (periodStart, periodEnd) = ReadPeriod(text);
        var previousBalance = FindAmountAfter(text, "Previous balance");
        var totalBalance = FindAmountAfter(text, "Total balance");
        if (periodStart is null || periodEnd is null || previousBalance is null || totalBalance is null)
            throw new StatementFormatException(1, 1, "Statement summary not found.");

        var accountRef = ReadAccountRef(text);

        var drafts = new List<RowDraft>();
        decimal? printedPayments = null;
        decimal? printedInterest = null;
        var printedCardTotals = new Dictionary<string, decimal>();

        var section = Section.None;
        Columns? columns = null;
        string? card = null;
        RowDraft? current = null;
        var page = 0;

        foreach (var line in text.Lines)
        {
            if (line.Page != page)
            {
                // Each page prints its own column header; positions differ between odd and even pages.
                page = line.Page;
                columns = null;
                current = null;
            }

            var lineText = line.Text;

            if (StartSection(lineText) is { } started)
            {
                section = started;
                columns = null;
                current = null;
                continue;
            }

            if (TryReadHeader(line) is { } header)
            {
                columns = header;
                current = null;
                continue;
            }

            if (lineText.StartsWith("Card number ", StringComparison.Ordinal))
            {
                card = LastFourOf(lineText);
                current = null;
                continue;
            }

            if (lineText.StartsWith("Total ", StringComparison.Ordinal))
            {
                current = null;
                if (section == Section.Payments && lineText.StartsWith("Total payments", StringComparison.Ordinal))
                {
                    printedPayments = LastMoney(lineText);
                    section = Section.None;
                }
                else if (section == Section.Interest && lineText.StartsWith("Total interest this period", StringComparison.Ordinal))
                {
                    printedInterest = LastMoney(lineText);
                    section = Section.None;
                }
                else if (section == Section.Charges && lineText.StartsWith("Total for ", StringComparison.Ordinal))
                {
                    // The card may print masked ("4500 XXXX XXXX 1234") or as its last four digits.
                    var cardPart = lineText[..lineText.LastIndexOf(' ')];
                    printedCardTotals[LastFourOf(cardPart) ?? string.Empty] = LastMoney(lineText) ?? 0m;
                    card = null;
                }
                continue;
            }

            if (section == Section.None || columns is null)
                continue;

            var amountWord = FindAmountWord(line, columns);
            var dates = FindDates(line, columns, periodEnd.Value);

            if (amountWord is not null && dates.Count > 0)
            {
                current = Anchor(line, section, section == Section.Charges ? card ?? string.Empty : null,
                    columns, amountWord, dates);
                drafts.Add(current);
            }
            else if (amountWord is null && dates.Count == 0 && current is not null && IsContinuation(line, current.Last, columns))
            {
                current.RawLine = $"{current.RawLine} {lineText}";
                current.Last = line;
            }
            else
            {
                // Anything else (page furniture, stray digits) ends the row it follows.
                current = null;
            }
        }

        var payments = drafts.Where(d => d.Section == Section.Payments).Sum(d => d.SignedAmount);
        Check(payments, printedPayments ?? 0m);
        Check(drafts.Where(d => d.Section == Section.Interest && !d.TripleStar).Sum(d => d.SignedAmount), printedInterest ?? 0m);
        foreach (var group in drafts.Where(d => d.Section == Section.Charges).GroupBy(d => d.Card!))
            Check(group.Sum(d => d.SignedAmount), printedCardTotals.GetValueOrDefault(group.Key));
        var chargesAndInterest = drafts.Where(d => d.Section != Section.Payments).Sum(d => d.SignedAmount);
        Check(previousBalance.Value - payments + chargesAndInterest, totalBalance.Value);

        var rows = drafts
            .Where(d => d.SignedAmount != 0) // a zero row moves no money and would fail the positive-amount rule
            .Select(d => ToRow(d, accountRef))
            .ToList();

        return new ParsedStatement(Institution, rows,
            new StatementSummary(periodStart.Value, periodEnd.Value, previousBalance.Value, totalBalance.Value, accountRef));
    }

    private static void Check(decimal computed, decimal printed)
    {
        if (computed != printed)
            throw new StatementTotalsMismatchException(computed - printed);
    }

    private static Section? StartSection(string lineText) => lineText switch
    {
        "Your payments" or "Your payments (continued)" => Section.Payments,
        "Your interest" or "Your interest (continued)" => Section.Interest,
        "Your new charges and credits" or "Your new charges and credits (continued)" => Section.Charges,
        _ => null,
    };

    /// <summary>The column header line: "date date Description [Spend Categories | Annual interest rate] Amount($)".</summary>
    private static Columns? TryReadHeader(PdfLine line)
    {
        var description = line.Words.FirstOrDefault(w => w.Text == "Description");
        var amount = line.Words.FirstOrDefault(w => w.Text == "Amount($)");
        if (description is null || amount is null || line.Words.All(w => w.Text != "date"))
            return null;

        var category = line.Words.FirstOrDefault(w => w.Text is "Spend" or "Annual");
        return new Columns(description.Left, category?.Left, amount.Left);
    }

    private static PdfWord? FindAmountWord(PdfLine line, Columns columns) =>
        line.Words.LastOrDefault(w =>
            AmountWord().IsMatch(w.Text)
            && (w.Left >= columns.Amount - AmountColumnSlack
                // Interest reversals ("-2.07***") print left of the amount column, inside the rate column.
                || (w.Text.EndsWith("***", StringComparison.Ordinal) && w.Left >= columns.DescriptionEnd)));

    /// <summary>"Mmm dd" pairs left of the Description column (and of the cash-back marker).</summary>
    private static List<(DateOnly Date, int DayIndex)> FindDates(PdfLine line, Columns columns, DateOnly periodEnd)
    {
        var dates = new List<(DateOnly, int)>();
        var words = line.Words;
        for (var i = 0; i + 1 < words.Count && words[i].Left < columns.Description - MarkerSlack; i++)
        {
            var month = Array.IndexOf(Months, words[i].Text) + 1;
            if (month == 0 || !DayWord().IsMatch(words[i + 1].Text))
                continue;

            var day = int.Parse(words[i + 1].Text, CultureInfo.InvariantCulture);
            if (ResolveDate(month, day, periodEnd) is { } date)
                dates.Add((date, i + 1));
            i++;
        }

        return dates;
    }

    /// <summary>Row dates carry no year: use the period end's year, or the year before if that lands after the period.</summary>
    private static DateOnly? ResolveDate(int month, int day, DateOnly periodEnd)
    {
        foreach (var year in new[] { periodEnd.Year, periodEnd.Year - 1 })
        {
            if (day < 1 || day > DateTime.DaysInMonth(year, month))
                continue;
            var date = new DateOnly(year, month, day);
            if (date <= periodEnd)
                return date;
        }

        return null;
    }

    private static RowDraft Anchor(PdfLine line, Section section, string? card, Columns columns, PdfWord amountWord,
        List<(DateOnly Date, int DayIndex)> dates)
    {
        var afterDates = line.Words.Skip(dates[^1].DayIndex + 1).Where(w => !ReferenceEquals(w, amountWord)).ToList();

        var descriptionWords = afterDates.Where(w => w.Left < columns.DescriptionEnd).Select(w => w.Text).ToList();
        if (descriptionWords.Count > 0 && descriptionWords[0].Length == 1 && descriptionWords[0][0] > 127)
            descriptionWords.RemoveAt(0); // cash-back multiplier marker

        var categoryWords = columns.Category is null
            ? []
            : afterDates.Where(w => w.Left >= columns.DescriptionEnd).Select(w => w.Text).ToList();

        var amount = decimal.Parse(amountWord.Text.TrimEnd('*'),
            NumberStyles.AllowLeadingSign | NumberStyles.AllowThousands | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture);

        return new RowDraft(line, section, card, dates[0].Date, string.Join(' ', descriptionWords),
            categoryWords.Count == 0 ? null : string.Join(' ', categoryWords), amount,
            amountWord.Text.EndsWith("***", StringComparison.Ordinal));
    }

    /// <summary>A detail line (e.g. "120.91 USD @ 1.405839054**") directly beneath its row, inside the description column.</summary>
    private static bool IsContinuation(PdfLine line, PdfLine previous, Columns columns) =>
        line.Page == previous.Page
        && line.LineNumber == previous.LineNumber + 1
        && previous.Baseline - line.Baseline <= MaxContinuationGap
        && line.Words.All(w => w.Left >= columns.Description - MarkerSlack && w.Left < columns.DescriptionEnd);

    private static ParsedStatementRow ToRow(RowDraft d, string? accountRef)
    {
        var positive = d.SignedAmount > 0;
        var (type, kind, category) = d.Section switch
        {
            Section.Payments => positive
                ? (TransactionType.Credit, TransactionKind.PendingTransfer, (TransactionCategory?)TransactionCategory.DebtPayments)
                : (TransactionType.Debit, TransactionKind.Regular, null),
            Section.Interest => (positive ? TransactionType.Debit : TransactionType.Credit, TransactionKind.Regular,
                TransactionCategory.BankFees),
            _ => positive
                ? (TransactionType.Debit, TransactionKind.Regular, ChargeCategory(d.Category))
                : (TransactionType.Credit, TransactionKind.Regular, null),
        };

        var raw = d.RawLine.Length > RawLineMax ? d.RawLine[..RawLineMax] : d.RawLine;
        return new ParsedStatementRow(d.Anchor.LineNumber, d.Date, d.Description, MerchantName: null,
            Math.Abs(d.SignedAmount), type, Money.DefaultCurrency, accountRef, kind, category, raw);
    }

    private static TransactionCategory? ChargeCategory(string? spendCategory) => spendCategory?.Trim() switch
    {
        "Restaurants" => TransactionCategory.DiningOut,
        "Transportation" => TransactionCategory.Transportation,
        _ => null,
    };

    private static (DateOnly? Start, DateOnly? End) ReadPeriod(PdfText text)
    {
        foreach (var line in text.Lines)
        {
            var match = PeriodLine().Match(line.Text);
            if (!match.Success
                || MonthNumber(match.Groups[1].Value) is not { } startMonth
                || MonthNumber(match.Groups[4].Value) is not { } endMonth)
                continue;

            var endYear = int.Parse(match.Groups[6].Value, CultureInfo.InvariantCulture);
            var startYear = match.Groups[3].Success
                ? int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture)
                : startMonth > endMonth ? endYear - 1 : endYear;
            var startDay = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            var endDay = int.Parse(match.Groups[5].Value, CultureInfo.InvariantCulture);
            if (startDay < 1 || startDay > DateTime.DaysInMonth(startYear, startMonth)
                || endDay < 1 || endDay > DateTime.DaysInMonth(endYear, endMonth))
                continue;

            return (new DateOnly(startYear, startMonth, startDay), new DateOnly(endYear, endMonth, endDay));
        }

        return (null, null);
    }

    private static int? MonthNumber(string fullName)
    {
        var index = Array.IndexOf(CultureInfo.InvariantCulture.DateTimeFormat.MonthNames, fullName);
        return index is >= 0 and < 12 ? index + 1 : null;
    }

    /// <summary>The first money value on the first line that starts with <paramref name="label"/>.</summary>
    private static decimal? FindAmountAfter(PdfText text, string label)
    {
        foreach (var line in text.Lines.Where(l => l.Text.StartsWith(label, StringComparison.Ordinal)))
        {
            var match = MoneyValue().Match(line.Text, label.Length);
            if (match.Success)
                return ParseMoney(match.Value);
        }

        return null;
    }

    private static decimal? LastMoney(string lineText)
    {
        var matches = MoneyValue().Matches(lineText);
        return matches.Count == 0 ? null : ParseMoney(matches[^1].Value);
    }

    private static decimal ParseMoney(string value)
    {
        var negative = value.Contains('-', StringComparison.Ordinal);
        var digits = value.Replace("-", string.Empty, StringComparison.Ordinal).Replace("$", string.Empty, StringComparison.Ordinal);
        var amount = decimal.Parse(digits, NumberStyles.AllowThousands | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
        return negative ? -amount : amount;
    }

    private static string? LastFourOf(string lineText)
    {
        var match = LastFour().Match(lineText);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string? ReadAccountRef(PdfText text)
    {
        foreach (var line in text.Lines)
        {
            var match = AccountNumberInline().Match(line.Text);
            if (match.Success)
                return match.Groups[1].Value;
        }

        // "Account number" printed alone, with the masked number on the next line.
        for (var i = 0; i + 1 < text.Lines.Count; i++)
        {
            if (!text.Lines[i].Text.EndsWith("Account number", StringComparison.Ordinal))
                continue;
            var match = MaskedCard().Match(text.Lines[i + 1].Text);
            if (match.Success)
                return match.Groups[1].Value;
        }

        return null;
    }
}
