#nullable enable
using DocumentIA.Core.Models;
using DocumentIA.Core.Services;
using DocumentIA.Core.Configuration;
using FluentAssertions;

namespace DocumentIA.Tests.Unit.Services;

/// <summary>
/// AB#100229: los servicios de Document Intelligence facturan por pagina, no por
/// tokens. Estos tests fijan la forma del consumo que producen y como lo tarifica
/// la calculadora, que es lo que determina el importe final.
/// </summary>
public class ConsumoDocumentIntelligenceTests
{
    private static TarifaRegistry Registro() => new()
    {
        Tarifas =
        {
            new TarifaIA
            {
                Modelo = "prebuilt-layout",
                VigenteDesde = new DateTime(2026, 4, 1),
                EurPorPagina = 0.01m
            },
            new TarifaIA
            {
                Modelo = "sareb-classifier-v1",
                VigenteDesde = new DateTime(2026, 4, 1),
                EurPorPagina = 0.003m
            }
        }
    };

    [Fact]
    public void ConsumoDeLayout_SeTarificaPorPaginasYNoPorTokens()
    {
        var consumo = new ConsumoIA
        {
            Actividad = ActividadesIA.Layout,
            Operacion = "layout.prebuilt-layout",
            Proveedor = ProveedoresIA.DocumentIntelligence,
            Modelo = "prebuilt-layout",
            Paginas = 7
        };

        CalculadoraCosteIA.Aplicar(consumo, Registro(), new DateTime(2026, 8, 15));

        consumo.CosteEur.Should().Be(0.07m);
        consumo.TokensEntrada.Should().BeNull();
        consumo.TokensSalida.Should().BeNull();
    }

    [Fact]
    public void ConsumoDelClasificador_SeTarificaPorPaginas()
    {
        var consumo = new ConsumoIA
        {
            Actividad = ActividadesIA.Clasificar,
            Operacion = "classification.di",
            Proveedor = ProveedoresIA.DocumentIntelligence,
            Modelo = "sareb-classifier-v1",
            Paginas = 20
        };

        CalculadoraCosteIA.Aplicar(consumo, Registro(), new DateTime(2026, 8, 15));

        consumo.CosteEur.Should().Be(0.06m);
    }

    [Fact]
    public void ConsumoSinPaginasConocidas_NoInventaCoste()
    {
        // El extractor DI a medida no expone conteo de paginas en su respuesta:
        // se registra la llamada sin paginas y el agregado saldra a cero por ese
        // consumo, en lugar de estimar una cifra.
        var consumo = new ConsumoIA
        {
            Actividad = ActividadesIA.Extraer,
            Operacion = "extraction.di",
            Proveedor = ProveedoresIA.DocumentIntelligence,
            Modelo = "prebuilt-layout"
        };

        CalculadoraCosteIA.Aplicar(consumo, Registro(), new DateTime(2026, 8, 15));

        consumo.CosteEur.Should().Be(0m);
    }

    [Fact]
    public void VariasLlamadasAlLayout_SumanCadaUnaSusPaginas()
    {
        // El layout se invoca desde cuatro puntos del orquestador; cada llamada
        // es un consumo propio y todas cuentan.
        var consumos = new List<ConsumoIA>
        {
            new() { Modelo = "prebuilt-layout", Paginas = 5, CosteEur = 0.05m },
            new() { Modelo = "prebuilt-layout", Paginas = 5, CosteEur = 0.05m }
        };

        var agregado = CalculadoraCosteIA.Agregar(consumos);

        agregado.PaginasTotales.Should().Be(10);
        agregado.CosteTotalEur.Should().Be(0.10m);
    }
}
