using DocumentIA.Core.Models;
using DocumentIA.Core.Services;
using DocumentIA.Functions.Activities;
using DocumentIA.Functions.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace DocumentIA.Tests.Unit.Activities;

public class PrepararDocumentoClasificacionActivityTests
{
    [Fact]
    public async Task Run_CuandoMaxPaginasEsNull_UsaDefaultTresPaginas()
    {
        var recorteService = new PdfRecorteService(NullLogger<PdfRecorteService>.Instance);
        var blobStorageService = new Mock<IBlobStorageService>().Object;
        var sut = new PrepararDocumentoClasificacionActivity(recorteService, blobStorageService, NullLogger<PrepararDocumentoClasificacionActivity>.Instance);
        var input = new PrepararDocumentoClasificacionInput
        {
            DocumentoBase64 = BuildPdfBase64WithPages(4),
            NombreDocumento = "test.pdf",
            MaxPaginasClasificacion = null
        };

        var result = await sut.Run(input);

        result.RecorteAplicado.Should().BeTrue();
        result.TotalPaginas.Should().Be(4);
        result.PaginasIncluidas.Should().Be(3);
        result.CharsTextoNativo.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Run_MapeaResultadoDelServicio()
    {
        var recorteService = new PdfRecorteService(NullLogger<PdfRecorteService>.Instance);
        var blobStorageService = new Mock<IBlobStorageService>().Object;
        var sut = new PrepararDocumentoClasificacionActivity(recorteService, blobStorageService, NullLogger<PrepararDocumentoClasificacionActivity>.Instance);
        var input = new PrepararDocumentoClasificacionInput
        {
            DocumentoBase64 = BuildPdfBase64WithPages(2),
            NombreDocumento = "test.pdf",
            MaxPaginasClasificacion = 5
        };

        var result = await sut.Run(input);

        result.RecorteAplicado.Should().BeFalse();
        result.TotalPaginas.Should().Be(2);
        result.PaginasIncluidas.Should().Be(2);
        result.DocumentoBase64Clasif.Should().Be(input.DocumentoBase64);
    }

    [Fact]
    public async Task Run_DocumentoNoPdf_NoLanzaYDevuelveDocumentoCompleto()
    {
        // AB#100045: un no-PDF (XLSX/PPTX empiezan por "PK") hacia lanzar PdfPig y el orquestador
        // se quedaba sin base64 para el Paso 2.8. El recorte debe saltarse limpiamente y devolver
        // el documento tal cual.
        var recorteService = new PdfRecorteService(NullLogger<PdfRecorteService>.Instance);
        var blobStorageService = new Mock<IBlobStorageService>().Object;
        var sut = new PrepararDocumentoClasificacionActivity(recorteService, blobStorageService, NullLogger<PrepararDocumentoClasificacionActivity>.Instance);
        var contenidoPptx = Convert.ToBase64String(new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x14, 0x00, 0x06, 0x00 });
        var input = new PrepararDocumentoClasificacionInput
        {
            DocumentoBase64 = contenidoPptx,
            NombreDocumento = "documento.pptx",
            MaxPaginasClasificacion = 3
        };

        var result = await sut.Run(input);

        result.RecorteAplicado.Should().BeFalse();
        result.TotalPaginas.Should().Be(0);
        result.DocumentoBase64Clasif.Should().Be(contenidoPptx);
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
