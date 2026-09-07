#nullable enable
using System.Linq;
using System.Text.Json;
using DocumentIA.Core.Configuration;
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
    public void Mapear_SeparaPaginasContextualizacionYModelosGenerativos()
    {
        var consumos = UsoContentUnderstandingMapper.Mapear(Raiz(RespuestaConUso), "CU_NS_1.5_0");

        // Paginas standard, contextualizacion, gpt-4.1 y embeddings.
        consumos.Should().HaveCount(4);

        var paginas = consumos.Single(c => c.Modelo == "cu.documentPagesStandard");
        paginas.Proveedor.Should().Be(ProveedoresIA.ContentUnderstanding);
        paginas.Actividad.Should().Be(ActividadesIA.Extraer);
        paginas.Paginas.Should().Be(10);

        // La contextualizacion si va contra el analizador: su precio depende del
        // workflow que resuelva.
        var contexto = consumos.Single(c => c.Modelo == "CU_NS_1.5_0");
        contexto.TokensContextualizacion.Should().Be(10000);
        contexto.Paginas.Should().BeNull();

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
    public void Mapear_CadaMedidorDePaginaEsUnConsumoPropio()
    {
        // El medidor standard cuesta unas 500 veces mas que el minimal. Sumarlos y
        // aplicar un unico precio sobrevaloraria en ese factor los documentos de
        // Office, que van siempre por minimal.
        var json = """{"usage":{"documentPagesMinimal":3,"documentPagesBasic":2,"documentPagesStandard":1}}""";

        var consumos = UsoContentUnderstandingMapper.Mapear(Raiz(json), "analyzer-x");

        consumos.Should().HaveCount(3);
        consumos.Single(c => c.Modelo == "cu.documentPagesMinimal").Paginas.Should().Be(3);
        consumos.Single(c => c.Modelo == "cu.documentPagesBasic").Paginas.Should().Be(2);
        consumos.Single(c => c.Modelo == "cu.documentPagesStandard").Paginas.Should().Be(1);
    }

    [Fact]
    public void Mapear_DocumentoDigital_SeTarificaAlPrecioMinimalYNoAlDeLayout()
    {
        // Un DOCX de 50 paginas va por el medidor minimal. Con precios reales de
        // westeurope: 50 x 0,0000086 = 0,00043 EUR, no 50 x 0,0042933 = 0,21 EUR.
        var json = """{"usage":{"documentPagesMinimal":50}}""";
        var registro = new TarifaRegistry
        {
            Tarifas =
            {
                new TarifaIA
                {
                    Modelo = "cu.documentPagesMinimal",
                    VigenteDesde = new DateTime(2026, 4, 1),
                    EurPorPagina = 0.0000086m
                },
                new TarifaIA
                {
                    Modelo = "cu.documentPagesStandard",
                    VigenteDesde = new DateTime(2026, 4, 1),
                    EurPorPagina = 0.0042933m
                }
            }
        };

        var consumos = UsoContentUnderstandingMapper.Mapear(Raiz(json), "analyzer-x");
        foreach (var consumo in consumos)
        {
            CalculadoraCosteIA.Aplicar(consumo, registro, new DateTime(2026, 8, 15));
        }

        CalculadoraCosteIA.Agregar(consumos).CosteTotalEur.Should().Be(0.00043m);
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
        consumos[0].Modelo.Should().Be("cu.documentPagesStandard");
    }
}
