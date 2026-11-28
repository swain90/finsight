using FinSight.Application.Common.Interfaces;
using FinSight.Domain.Enums;

namespace FinSight.Infrastructure.Imports;

public interface IStatementReader
{
    Institution Institution { get; }

    /// <summary>Whether this reader recognises the file, given up to 5 non-blank rows.</summary>
    bool CanRead(IReadOnlyList<CsvRow> firstRows);

    IEnumerable<ParsedStatementRow> Read(IReadOnlyList<CsvRow> allRows);
}
