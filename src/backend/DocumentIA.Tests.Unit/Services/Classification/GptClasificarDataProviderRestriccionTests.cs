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
    public void RestriccionInstructions_MencionanRespuestaNull()
    {
        GptClasificarDataProvider.RestriccionPhase1Instruction.Should().Contain("null");
        GptClasificarDataProvider.RestriccionPhase2Instruction.Should().Contain("null");
    }
}
