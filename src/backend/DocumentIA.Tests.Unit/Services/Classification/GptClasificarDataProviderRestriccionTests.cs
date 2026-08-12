#nullable enable
using DocumentIA.Core.Models;
using DocumentIA.Functions.Services;
using FluentAssertions;
using Xunit;

namespace DocumentIA.Tests.Unit.Services.Classification;

public class GptClasificarDataProviderRestriccionTests
{
    private static ClasificacionInput CrearInput(RestriccionTipologias? restriccion, bool omitir = false)
    {
        return new ClasificacionInput
        {
            OmitirRestriccionTipologias = omitir,
            Entrada = new ContratoEntrada
            {
                Instrucciones = new Instrucciones { RestriccionTipologias = restriccion }
            }
        };
    }

    [Fact]
    public void ResolverRestriccion_SinRestriccion_DevuelveNull()
    {
        GptClasificarDataProvider.ResolverRestriccion(CrearInput(null)).Should().BeNull();
    }

    [Fact]
    public void ResolverRestriccion_ConCodigos_DevuelveElConjunto()
    {
        var restriccion = new RestriccionTipologias { Codigos = new List<string> { "SERE-25", "nota-simple" } };

        var conjunto = GptClasificarDataProvider.ResolverRestriccion(CrearInput(restriccion));

        conjunto.Should().BeEquivalentTo("SERE-25", "nota-simple");
    }

    [Fact]
    public void ResolverRestriccion_ConOmitir_DevuelveNull()
    {
        var restriccion = new RestriccionTipologias { Codigos = new List<string> { "SERE-25" } };

        GptClasificarDataProvider.ResolverRestriccion(CrearInput(restriccion, omitir: true)).Should().BeNull();
    }

    [Fact]
    public void ResolverRestriccion_ListaVacia_DevuelveNull()
    {
        var restriccion = new RestriccionTipologias { Codigos = new List<string>() };

        GptClasificarDataProvider.ResolverRestriccion(CrearInput(restriccion)).Should().BeNull();
    }

    [Fact]
    public void RestriccionFasePlanaInstruction_MencionaNullYCompatible()
    {
        GptClasificarDataProvider.RestriccionFasePlanaInstruction.Should().Contain("null");
        GptClasificarDataProvider.RestriccionFasePlanaInstruction.Should().Contain("compatible");
    }

    [Fact]
    public void RestriccionFasePlanaResponseInstruction_MencionaTipologia()
    {
        GptClasificarDataProvider.RestriccionFasePlanaResponseInstruction.Should().Contain("tipologia");
    }

    [Fact]
    public void RestriccionFasePlanaResponseInstructionConResumen_MencionaTipologiaYResumen()
    {
        GptClasificarDataProvider.RestriccionFasePlanaResponseInstructionConResumen.Should().Contain("tipologia");
        GptClasificarDataProvider.RestriccionFasePlanaResponseInstructionConResumen.Should().Contain("resumen");
    }

    // ========== Plantillas dedicadas de la fase única restringida (AB#100061) ==========
    // No deben reutilizar las plantillas de Fase 1 de BD: aquellas incrustan el formato de
    // respuesta jerárquico ("tdn1"/familias), que contradice el formato plano restringido.

    [Fact]
    public void RestriccionFasePlanaSystemPrompt_NoContieneLenguajeJerarquicoYMencionaTipologia()
    {
        GptClasificarDataProvider.RestriccionFasePlanaSystemPrompt.Should().NotContainEquivalentOf("tdn1");
        GptClasificarDataProvider.RestriccionFasePlanaSystemPrompt.Should().NotContainEquivalentOf("familia");
        GptClasificarDataProvider.RestriccionFasePlanaSystemPrompt.Should().Contain("tipolog");
    }

    [Fact]
    public void RestriccionFasePlanaUserPromptTemplate_UsaPlaceholdersPropiosYNoElCatalogoTdn1()
    {
        GptClasificarDataProvider.RestriccionFasePlanaUserPromptTemplate.Should().Contain("{CATALOGO}");
        GptClasificarDataProvider.RestriccionFasePlanaUserPromptTemplate.Should().Contain("{DOCUMENT_TEXT}");
        GptClasificarDataProvider.RestriccionFasePlanaUserPromptTemplate.Should().NotContain("{TDN1_CATALOG}");
    }
}
