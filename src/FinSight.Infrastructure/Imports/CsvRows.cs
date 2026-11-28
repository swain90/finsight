namespace FinSight.Infrastructure.Imports;

/// <param name="LineNumber">1-based physical line number in the file (header is line 1).</param>
public sealed record CsvRow(int LineNumber, IReadOnlyList<string> Fields, string RawLine);
