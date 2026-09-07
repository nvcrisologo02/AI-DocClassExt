#nullable enable
using DocumentIA.Core.Configuration;
using FluentAssertions;

namespace DocumentIA.Tests.Unit.Configuration;

public class TarifaRegistryLoaderTests
{
    private const string JsonValido = """
    {
      "Moneda": "EUR",
      "Tarifas": [
        { "Modelo": "gpt-5-mini", "VigenteDesde": "2026-07-21",
          "EurEntradaPor1M": 0.23, "EurEntradaCachePor1M": 0.02, "EurSalidaPor1M": 1.84 },
        { "Modelo": "prebuilt-layout", "VigenteDesde": "2026-04-01", "EurPorPagina": 0.0092 }
      ]
    }
    """;

    [Fact]
    public void Parse_JsonValido_DevuelveLasTarifas()
    {
        var registry = TarifaRegistryLoader.Parse(JsonValido);

        registry.Moneda.Should().Be("EUR");
        registry.Tarifas.Should().HaveCount(2);
        registry.Tarifas[0].Modelo.Should().Be("gpt-5-mini");
        registry.Tarifas[0].VigenteDesde.Should().Be(new DateTime(2026, 7, 21));
        registry.Tarifas[0].EurSalidaPor1M.Should().Be(1.84m);
        registry.Tarifas[1].EurPorPagina.Should().Be(0.0092m);
    }

    [Fact]
    public void Parse_JsonInvalido_DevuelveRegistroVacioSinLanzar()
    {
        var registry = TarifaRegistryLoader.Parse("{ esto no es json ");

        registry.Tarifas.Should().BeEmpty();
    }

    [Fact]
    public void Parse_JsonNuloOVacio_DevuelveRegistroVacio()
    {
        TarifaRegistryLoader.Parse(null).Tarifas.Should().BeEmpty();
        TarifaRegistryLoader.Parse("   ").Tarifas.Should().BeEmpty();
    }

    [Fact]
    public void Parse_ToleraNombresDePropiedadEnMinusculas()
    {
        var registry = TarifaRegistryLoader.Parse(
            """{"moneda":"EUR","tarifas":[{"modelo":"gpt-4o-mini","vigenteDesde":"2026-04-01","eurPorPagina":0.5}]}""");

        registry.Tarifas.Should().ContainSingle();
        registry.Tarifas[0].Modelo.Should().Be("gpt-4o-mini");
        registry.Tarifas[0].EurPorPagina.Should().Be(0.5m);
    }

    [Fact]
    public void Parse_TarifaSinPreciosInformados_LosDejaANulo()
    {
        // Un modelo declarado sin precio no es un error de formato: la calculadora
        // simplemente no le sumara nada y el agregado saldra incompleto.
        var registry = TarifaRegistryLoader.Parse(
            """{"Tarifas":[{"Modelo":"x","VigenteDesde":"2026-04-01"}]}""");

        registry.Tarifas.Should().ContainSingle();
        registry.Tarifas[0].EurEntradaPor1M.Should().BeNull();
        registry.Tarifas[0].EurPorPagina.Should().BeNull();
    }
}
