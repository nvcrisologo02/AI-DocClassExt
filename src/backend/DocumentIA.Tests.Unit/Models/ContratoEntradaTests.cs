using DocumentIA.Core.Models;
using FluentAssertions;
using System.Text.Json;

namespace DocumentIA.Tests.Unit.Models;

public class ContratoEntradaTests
{
    [Fact]
    public void ConfiguracionIA_Defaults_NivelClasificacionIsNull()
    {
        var config = new ConfiguracionIA();

        config.NivelClasificacion.Should().BeNull();
    }

    [Fact]
    public void Trazabilidad_DeserializaElSistemaOrigenQueEnviaColabora()
    {
        const string json = "{\"trazabilidad\":{\"sistemaOrigen\":\"Colabora\"}}";

        var entrada = JsonSerializer.Deserialize<ContratoEntrada>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        entrada.Should().NotBeNull();
        entrada!.Trazabilidad.SourceSystem.Should().Be("Colabora");
        entrada.Trazabilidad.EffectiveSourceSystem.Should().Be("Colabora");
    }

    [Fact]
    public void Trazabilidad_AceptaElAliasSourceSystemParaNuevosConsumidores()
    {
        const string json = "{\"trazabilidad\":{\"sourceSystem\":\"Colabora\"}}";

        var entrada = JsonSerializer.Deserialize<ContratoEntrada>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        entrada!.Trazabilidad.EffectiveSourceSystem.Should().Be("Colabora");
    }
}
