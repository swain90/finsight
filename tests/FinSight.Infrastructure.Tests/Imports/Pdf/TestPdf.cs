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
}
