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
    private const string BlobOriginal = "documents/2026/10/original.pdf";
    private const string BlobRecorte = "documents-clasif/2026/10/recorte.pdf";

    [Fact]
    public async Task Run_CuandoExcedeMaxPaginas_SubeElRecorteADocumentsClasifYDevuelveSuRuta()
    {
        var blob = new Mock<IBlobStorageService>(MockBehavior.Strict);
        blob.Setup(b => b.DownloadDocumentAsync(BlobOriginal)).ReturnsAsync(BuildPdfWithPages(4));
        byte[]? subido = null;
        blob.Setup(b => b.UploadDocumentAsync(It.IsAny<byte[]>(), "test.pdf", "documents-clasif"))
            .Callback<byte[], string, string>((bytes, _, _) => subido = bytes)
            .ReturnsAsync(BlobRecorte);
        var sut = CrearSut(blob);

        var result = await sut.Run(new PrepararDocumentoClasificacionInput
        {
            NombreDocumento = "test.pdf",
            MaxPaginasClasificacion = null,
            BlobPath = BlobOriginal
        });

        result.RecorteAplicado.Should().BeTrue();
        result.TotalPaginas.Should().Be(4);
        result.PaginasIncluidas.Should().Be(3);
        result.CharsTextoNativo.Should().BeGreaterThan(0);
        result.BlobPathClasificacion.Should().Be(BlobRecorte);
        subido.Should().NotBeNull();
        subido!.Length.Should().BeGreaterThan(0);
#pragma warning disable CS0618
        result.DocumentoBase64Clasif.Should().BeNull();
#pragma warning restore CS0618
        blob.VerifyAll();
    }

    [Fact]
    public async Task Run_CuandoNoExcedeMaxPaginas_DevuelveLaRutaOriginalSinSubirNada()
    {
        var blob = new Mock<IBlobStorageService>(MockBehavior.Strict);
        blob.Setup(b => b.DownloadDocumentAsync(BlobOriginal)).ReturnsAsync(BuildPdfWithPages(2));
        var sut = CrearSut(blob);

        var result = await sut.Run(new PrepararDocumentoClasificacionInput
        {
            NombreDocumento = "test.pdf",
            MaxPaginasClasificacion = 5,
            BlobPath = BlobOriginal
        });

        result.RecorteAplicado.Should().BeFalse();
        result.TotalPaginas.Should().Be(2);
        result.PaginasIncluidas.Should().Be(2);
        result.BlobPathClasificacion.Should().Be(BlobOriginal);
        blob.Verify(b => b.UploadDocumentAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Run_DocumentoNoPdf_NoLanzaYDevuelveLaRutaOriginal()
    {
        // AB#100045: un no-PDF (XLSX/PPTX empiezan por "PK") hacia lanzar PdfPig. El recorte se
        // salta limpiamente y la clasificacion usa el documento original.
        var blob = new Mock<IBlobStorageService>(MockBehavior.Strict);
        blob.Setup(b => b.DownloadDocumentAsync("documents/2026/10/documento.pptx"))
            .ReturnsAsync(new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x14, 0x00, 0x06, 0x00 });
        var sut = CrearSut(blob);

        var result = await sut.Run(new PrepararDocumentoClasificacionInput
        {
            NombreDocumento = "documento.pptx",
            MaxPaginasClasificacion = 3,
            BlobPath = "documents/2026/10/documento.pptx"
        });

        result.RecorteAplicado.Should().BeFalse();
        result.TotalPaginas.Should().Be(0);
        result.BlobPathClasificacion.Should().Be("documents/2026/10/documento.pptx");
    }

    [Fact]
    public async Task Run_SinBlobPath_Lanza()
    {
        var sut = CrearSut(new Mock<IBlobStorageService>(MockBehavior.Strict));

        var act = () => sut.Run(new PrepararDocumentoClasificacionInput { NombreDocumento = "x.pdf" });

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*BlobPath*");
    }

    private static PrepararDocumentoClasificacionActivity CrearSut(Mock<IBlobStorageService> blob)
    {
        var recorteService = new PdfRecorteService(NullLogger<PdfRecorteService>.Instance);
        return new PrepararDocumentoClasificacionActivity(
            recorteService,
            blob.Object,
            NullLogger<PrepararDocumentoClasificacionActivity>.Instance);
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
