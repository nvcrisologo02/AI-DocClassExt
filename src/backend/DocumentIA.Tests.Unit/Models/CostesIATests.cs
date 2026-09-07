#nullable enable
using System.Text.Json;
using DocumentIA.Core.Models;
using FluentAssertions;

namespace DocumentIA.Tests.Unit.Models;

public class CostesIATests
{
    [Fact]
    public void DetalleEjecucion_SinCostes_OmiteLaPropiedadDelJson()
    {
        var salida = new ContratoSalida();

        var json = JsonSerializer.Serialize(salida);

        json.Should().NotContain("Costes");
    }

    [Fact]
    public void DetalleEjecucion_ConCostes_SerializaElBloque()
    {
        var salida = new ContratoSalida();
        salida.DetalleEjecucion.Costes = new CostesIA
        {
            CosteTotalEur = 0.012345m,
            TokensTotales = 1500,
            PaginasTotales = 3,
            Consumos =
            {
                new ConsumoIA
                {
                    Actividad = "Clasificar",
                    Operacion = "classification.phase1",
                    Proveedor = "AzureOpenAI",
                    Modelo = "gpt-5-mini",
                    TokensEntrada = 1200,
                    TokensSalida = 300,
                    CosteEur = 0.012345m,
                    TarifaAplicada = "gpt-5-mini@2026-07-21"
                }
            }
        };

        var json = JsonSerializer.Serialize(salida);

        json.Should().Contain("\"Costes\"");
        json.Should().Contain("gpt-5-mini@2026-07-21");
        json.Should().Contain("0.012345");
    }

    [Fact]
    public void Instrucciones_PorDefecto_NoIncluyeCostes()
    {
        var entrada = new ContratoEntrada();

        entrada.Instrucciones.IncluirCostes.Should().BeFalse();
    }

    [Fact]
    public void Instrucciones_DeserializaIncluirCostes()
    {
        var json = """{"Instrucciones":{"IncluirCostes":true}}""";

        var entrada = JsonSerializer.Deserialize<ContratoEntrada>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        entrada!.Instrucciones.IncluirCostes.Should().BeTrue();
    }

    [Fact]
    public void Instrucciones_IncluirCostesNulo_SeInterpretaComoFalse()
    {
        // Mismo trato que el resto de banderas del contrato: un null explicito
        // del llamador no debe reventar la deserializacion.
        var json = """{"Instrucciones":{"IncluirCostes":null}}""";

        var entrada = JsonSerializer.Deserialize<ContratoEntrada>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        entrada!.Instrucciones.IncluirCostes.Should().BeFalse();
    }
}
