using DocumentIA.Functions.Services;
using FluentAssertions;

namespace DocumentIA.Tests.Unit.Services;

public class AzureDocumentIntelligenceLayoutMarkdownProviderTests
{
    private const string Endpoint = "https://di.example.com/";
    private const string Api = "2024-11-30";

    [Fact]
    public void SinPaginas_NoAnadeElParametro()
    {
        var url = AzureDocumentIntelligenceLayoutMarkdownProvider.BuildAnalyzeUrl(Endpoint, Api, "doc.pdf", null);

        url.Should().Be("https://di.example.com/documentintelligence/documentModels/prebuilt-layout:analyze?outputContentFormat=markdown&api-version=2024-11-30");
    }

    [Theory]
    [InlineData("doc.pdf")]
    [InlineData("DOC.PDF")]
    [InlineData("escaneado.tif")]
    [InlineData("escaneado.tiff")]
    public void PdfYTiff_PidenElRango(string nombre)
    {
        var url = AzureDocumentIntelligenceLayoutMarkdownProvider.BuildAnalyzeUrl(Endpoint, Api, nombre, 3);

        url.Should().EndWith("&pages=1-3");
    }

    [Theory]
    [InlineData("informe.docx")]
    [InlineData("hoja.xlsx")]
    [InlineData("pres.pptx")]
    [InlineData("pagina.html")]
    [InlineData("foto.png")]
    [InlineData("")]
    [InlineData(null)]
    public void OtrosFormatos_IgnoranElRango(string? nombre)
    {
        // Document Intelligence documenta 'pages' solo para PDF y TIFF multipagina; en Office las
        // paginas son unidades sinteticas. Se analiza el documento entero.
        var url = AzureDocumentIntelligenceLayoutMarkdownProvider.BuildAnalyzeUrl(Endpoint, Api, nombre, 3);

        url.Should().NotContain("pages=");
    }

    [Fact]
    public void PaginasCeroONegativas_NoAnadenElParametro()
    {
        AzureDocumentIntelligenceLayoutMarkdownProvider.BuildAnalyzeUrl(Endpoint, Api, "doc.pdf", 0).Should().NotContain("pages=");
        AzureDocumentIntelligenceLayoutMarkdownProvider.BuildAnalyzeUrl(Endpoint, Api, "doc.pdf", -1).Should().NotContain("pages=");
    }

    [Fact]
    public void EndpointSinBarraFinal_ProduceLaMismaUrl()
    {
        var conBarra = AzureDocumentIntelligenceLayoutMarkdownProvider.BuildAnalyzeUrl("https://di.example.com/", Api, "d.pdf", 2);
        var sinBarra = AzureDocumentIntelligenceLayoutMarkdownProvider.BuildAnalyzeUrl("https://di.example.com", Api, "d.pdf", 2);

        sinBarra.Should().Be(conBarra);
    }
}
