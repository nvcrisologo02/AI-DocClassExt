#nullable enable
using DocumentIA.Data.Entities;
using FluentAssertions;

namespace DocumentIA.Tests.Unit.Configuration;

/// <summary>
/// AB#100233: el valor numerico de TipoModelo se persiste en la columna Tipo de
/// ModeloConfigs. Reordenar el enum reasignaria en silencio las filas ya grabadas,
/// asi que los valores quedan fijados por test.
/// </summary>
public class TipoModeloTarifasTests
{
    [Fact]
    public void TipoModelo_MantieneLosValoresNumericosPersistidos()
    {
        ((int)TipoModelo.Clasificacion).Should().Be(0);
        ((int)TipoModelo.Extraccion).Should().Be(1);
        ((int)TipoModelo.Prompt).Should().Be(2);
        ((int)TipoModelo.Layout).Should().Be(3);
        ((int)TipoModelo.Tarifas).Should().Be(4);
    }

    [Fact]
    public void TipoModelo_TarifasEsUnTipoPropioYNoUnModeloInvocable()
    {
        // El catalogo de tarifas comparte tabla con los modelos, pero no tiene
        // endpoint ni deployment: no debe confundirse con los cuatro tipos que si
        // se invocan.
        TipoModelo.Tarifas.Should().NotBe(TipoModelo.Clasificacion);
        TipoModelo.Tarifas.Should().NotBe(TipoModelo.Extraccion);
        TipoModelo.Tarifas.Should().NotBe(TipoModelo.Prompt);
        TipoModelo.Tarifas.Should().NotBe(TipoModelo.Layout);
    }
}
