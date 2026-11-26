using System.Text.RegularExpressions;
using Bunit;
using DocumentIA.Admin.Components.Monitor;
using DocumentIA.Admin.Services;
using FluentAssertions;

namespace DocumentIA.Tests.Admin.Components;

// Criterio 1 de AB#101124: los componentes de graficos y KPIs no pintan colores
// fuera de la paleta corporativa. Todo color hexadecimal del marcado tiene que
// ser uno de SarebPaleta.
public class PaletaComponentesTests : TestContext
{
    private static readonly HashSet<string> Paleta = new(StringComparer.OrdinalIgnoreCase)
    {
        SarebPaleta.Verde, SarebPaleta.AzulOscuro, SarebPaleta.Azul, SarebPaleta.Turquesa, SarebPaleta.VerdeClaro,
        SarebPaleta.VerdeGris, SarebPaleta.Gris, SarebPaleta.GrisClaro, SarebPaleta.Naranja, SarebPaleta.Rojo
    };

    private static void SoloColoresDeLaPaleta(string markup) =>
        Regex.Matches(markup, "#[0-9a-fA-F]{6}\\b").Select(m => m.Value).Distinct()
            .Where(c => !Paleta.Contains(c))
            .Should().BeEmpty();

    private static List<AgregadoGrupoDto> Grupos() =>
    [
        new() { Grupo = "nots.01", Total = 10, Ok = 6, Revision = 3, Error = 1, Fallbacks = 2 },
        new() { Grupo = "OK", Total = 6, Ok = 6 },
        new() { Grupo = "Error", Total = 2, Error = 2 },
        new() { Grupo = "NO_CLASIFICADO", Total = 1, Revision = 1 },
        new() { Grupo = "VALIDACION_CON_ERRORES", Total = 1, Error = 1 },
        new() { Grupo = "OTRO", Total = 1 }
    ];

    [Fact]
    public void CostesDesglose_SoloColoresDeLaPaleta()
    {
        var costes = new CostesResumenDto
        {
            TotalEjecuciones = 4,
            EjecucionesConImporte = 4,
            CosteTotalEur = 10m,
            LayoutEur = 4m,
            ClasificacionEur = 3m,
            ExtraccionEur = 2m,
            PromptEur = 1m
        };

        var cut = RenderComponent<CostesDesglose>(p => p.Add(c => c.Costes, costes));

        SoloColoresDeLaPaleta(cut.Markup);
    }

    [Fact]
    public void CostesKpis_SoloColoresDeLaPaleta()
    {
        var costes = new CostesResumenDto { TotalEjecuciones = 4, ConCosteReal = 3, ConCosteEstimado = 1, CosteTotalEur = 1m };

        var cut = RenderComponent<CostesKpis>(p => p.Add(c => c.Costes, costes));

        SoloColoresDeLaPaleta(cut.Markup);
    }

    [Fact]
    public void MonitorHistograma_SoloColoresDeLaPaleta()
    {
        var bins = new List<HistogramaBinDto>
        {
            new() { Desde = 0.5, Hasta = 0.6, Total = 1 },
            new() { Desde = 0.7, Hasta = 0.8, Total = 2 },
            new() { Desde = 0.9, Hasta = 1.0, Total = 3 }
        };

        var cut = RenderComponent<MonitorHistograma>(p => p.Add(c => c.Bins, bins));

        SoloColoresDeLaPaleta(cut.Markup);
    }

    [Fact]
    public void MonitorMatriz_SoloColoresDeLaPaleta()
    {
        var celdas = new List<MatrizCeldaDto>
        {
            new() { EstadoProceso = "OK", Calidad = "OK", Total = 40 },
            new() { EstadoProceso = "OK", Calidad = "REVISION", Total = 10 },
            new() { EstadoProceso = "Error", Calidad = "ERROR", Total = 1 }
        };

        var cut = RenderComponent<MonitorMatriz>(p => p.Add(c => c.Celdas, celdas));

        SoloColoresDeLaPaleta(cut.Markup);
    }

    [Fact]
    public void MonitorDesglose_SoloColoresDeLaPaleta()
    {
        var cut = RenderComponent<MonitorDesglose>(p => p
            .Add(c => c.Tipologias, Grupos())
            .Add(c => c.Modelos, Grupos()));

        SoloColoresDeLaPaleta(cut.Markup);
    }

    [Fact]
    public void MonitorCalidadKpis_SoloColoresDeLaPaleta()
    {
        var agregados = new DashboardAgregadosDto { TotalEjecuciones = 10, Ok = 6, Revision = 3, Error = 1 };

        var cut = RenderComponent<MonitorCalidadKpis>(p => p.Add(c => c.Agregados, agregados));

        SoloColoresDeLaPaleta(cut.Markup);
    }

    [Fact]
    public void MonitorLecturas_SoloColoresDeLaPaleta()
    {
        var cut = RenderComponent<MonitorLecturas>(p => p
            .Add(c => c.Proceso, Grupos())
            .Add(c => c.CalidadOk, 6)
            .Add(c => c.CalidadRevision, 3)
            .Add(c => c.CalidadError, 1));

        SoloColoresDeLaPaleta(cut.Markup);
    }
}
