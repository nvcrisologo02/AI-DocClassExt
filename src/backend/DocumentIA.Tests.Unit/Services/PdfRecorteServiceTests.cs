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
    public void RecortarParaClasificacion_CuandoExcedeMaxPaginas_RecortaDocumento()
    {
        var sut = new PdfRecorteService(NullLogger<PdfRecorteService>.Instance);
        var base64 = BuildPdfBase64WithPages(4);

        var result = sut.RecortarParaClasificacion(base64, 2);

        result.RecorteAplicado.Should().BeTrue();
        result.TotalPaginas.Should().Be(4);
        result.PaginasIncluidas.Should().Be(2);
        result.CharsTextoNativo.Should().BeGreaterThan(0);

        var recortadoBytes = Convert.FromBase64String(result.DocumentoBase64Recortado);
        using var recortado = PdfDocument.Open(recortadoBytes);
        recortado.NumberOfPages.Should().Be(2);
    }

    [Fact]
    public void RecortarParaClasificacion_CuandoNoExcedeMaxPaginas_NoRecortaDocumento()
    {
        var sut = new PdfRecorteService(NullLogger<PdfRecorteService>.Instance);
        var base64 = BuildPdfBase64WithPages(2);

        var result = sut.RecortarParaClasificacion(base64, 3);

        result.RecorteAplicado.Should().BeFalse();
        result.TotalPaginas.Should().Be(2);
        result.PaginasIncluidas.Should().Be(2);
        result.DocumentoBase64Recortado.Should().Be(base64);
    }

    [Fact]
    public void RecortarParaClasificacion_MaxPaginasInvalido_NormalizaAUno()
    {
        var sut = new PdfRecorteService(NullLogger<PdfRecorteService>.Instance);
        var base64 = BuildPdfBase64WithPages(3);

        var result = sut.RecortarParaClasificacion(base64, 0);

        result.RecorteAplicado.Should().BeTrue();
        result.TotalPaginas.Should().Be(3);
        result.PaginasIncluidas.Should().Be(1);

        var recortadoBytes = Convert.FromBase64String(result.DocumentoBase64Recortado);
        using var recortado = PdfDocument.Open(recortadoBytes);
        recortado.NumberOfPages.Should().Be(1);
    }

    private static string BuildPdfBase64WithPages(int pages)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);

        for (var i = 1; i <= pages; i++)
        {
            var page = builder.AddPage(595, 842);
            page.AddText($"Pagina {i}", 12, new PdfPoint(36, 806), font);
        }

        var bytes = builder.Build();
        return Convert.ToBase64String(bytes);
    }
}
