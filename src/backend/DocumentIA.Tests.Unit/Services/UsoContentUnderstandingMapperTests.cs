#nullable enable
using System.Linq;
using System.Text.Json;
using DocumentIA.Core.Models;
using DocumentIA.Core.Services;
using FluentAssertions;

namespace DocumentIA.Tests.Unit.Services;

public class UsoContentUnderstandingMapperTests
{
    // Forma documentada del bloque usage del servicio.
    private const string RespuestaConUso = """
    {
      "usage": {
        "documentPagesStandard": 10,
        "contextualizationTokens": 10000,
        "tokens": {
          "gpt-4.1-input": 52000,
          "gpt-4.1-output": 1800,
          "text-embedding-3-large": 3456
        }
      }
    }
    """;

    private static JsonElement Raiz(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void Mapear_SeparaElConsumoDelServicioYElDeLosModelosGenerativos()
    {
        var consumos = UsoContentUnderstandingMapper.Mapear(Raiz(RespuestaConUso), "CU_NS_1.4_2");

        // Uno del servicio (paginas + contextualizacion) y uno por modelo generativo.
        consumos.Should().HaveCount(3);

        var servicio = consumos.Single(c => c.Modelo == "CU_NS_1.4_2");
        servicio.Proveedor.Should().Be(ProveedoresIA.ContentUnderstanding);
        servicio.Actividad.Should().Be(ActividadesIA.Extraer);
        servicio.Paginas.Should().Be(10);
        servicio.TokensContextualizacion.Should().Be(10000);

        // El modelo generativo lo factura el deployment de Foundry, no el servicio.
        var generativo = consumos.Single(c => c.Modelo == "gpt-4.1");
        generativo.Proveedor.Should().Be(ProveedoresIA.AzureOpenAI);
        generativo.TokensEntrada.Should().Be(52000);
        generativo.TokensSalida.Should().Be(1800);

        var embeddings = consumos.Single(c => c.Modelo == "text-embedding-3-large");
        embeddings.TokensEntrada.Should().Be(3456);
        embeddings.TokensSalida.Should().BeNull();
    }

    [Fact]
    public void Mapear_SumaLosTresMedidoresDePaginas()
    {
        var json = """{"usage":{"documentPagesMinimal":3,"documentPagesBasic":2,"documentPagesStandard":1}}""";

        var consumos = UsoContentUnderstandingMapper.Mapear(Raiz(json), "analyzer-x");

        consumos.Should().ContainSingle();
        consumos[0].Paginas.Should().Be(6);
    }

    [Fact]
    public void Mapear_BloqueUsageAnidadoBajoResult_TambienSeLee()
    {
        var json = """{"result":{"usage":{"documentPagesStandard":4}}}""";

        var consumos = UsoContentUnderstandingMapper.Mapear(Raiz(json), "analyzer-x");

        consumos.Should().ContainSingle();
        consumos[0].Paginas.Should().Be(4);
    }

    [Fact]
    public void Mapear_SinBloqueUsage_DevuelveListaVacia()
    {
        var consumos = UsoContentUnderstandingMapper.Mapear(Raiz("""{"result":{}}"""), "analyzer-x");

        consumos.Should().BeEmpty();
    }

    [Fact]
    public void Mapear_FormaInesperadaDelBloque_NoLanza()
    {
        // Si el servicio cambia la forma del bloque preferimos no tarificar a
        // dar una cifra inventada.
        var json = """{"usage":{"tokens":"esto-no-es-un-objeto","documentPagesStandard":"cinco"}}""";

        var accion = () => UsoContentUnderstandingMapper.Mapear(Raiz(json), "analyzer-x");

        accion.Should().NotThrow();
        accion().Should().BeEmpty();
    }

    [Fact]
    public void Mapear_TokensACero_NoGeneraConsumo()
    {
        var json = """{"usage":{"documentPagesStandard":2,"tokens":{"gpt-4.1-input":0}}}""";

        var consumos = UsoContentUnderstandingMapper.Mapear(Raiz(json), "analyzer-x");

        consumos.Should().ContainSingle();
        consumos[0].Modelo.Should().Be("analyzer-x");
    }
}
