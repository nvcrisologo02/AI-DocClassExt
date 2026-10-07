using DocumentIA.Functions.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace DocumentIA.Tests.Unit.Services;

public class PdfRecorteServiceTests
{
    [Fact]
    public void RecortarParaClasificacion_CuandoExcedeMaxPaginas_DevuelveBytesRecortados()
    {
        var sut = new PdfRecorteService(NullLogger<PdfRecorteService>.Instance);
        var pdf = BuildPdfWithPages(4);

        var result = sut.RecortarParaClasificacion(pdf, 2);

        result.RecorteAplicado.Should().BeTrue();
        result.TotalPaginas.Should().Be(4);
        result.PaginasIncluidas.Should().Be(2);
        result.CharsTextoNativo.Should().BeGreaterThan(0);
        result.PdfRecortado.Should().NotBeNull();

        using var recortado = PdfDocument.Open(result.PdfRecortado!);
        recortado.NumberOfPages.Should().Be(2);
    }

    [Fact]
    public void RecortarParaClasificacion_CuandoNoExcedeMaxPaginas_NoDevuelveBytes()
    {
        var sut = new PdfRecorteService(NullLogger<PdfRecorteService>.Instance);
        var pdf = BuildPdfWithPages(2);

        var result = sut.RecortarParaClasificacion(pdf, 3);

        result.RecorteAplicado.Should().BeFalse();
        result.TotalPaginas.Should().Be(2);
        result.PaginasIncluidas.Should().Be(2);
        result.CharsTextoNativo.Should().BeGreaterThan(0);
        result.PdfRecortado.Should().BeNull();
    }

    [Fact]
    public void RecortarParaClasificacion_MaxPaginasInvalido_NormalizaAUno()
    {
        var sut = new PdfRecorteService(NullLogger<PdfRecorteService>.Instance);
        var pdf = BuildPdfWithPages(3);

        var result = sut.RecortarParaClasificacion(pdf, 0);

        result.RecorteAplicado.Should().BeTrue();
        result.TotalPaginas.Should().Be(3);
        result.PaginasIncluidas.Should().Be(1);

        using var recortado = PdfDocument.Open(result.PdfRecortado!);
        recortado.NumberOfPages.Should().Be(1);
    }

    [Fact]
    public void RecortarParaClasificacion_DocumentoNoPdf_NoLanzaNiRecorta()
    {
        // AB#100045: XLSX/PPTX empiezan por "PK"; el recorte por paginas solo aplica a PDF.
        var sut = new PdfRecorteService(NullLogger<PdfRecorteService>.Instance);
        var pptx = new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x14, 0x00, 0x06, 0x00 };

        var result = sut.RecortarParaClasificacion(pptx, 3);

        result.RecorteAplicado.Should().BeFalse();
        result.TotalPaginas.Should().Be(0);
        result.PaginasIncluidas.Should().Be(0);
        result.PdfRecortado.Should().BeNull();
    }

    [Fact]
    public void RecortarParaClasificacion_DocumentoVacio_NoLanzaNiRecorta()
    {
        var sut = new PdfRecorteService(NullLogger<PdfRecorteService>.Instance);

        var result = sut.RecortarParaClasificacion(Array.Empty<byte>(), 3);

        result.RecorteAplicado.Should().BeFalse();
        result.TotalPaginas.Should().Be(0);
        result.PdfRecortado.Should().BeNull();
    }

    private static byte[] BuildPdfWithPages(int pages)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);

        for (var i = 1; i <= pages; i++)
        {
            var page = builder.AddPage(595, 842);
            page.AddText($"Pagina {i}", 12, new PdfPoint(36, 806), font);
        }

        return builder.Build();
    }
}
