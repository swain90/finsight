using FinSight.Application.Common.Interfaces;
using FinSight.Domain.Enums;

namespace FinSight.Infrastructure.Imports.Pdf;

public interface IPdfStatementReader
{
    Institution Institution { get; }

    bool CanRead(PdfText text);

    /// <summary>Throws <c>StatementFormatException</c> (with a page) or <see cref="StatementTotalsMismatchException"/>.</summary>
    ParsedStatement Read(PdfText text);
}
