using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace FinSight.Infrastructure.Tests.Imports.Pdf;

/// <summary>Builds tiny PDFs with text at exact positions (1-based page numbers, PDF coordinates: y grows upward).</summary>
public static class TestPdf
{
    public static byte[] Build(params (int page, double x, double y, string text)[] items)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var pageCount = items.Length == 0 ? 1 : items.Max(i => i.page);
        var pages = Enumerable.Range(0, pageCount).Select(_ => builder.AddPage(PageSize.A4)).ToList();
        foreach (var (page, x, y, text) in items)
            pages[page - 1].AddText(text, 10, new PdfPoint(x, y), font);
        return builder.Build();
    }

    /// <summary>One-page PDF with horizontal lines plus 90-degree rotated margin text (PdfDocumentBuilder can't rotate).</summary>
    public static byte[] BuildWithRotatedText(string rotatedText, double rotatedX, double rotatedY, string horizontalText, double x, double y)
    {
        var content = FormattableString.Invariant(
            $"BT /F1 10 Tf 1 0 0 1 {x} {y} Tm ({horizontalText}) Tj ET\nBT /F1 10 Tf 0 1 -1 0 {rotatedX} {rotatedY} Tm ({rotatedText}) Tj ET");
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
            $"<< /Length {content.Length} >>\nstream\n{content}\nendstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
        };
        var sb = new System.Text.StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (var i = 0; i < objects.Length; i++)
        {
            offsets.Add(sb.Length);
            sb.Append(System.Globalization.CultureInfo.InvariantCulture, $"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }
        var xref = sb.Length;
        sb.Append(System.Globalization.CultureInfo.InvariantCulture, $"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (var o in offsets) sb.Append(System.Globalization.CultureInfo.InvariantCulture, $"{o:D10} 00000 n \n");
        sb.Append(System.Globalization.CultureInfo.InvariantCulture,
            $"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF");
        return System.Text.Encoding.ASCII.GetBytes(sb.ToString());
    }
}
