using DocumentIA.Core.Models;
using DocumentIA.Functions.Activities;
using DocumentIA.Functions.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace DocumentIA.Tests.Unit.Activities;

public class PrepararDocumentoClasificacionActivityTests
{
    [Fact]
    public void Run_CuandoMaxPaginasEsNull_UsaDefaultTresPaginas()
    {
        var recorteService = new PdfRecorteService(NullLogger<PdfRecorteService>.Instance);
        var sut = new PrepararDocumentoClasificacionActivity(recorteService, NullLogger<PrepararDocumentoClasificacionActivity>.Instance);
        var input = new PrepararDocumentoClasificacionInput
        {
            DocumentoBase64 = BuildPdfBase64WithPages(4),
            NombreDocumento = "test.pdf",
            MaxPaginasClasificacion = null
        };

        var result = sut.Run(input);

        result.RecorteAplicado.Should().BeTrue();
        result.TotalPaginas.Should().Be(4);
        result.PaginasIncluidas.Should().Be(3);
        result.CharsTextoNativo.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Run_MapeaResultadoDelServicio()
    {
        var recorteService = new PdfRecorteService(NullLogger<PdfRecorteService>.Instance);
        var sut = new PrepararDocumentoClasificacionActivity(recorteService, NullLogger<PrepararDocumentoClasificacionActivity>.Instance);
        var input = new PrepararDocumentoClasificacionInput
        {
            DocumentoBase64 = BuildPdfBase64WithPages(2),
            NombreDocumento = "test.pdf",
            MaxPaginasClasificacion = 5
        };

        var result = sut.Run(input);

        result.RecorteAplicado.Should().BeFalse();
        result.TotalPaginas.Should().Be(2);
        result.PaginasIncluidas.Should().Be(2);
        result.DocumentoBase64Clasif.Should().Be(input.DocumentoBase64);
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
