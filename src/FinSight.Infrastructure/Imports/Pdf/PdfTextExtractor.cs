using UglyToad.PdfPig;

namespace FinSight.Infrastructure.Imports.Pdf;

public sealed class PdfUnreadableException(Exception inner) : Exception(null, inner);

public static class PdfTextExtractor
{
    private const double BaselineTolerance = 2.0;

    public static PdfText Extract(byte[] pdf)
    {
        try
        {
            using var document = PdfDocument.Open(pdf);
            if (document.NumberOfPages == 0)
                throw new InvalidOperationException("The PDF has no pages.");

            var lines = new List<PdfLine>();
            var lineNumber = 0;
            foreach (var page in document.GetPages())
            {
                var words = page.GetWords()
                    .Where(w => !string.IsNullOrWhiteSpace(w.Text))
                    .OrderByDescending(w => w.BoundingBox.Bottom)
                    .ThenBy(w => w.BoundingBox.Left)
                    .ToList();

                var group = new List<UglyToad.PdfPig.Content.Word>();
                foreach (var word in words)
                {
                    if (group.Count > 0 &&
                        Math.Abs(group[0].BoundingBox.Bottom - word.BoundingBox.Bottom) > BaselineTolerance)
                    {
                        lines.Add(ToLine(page.Number, ++lineNumber, group));
                        group = [];
                    }
                    group.Add(word);
                }
                if (group.Count > 0)
                    lines.Add(ToLine(page.Number, ++lineNumber, group));
            }

            return new PdfText(lines);
        }
        catch (Exception ex)
        {
            throw new PdfUnreadableException(ex);
        }
    }

    private static PdfLine ToLine(int page, int lineNumber, List<UglyToad.PdfPig.Content.Word> group) =>
        new(page, lineNumber, group[0].BoundingBox.Bottom,
            group.OrderBy(w => w.BoundingBox.Left)
                .Select(w => new PdfWord(w.Text, w.BoundingBox.Left, w.BoundingBox.Right))
                .ToList());
}
