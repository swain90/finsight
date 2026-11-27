using FinSight.Application.Common.Exceptions;
using FinSight.Application.Common.Interfaces;
using FinSight.Domain.Enums;
using FinSight.Infrastructure.Imports.Pdf;

namespace FinSight.Infrastructure.Tests.Imports.Pdf;

/// <summary>Claims any PDF containing the marker word "FAKEPDF"; emits one row, or throws what it is told to.</summary>
public sealed class FakePdfReader(Exception? throws = null, bool canRead = true) : IPdfStatementReader
{
    public Institution Institution => Institution.Cibc;

    public bool CanRead(PdfText text) =>
        canRead && text.Lines.Any(l => l.Text.Contains("FAKEPDF"));

    public ParsedStatement Read(PdfText text)
    {
        if (throws is not null) throw throws;
        var row = new ParsedStatementRow(1, new DateOnly(2026, 10, 3), "COFFEE", null, 4.50m,
            TransactionType.Debit, "CAD", null, TransactionKind.Regular, null, "raw");
        return new ParsedStatement(Institution, [row]);
    }
}
