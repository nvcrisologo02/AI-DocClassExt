#nullable enable
using DocumentIA.Core.Configuration;
using DocumentIA.Core.Models;
using DocumentIA.Core.Services;
using FluentAssertions;

namespace DocumentIA.Tests.Unit.Services;

public class CalculadoraCosteIATests
{
    private static TarifaRegistry Registro() => new()
    {
        Tarifas =
        {
            new TarifaIA
            {
                Modelo = "gpt-5-mini",
                VigenteDesde = new DateTime(2026, 7, 21),
                EurEntradaPor1M = 1.00m,
                EurEntradaCachePor1M = 0.10m,
                EurSalidaPor1M = 8.00m
            },
            new TarifaIA
            {
                Modelo = "gpt-5-mini",
                VigenteDesde = new DateTime(2026, 9, 1),
                EurEntradaPor1M = 2.00m,
                EurEntradaCachePor1M = 0.20m,
                EurSalidaPor1M = 16.00m
            },
            new TarifaIA
            {
                Modelo = "prebuilt-layout",
                VigenteDesde = new DateTime(2026, 4, 1),
                EurPorPagina = 0.01m
            },
            new TarifaIA
            {
                Modelo = "CU_NS_1.4_2",
                VigenteDesde = new DateTime(2026, 4, 1),
                EurPorPagina = 0.005m,
                EurContextualizacionPor1M = 1.00m
            }
        }
    };

    [Fact]
    public void Aplicar_TokensCacheados_NoLosCuentaDosVeces()
    {
        // 1.000.000 de entrada de los que 600.000 vienen de cache.
        // Correcto: 400.000 a 1,00 EUR/M + 600.000 a 0,10 EUR/M = 0,40 + 0,06 = 0,46.
        // Si se sumaran entrada y cache saldria 1,06: mas del doble.
        var consumo = new ConsumoIA
        {
            Modelo = "gpt-5-mini",
            TokensEntrada = 1_000_000,
            TokensEntradaCache = 600_000,
            TokensSalida = 0
        };

        CalculadoraCosteIA.Aplicar(consumo, Registro(), new DateTime(2026, 8, 15));

        consumo.CosteEur.Should().Be(0.46m);
    }

    [Fact]
    public void Aplicar_CacheMayorQueLaEntrada_NoProduceCosteNegativo()
    {
        // Defensa ante datos incoherentes del proveedor: la parte a precio pleno
        // nunca puede ser negativa.
        var consumo = new ConsumoIA
        {
            Modelo = "gpt-5-mini",
            TokensEntrada = 100,
            TokensEntradaCache = 500
        };

        CalculadoraCosteIA.Aplicar(consumo, Registro(), new DateTime(2026, 8, 15));

        consumo.CosteEur.Should().BeGreaterThanOrEqualTo(0m);
    }

    [Fact]
    public void Aplicar_SinCache_TarificaTodaLaEntradaAPrecioPleno()
    {
        var consumo = new ConsumoIA
        {
            Modelo = "gpt-5-mini",
            TokensEntrada = 1_000_000,
            TokensSalida = 1_000_000
        };

        CalculadoraCosteIA.Aplicar(consumo, Registro(), new DateTime(2026, 8, 15));

        consumo.CosteEur.Should().Be(9.00m);
    }

    [Fact]
    public void Aplicar_EligeLaLineaVigenteEnLaFechaDeLaEjecucion()
    {
        var consumo = new ConsumoIA { Modelo = "gpt-5-mini", TokensSalida = 1_000_000 };

        CalculadoraCosteIA.Aplicar(consumo, Registro(), new DateTime(2026, 9, 5));

        consumo.CosteEur.Should().Be(16.00m);
        consumo.TarifaAplicada.Should().Be("gpt-5-mini@2026-09-01");
    }

    [Fact]
    public void Aplicar_FechaAnteriorATodaVigencia_NoTarifica()
    {
        var consumo = new ConsumoIA { Modelo = "gpt-5-mini", TokensSalida = 1_000_000 };

        CalculadoraCosteIA.Aplicar(consumo, Registro(), new DateTime(2026, 1, 1));

        consumo.CosteEur.Should().BeNull();
        consumo.TarifaAplicada.Should().BeNull();
    }

    [Fact]
    public void Aplicar_ModeloAusente_DejaCosteNuloSinLanzar()
    {
        var consumo = new ConsumoIA { Modelo = "modelo-inexistente", TokensSalida = 1000 };

        CalculadoraCosteIA.Aplicar(consumo, Registro(), new DateTime(2026, 8, 15));

        consumo.CosteEur.Should().BeNull();
    }

