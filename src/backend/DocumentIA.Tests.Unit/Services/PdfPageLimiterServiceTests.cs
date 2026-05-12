using DocumentIA.Functions.Services;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using FluentAssertions;

namespace DocumentIA.Tests.Unit.Services;

public class PdfPageLimiterServiceTests
{
    [Fact]
    public void LimitForClassificationOnly_WhenMaxPagesIsZero_ShouldNotApplyLimit()
    {
        var sut = new PdfPageLimiterService();
        var base64 = Convert.ToBase64String(CreatePdfBytes(6));

        var result = sut.LimitForClassificationOnly(base64, 0);

        result.Applied.Should().BeFalse();
        result.OriginalPages.Should().Be(6);
        result.UsedPages.Should().Be(6);
    }

    [Fact]
    public void LimitForClassificationOnly_WhenMaxPagesLowerThanOriginal_ShouldReturnTrimmedPdf()
    {
        var sut = new PdfPageLimiterService();
        var base64 = Convert.ToBase64String(CreatePdfBytes(9));

        var result = sut.LimitForClassificationOnly(base64, 5);

        result.Applied.Should().BeTrue();
        result.OriginalPages.Should().Be(9);
        result.UsedPages.Should().Be(5);

        var bytes = Convert.FromBase64String(result.Base64);
        using var stream = new MemoryStream(bytes);
        using var pdf = PdfReader.Open(stream, PdfDocumentOpenMode.Import);
        pdf.PageCount.Should().Be(5);
    }

    private static byte[] CreatePdfBytes(int pages)
    {
        using var pdf = new PdfDocument();
        for (var i = 0; i < pages; i++)
        {
            pdf.AddPage();
        }

        using var ms = new MemoryStream();
        pdf.Save(ms, false);
        return ms.ToArray();
    }
}
