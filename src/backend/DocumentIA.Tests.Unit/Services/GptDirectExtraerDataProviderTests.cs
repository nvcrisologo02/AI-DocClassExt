#nullable enable
using DocumentIA.Core.Configuration;
using DocumentIA.Core.Models;
using DocumentIA.Functions.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DocumentIA.Tests.Unit.Services;

public class GptDirectExtraerDataProviderTests
{
    private static GptDirectExtraerDataProvider Crear(out Mock<GptFallbackExtraerDataProvider> gpt)
    {
        var loader = new ExtractionModelRegistryLoader("no-existe.json");
        gpt = new Mock<GptFallbackExtraerDataProvider>(
            loader,
            Options.Create(new PromptDefaultsSettings()),
            new Mock<ILogger<GptFallbackExtraerDataProvider>>().Object,
            Mock.Of<DocumentIA.Functions.Services.Abstractions.IGptPromptBuilder>(),
            Mock.Of<DocumentIA.Functions.Services.Abstractions.IGptJsonResponseParser>(),
            Mock.Of<DocumentIA.Functions.Services.Abstractions.IOpenAiClientFactory>());

        gpt.Setup(g => g.ResolveModel(It.IsAny<string>()))
            .Returns((string k) => new ExtractionModelConfig { Key = k, Provider = "azure-openai" });
        gpt.Setup(g => g.ObtenerDatosConModeloAsync(
                It.IsAny<ExtraccionInput>(),
                It.IsAny<TipologiaValidationConfig>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExtraccionResultado { Proveedor = "azure-openai", Modelo = "dep" });

        return new GptDirectExtraerDataProvider(
            gpt.Object,
            loader,
            new Mock<ILogger<GptDirectExtraerDataProvider>>().Object);
    }

    private static ExtraccionInput Input(string? modelKeyPedido) => new()
    {
        Tipologia = "T1",
        ModelKeyEfectivo = modelKeyPedido,
        DatosNormalizados = new Dictionary<string, object> { ["Markdown"] = "texto" }
    };

    [Fact]
    public async Task ObtenerDatosAsync_ModelKeyDeTipologia_LoDevuelveComoModelKeyEfectivo()
    {
        var sut = Crear(out _);
        var config = new TipologiaValidationConfig { Extraction = { ModelKey = "gpt-4o-mini" } };

        var resultado = await sut.ObtenerDatosAsync(Input(null), config);

        resultado.ModelKeyEfectivo.Should().Be("gpt-4o-mini");
    }

    [Fact]
    public async Task ObtenerDatosAsync_ModelKeyPedido_MandaSobreElDeLaTipologia()
    {
        var sut = Crear(out _);
        var config = new TipologiaValidationConfig { Extraction = { ModelKey = "gpt-4o-mini" } };

        var resultado = await sut.ObtenerDatosAsync(Input("gpt5-mini"), config);

        resultado.ModelKeyEfectivo.Should().Be("gpt5-mini");
    }
}
