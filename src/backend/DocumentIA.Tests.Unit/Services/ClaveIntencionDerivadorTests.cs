// src/backend/DocumentIA.Tests.Unit/Services/ClaveIntencionDerivadorTests.cs
#nullable enable
using DocumentIA.Core.Models;
using DocumentIA.Core.Services;
using FluentAssertions;

namespace DocumentIA.Tests.Unit.Services;

/// <summary>
/// Las filas anteriores al cambio no tienen clave: se deriva del contrato persistido con la
/// misma normalizacion que la clave viva, para que una peticion igual las reutilice (AB#100930).
/// </summary>
public class ClaveIntencionDerivadorTests
{
    private const string NivelDefault = "TDN1_TDN2";

    [Fact]
    public void DesdeContrato_ConRestriccion_DeriveElUniversoOrdenado()
    {
        const string json = """
        {"Identificacion":{"Tipologia":"tasacion"},
         "DetalleEjecucion":{"ClassificationOnly":true,"NivelClasificacion":"TDN1_TDN2",
           "Clasificacion":{"Modelo":"gpt-4o-mini","RestriccionTipologias":{"Codigos":["tasacion","nota.simple"]}}},
         "Resultado":{"Estado":"OK"}}
        """;

        var clave = ClaveIntencionDerivador.DesdeContrato(json, NivelDefault);

        clave.Should().NotBeNull();
        clave!.Componentes.Universo.Should().Be("NOTA.SIMPLE,TASACION");
        clave.Componentes.Modo.Should().Be(ModosIntencion.SoloClasificacion);
        clave.Clave.Should().Be(ClaveIntencionBuilder.Calcular(ClaveIntencionBuilder.Normalizar(
            true, "TDN1_TDN2", NivelDefault, new[] { "tasacion", "nota.simple" }, null, "gpt-4o-mini")));
    }

    [Fact]
    public void DesdeContrato_SinRestriccionYNivelVacio_UsaUniversoCompletoYDefault()
    {
        const string json = """
        {"Identificacion":{"Tipologia":"tasacion"},
         "DetalleEjecucion":{"ClassificationOnly":false,"NivelClasificacion":null,"Clasificacion":{"Modelo":"gpt-4o-mini"}},
         "Resultado":{"Estado":"OK"}}
        """;

        var clave = ClaveIntencionDerivador.DesdeContrato(json, NivelDefault);

        clave!.Componentes.Universo.Should().Be("*");
        clave.Componentes.Nivel.Should().Be("TDN1_TDN2");
        clave.Componentes.Modo.Should().Be(ModosIntencion.Completo);
    }

    [Fact]
    public void DesdeContrato_ConExpectedTypeInput_TomaLaTipologiaComoExpectedType()
    {
        const string json = """
        {"Identificacion":{"Tipologia":"nota.simple"},
         "DetalleEjecucion":{"ClassificationOnly":false,"NivelClasificacion":"TDN1_TDN2","Clasificacion":{"Modelo":"expectedtype-input"}},
         "Resultado":{"Estado":"OK"}}
        """;

        var clave = ClaveIntencionDerivador.DesdeContrato(json, NivelDefault);

        clave!.Componentes.ExpectedType.Should().Be("NOTA.SIMPLE");
        clave.Componentes.ModeloClasificacion.Should().Be(ClaveIntencionBuilder.ModeloExpectedType);
    }

    [Fact]
    public void DesdeContrato_AceptaNombresEnCamelCase()
    {
        const string json = """
        {"identificacion":{"tipologia":"tasacion"},
         "detalleEjecucion":{"classificationOnly":true,"nivelClasificacion":"TDN1","clasificacion":{"modelo":"gpt-4o-mini"}},
         "resultado":{"estado":"OK"}}
        """;

        var clave = ClaveIntencionDerivador.DesdeContrato(json, NivelDefault);

        clave!.Componentes.Nivel.Should().Be("TDN1");
        clave.Componentes.ModeloClasificacion.Should().Be("gpt-4o-mini");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{no es json")]
    public void DesdeContrato_ContratoIlegible_DevuelveNull(string? json)
    {
        ClaveIntencionDerivador.DesdeContrato(json, NivelDefault).Should().BeNull();
    }

    [Fact]
    public void DesdeContrato_DuplicadoVacio_DevuelveNull()
    {
        const string json = """{"DetalleEjecucion":{"ClassificationOnly":false},"Resultado":{"Estado":"DUPLICADO"}}""";

        ClaveIntencionDerivador.DesdeContrato(json, NivelDefault).Should().BeNull();
    }
}