    [Fact]
    public void Aplicar_ModeloConMayusculasYEspacios_ResuelveIgual()
    {
        var consumo = new ConsumoIA { Modelo = "  GPT-5-Mini ", TokensSalida = 1_000_000 };

        CalculadoraCosteIA.Aplicar(consumo, Registro(), new DateTime(2026, 8, 15));

        consumo.CosteEur.Should().Be(8.00m);
    }

    [Fact]
    public void Aplicar_CostePorPagina()
    {
        var consumo = new ConsumoIA { Modelo = "prebuilt-layout", Paginas = 12 };

        CalculadoraCosteIA.Aplicar(consumo, Registro(), new DateTime(2026, 8, 15));

        consumo.CosteEur.Should().Be(0.12m);
    }

    [Fact]
    public void Aplicar_PaginasYContextualizacionSeSuman()
    {
        var consumo = new ConsumoIA
        {
            Modelo = "CU_NS_1.4_2",
            Paginas = 10,
            TokensContextualizacion = 10_000
        };

        CalculadoraCosteIA.Aplicar(consumo, Registro(), new DateTime(2026, 8, 15));

        consumo.CosteEur.Should().Be(0.06m);
    }

    [Fact]
    public void Aplicar_ModeloConTarifaPeroSinConsumo_DejaCosteACero()
    {
        // Una llamada registrada sin cifras (el proveedor no devolvio uso) no
        // inventa coste, pero si deja constancia de la tarifa que le tocaba.
        var consumo = new ConsumoIA { Modelo = "gpt-5-mini" };

        CalculadoraCosteIA.Aplicar(consumo, Registro(), new DateTime(2026, 8, 15));

        consumo.CosteEur.Should().Be(0m);
        consumo.TarifaAplicada.Should().Be("gpt-5-mini@2026-07-21");
    }

    [Fact]
    public void Agregar_SumaCostesTokensYPaginas()
    {
        var consumos = new List<ConsumoIA>
        {
            new() { Modelo = "a", CosteEur = 0.10m, TokensEntrada = 100, TokensEntradaCache = 60, TokensSalida = 20 },
            new() { Modelo = "b", CosteEur = 0.05m, Paginas = 3, TokensContextualizacion = 500 }
        };

        var agregado = CalculadoraCosteIA.Agregar(consumos);

        agregado.CosteTotalEur.Should().Be(0.15m);
        agregado.TokensTotales.Should().Be(620);
        agregado.PaginasTotales.Should().Be(3);
        agregado.TarifasCompletas.Should().BeTrue();
        agregado.Consumos.Should().HaveCount(2);
    }

    [Fact]
    public void Agregar_ConsumoSinTarifa_MarcaElTotalIncompleto()
    {
        var consumos = new List<ConsumoIA>
        {
            new() { Modelo = "con-tarifa", CosteEur = 0.10m },
            new() { Modelo = "sin-tarifa", CosteEur = null, TokensSalida = 999 }
        };

        var agregado = CalculadoraCosteIA.Agregar(consumos);

        agregado.CosteTotalEur.Should().Be(0.10m);
        agregado.TarifasCompletas.Should().BeFalse();
        agregado.ModelosSinTarifa.Should().ContainSingle().Which.Should().Be("sin-tarifa");
    }

    [Fact]
    public void Agregar_ConsumoDescartado_CuentaIgualEnElTotal()
    {
        // La clasificacion evalua varios proveedores y se queda con uno, pero
        // todos se han pagado.
        var consumos = new List<ConsumoIA>
        {
            new() { Modelo = "a", CosteEur = 0.10m, Descartado = true },
            new() { Modelo = "b", CosteEur = 0.05m }
        };

        var agregado = CalculadoraCosteIA.Agregar(consumos);

        agregado.CosteTotalEur.Should().Be(0.15m);
    }

    [Fact]
    public void Agregar_SinConsumos_DevuelveAgregadoACero()
    {
        var agregado = CalculadoraCosteIA.Agregar(new List<ConsumoIA>());

        agregado.CosteTotalEur.Should().Be(0m);
        agregado.TokensTotales.Should().Be(0);
        agregado.PaginasTotales.Should().Be(0);
        agregado.TarifasCompletas.Should().BeTrue();
        agregado.ModelosSinTarifa.Should().BeEmpty();
    }
}
