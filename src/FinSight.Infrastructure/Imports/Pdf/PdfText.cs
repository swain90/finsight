namespace FinSight.Infrastructure.Imports.Pdf;

public sealed record PdfWord(string Text, double Left, double Right);

/// <summary>One visual line of a PDF page. <paramref name="Baseline"/> is the PDF y coordinate (larger = higher on the page).</summary>
public sealed record PdfLine(int Page, int LineNumber, double Baseline, IReadOnlyList<PdfWord> Words)
{
    public string Text => string.Join(' ', Words.Select(w => w.Text));
}

public sealed record PdfText(IReadOnlyList<PdfLine> Lines);
