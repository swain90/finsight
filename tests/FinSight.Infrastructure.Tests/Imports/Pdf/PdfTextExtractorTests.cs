using FinSight.Infrastructure.Imports.Pdf;
using FluentAssertions;

namespace FinSight.Infrastructure.Tests.Imports.Pdf;

public class PdfTextExtractorTests
{
    [Fact]
    public void Extract_GroupsWordsOnSameBaselineIntoOneLine()
    {
        var pdf = TestPdf.Build((1, 200, 700, "Beta"), (1, 50, 700, "Alpha"), (1, 50, 600, "Below"));

        var text = PdfTextExtractor.Extract(pdf);

        text.Lines.Should().HaveCount(2);
        text.Lines[0].Text.Should().Be("Alpha Beta");
        text.Lines[0].Words.Select(w => w.Text).Should().Equal("Alpha", "Beta");
        text.Lines[0].Words[0].Left.Should().BeLessThan(text.Lines[0].Words[1].Left);
        text.Lines[1].Text.Should().Be("Below");
        text.Lines[0].Baseline.Should().BeGreaterThan(text.Lines[1].Baseline);
    }

    [Fact]
    public void Extract_DescendersDoNotSplitLine()
    {
        var pdf = TestPdf.Build((1, 50, 700, "gypsy"), (1, 150, 700, "ABC"), (1, 250, 700, "jqp 12.50"));

        PdfTextExtractor.Extract(pdf).Lines.Should().ContainSingle()
            .Which.Text.Should().Be("gypsy ABC jqp 12.50");
    }

    [Fact]
    public void Extract_SplitsLinesAndNumbersAcrossPages()
    {
        var pdf = TestPdf.Build((1, 50, 700, "One"), (1, 50, 650, "Two"), (2, 50, 700, "Three"));

        var text = PdfTextExtractor.Extract(pdf);

        text.Lines.Select(l => (l.Page, l.LineNumber, l.Text)).Should().Equal(
            (1, 1, "One"), (1, 2, "Two"), (2, 3, "Three"));
    }

    [Fact]
    public void Extract_Garbage_ThrowsPdfUnreadable()
    {
        var act = () => PdfTextExtractor.Extract("%PDF-1.4 junk junk"u8.ToArray());

        act.Should().Throw<PdfUnreadableException>();
    }
}
