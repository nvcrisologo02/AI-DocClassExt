using System.Text.Json;
using DocumentIA.Core.Models;
using DocumentIA.Core.Services;
using FluentAssertions;

namespace DocumentIA.Tests.Unit.Services;

public class ContratoTimelineRehidratadorTests
{
    private static string TimelineJson() => JsonSerializer.Serialize(new List<TrazaActividad>
    {
        new() { Nombre = "Clasificar", Estado = "Completed", DuracionMs = 900 },
        new() { Nombre = "Persistir", Estado = "Completed", DuracionMs = 40 }
    });

    [Fact]
    public void Rehidratar_ContratoPodado_RellenaElTimelineDesdeLaColumna()
    {
        var salida = new ContratoSalida();
        salida.DetalleEjecucion.Seguimiento.Actividades = new List<TrazaActividad>();

        ContratoTimelineRehidratador.Rehidratar(salida, TimelineJson());

        salida.DetalleEjecucion.Seguimiento.Actividades.Should().HaveCount(2);
        salida.DetalleEjecucion.Seguimiento.Actividades[0].Nombre.Should().Be("Clasificar");
        salida.DetalleEjecucion.Seguimiento.Actividades[0].DuracionMs.Should().Be(900);
    }

    [Fact]
    public void Rehidratar_ContratoHistoricoConTimeline_NoLoPisa()
    {
        // Filas anteriores a AB#100166 traen el timeline dentro del contrato: manda ese.
        var salida = new ContratoSalida();
        salida.DetalleEjecucion.Seguimiento.Actividades = new List<TrazaActividad>
        {
            new() { Nombre = "Historico", Estado = "Completed" }
        };

        ContratoTimelineRehidratador.Rehidratar(salida, TimelineJson());

        salida.DetalleEjecucion.Seguimiento.Actividades.Should().HaveCount(1);
        salida.DetalleEjecucion.Seguimiento.Actividades[0].Nombre.Should().Be("Historico");
    }

    [Fact]
    public void Rehidratar_SinColumna_DejaElContratoIntacto()
    {
        var salida = new ContratoSalida();
        salida.DetalleEjecucion.Seguimiento.Actividades = new List<TrazaActividad>();

        ContratoTimelineRehidratador.Rehidratar(salida, null);
        ContratoTimelineRehidratador.Rehidratar(salida, "   ");

        salida.DetalleEjecucion.Seguimiento.Actividades.Should().BeEmpty();
    }

    [Fact]
    public void Rehidratar_JsonInvalido_NoLanza()
    {
        var salida = new ContratoSalida();
        salida.DetalleEjecucion.Seguimiento.Actividades = new List<TrazaActividad>();

        var act = () => ContratoTimelineRehidratador.Rehidratar(salida, "{esto no es json}");

        act.Should().NotThrow();
        salida.DetalleEjecucion.Seguimiento.Actividades.Should().BeEmpty();
    }
}
