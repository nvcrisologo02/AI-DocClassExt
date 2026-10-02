#nullable enable
using DocumentIA.Core.Models;
using DocumentIA.Functions.Services;
using FluentAssertions;
using OpenAI.Embeddings;

namespace DocumentIA.Tests.Unit.Services;

public class UsoEmbeddingsMapperTests
{
    [Fact]
    public void Mapear_SoloTokensDeEntradaYDeploymentComoModelo()
    {
        var uso = OpenAIEmbeddingsModelFactory.EmbeddingTokenUsage(inputTokenCount: 4321, totalTokenCount: 4321);

        var consumo = UsoEmbeddingsMapper.Mapear(uso, "text-embedding-3-large-030358");

        consumo.Should().NotBeNull();
        consumo!.Actividad.Should().Be(ActividadesIA.Clasificar);
        consumo.Operacion.Should().Be(UsoEmbeddingsMapper.Operacion);
        consumo.Proveedor.Should().Be(ProveedoresIA.AzureOpenAI);
        consumo.Modelo.Should().Be("text-embedding-3-large-030358");
        consumo.TokensEntrada.Should().Be(4321);
        consumo.TokensSalida.Should().Be(0);
        consumo.TokensEntradaCache.Should().BeNull();
        consumo.TokensRazonamiento.Should().BeNull();
        consumo.Descartado.Should().BeFalse("en sombra el coste es real");
    }

    [Fact]
    public void Mapear_SinUsage_NoRegistraConsumo()
    {
        UsoEmbeddingsMapper.Mapear(null, "text-embedding-3-large-030358").Should().BeNull();
    }
}
