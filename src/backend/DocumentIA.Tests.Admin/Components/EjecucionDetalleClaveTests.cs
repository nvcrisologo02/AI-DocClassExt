using Bunit;
using DocumentIA.Admin.Components.Monitor;
using DocumentIA.Admin.Services;
using FluentAssertions;

namespace DocumentIA.Tests.Admin.Components;

/// <summary>
/// La regla de reutilizacion tiene que poder auditarse desde el Monitor (AB#100930): el detalle
/// ensena los componentes de la clave y el motivo.
/// </summary>
public class EjecucionDetalleClaveTests : TestContext
{
    [Fact]
    public void Detalle_Should_MostrarComponentesDeLaClaveYMotivo()
    {
        var detalle = new EjecucionDetalleDto
        {
            Id = 1,
            EjecucionGuid = "g",
            Resultado = new ResultadoDetalleDto { Estado = "OK" },
            ClaveIntencion = "0123456789abcdef0123456789abcdef",
            ClaveIntencionComponentes = new ClaveIntencionComponentesDto
            {
                Modo = "solo-clasificacion",
                Nivel = "TDN1_TDN2",
                Universo = "NOTA.SIMPLE,TASACION",
                ExpectedType = "",
                ModeloClasificacion = "gpt-4o-mini"
            },
            MotivoReutilizacion = "clave-distinta"
        };

        var cut = RenderComponent<EjecucionDetalle>(p => p.Add(c => c.Detalle, detalle));

        cut.Markup.Should().Contain("0123456789abcdef0123456789abcdef");
        cut.Markup.Should().Contain("solo-clasificacion");
        cut.Markup.Should().Contain("NOTA.SIMPLE,TASACION");
        cut.Markup.Should().Contain("gpt-4o-mini");
        cut.Markup.Should().Contain("clave-distinta");
    }

    [Fact]
    public void Detalle_Should_NoPintarElBloqueSinClave()
    {
        var detalle = new EjecucionDetalleDto { Id = 1, EjecucionGuid = "g", Resultado = new ResultadoDetalleDto { Estado = "OK" } };

        var cut = RenderComponent<EjecucionDetalle>(p => p.Add(c => c.Detalle, detalle));

        cut.Markup.Should().NotContain("Clave de intención");
    }
}
