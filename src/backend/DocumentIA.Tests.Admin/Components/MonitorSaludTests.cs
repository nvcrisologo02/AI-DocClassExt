using Bunit;
using DocumentIA.Admin.Components.Monitor;
using DocumentIA.Admin.Services;
using FluentAssertions;

namespace DocumentIA.Tests.Admin.Components;

public class MonitorSaludTests : TestContext
{
    private static SystemHealthDto Salud(string gdc = "healthy", string global = "healthy") => new()
    {
        Ok = global == "healthy",
        Status = global,
        Timestamp = new DateTimeOffset(2026, 8, 10, 9, 24, 7, TimeSpan.Zero),
        Components = new HealthComponentsDto
        {
            Functions = new HealthComponentDto { Status = "healthy", Message = "ok" },
            AssetResolver = new HealthComponentDto { Status = "healthy", Message = "ok" },
            Gdc = new HealthComponentDto { Status = gdc, Message = "Failed to generate CWS Credential" },
            ModelProviders = new ModelProvidersHealthDto
            {
                Status = "healthy",
                Classification = new HealthComponentDto { Status = "healthy", Message = "ok" },
                Extraction = new HealthComponentDto { Status = "healthy", Message = "ok" },
                Prompt = new HealthComponentDto { Status = "degraded", Message = "lento" }
            }
        }
    };

    [Fact]
    public void MuestraLosSeisComponentesDelBackend()
    {
        var cut = RenderComponent<MonitorSalud>(p => p.Add(c => c.Salud, Salud()));

        foreach (var nombre in new[] { "functions", "assetResolver", "gdc", "clasificación", "extracción", "prompt" })
        {
            cut.Markup.Should().Contain(nombre);
        }
    }

    // Un componente caido tiene que distinguirse sin depender solo del color.
    [Fact]
    public void ComponenteCaido_MuestraSuMensajeDeError()
    {
        var cut = RenderComponent<MonitorSalud>(p => p.Add(c => c.Salud, Salud(gdc: "unhealthy")));

        cut.Markup.Should().Contain("Failed to generate CWS Credential");
    }

    [Fact]
    public void ComponenteSano_NoGastaEspacioEnRepetirQueEstaBien()
    {
        var cut = RenderComponent<MonitorSalud>(p => p.Add(c => c.Salud, Salud()));

        cut.Markup.Should().NotContain(">ok<",
            "el mensaje de un componente sano no aporta nada; basta el indicador");
    }

    // Coherente con el resto de la pagina: nada de hora del servidor.
    [Fact]
    public void MarcaDeTiempo_SeMuestraEnHoraPeninsular()
    {
        var cut = RenderComponent<MonitorSalud>(p => p.Add(c => c.Salud, Salud()));

        cut.Markup.Should().Contain("11:24:07", "09:24:07 UTC de un 10 de agosto son las 11:24:07 peninsulares");
    }

    // El proyecto sirve Bootstrap 5.1: las utilidades -subtle y -emphasis son de
    // 5.3 y aqui no existen, asi que un badge que dependa de ellas se renderiza
    // sin fondo y resulta invisible.
    [Fact]
    public void Badges_NoDependenDeClasesQueBootstrap51NoTiene()
    {
        var cut = RenderComponent<MonitorSalud>(p => p.Add(c => c.Salud, Salud(gdc: "unhealthy")));

        cut.Markup.Should().NotContain("-subtle").And.NotContain("-emphasis");
    }

    [Fact]
    public void Badges_LlevanColorDeFondoExplicito()
    {
        var cut = RenderComponent<MonitorSalud>(p => p.Add(c => c.Salud, Salud(gdc: "unhealthy")));

        cut.Markup.Should().Contain("background:#",
            "sin fondo propio la pastilla no se distingue del papel");
    }

    [Fact]
    public void SinDatos_LoIndicaEnVezDeFingirQueTodoVaBien()
    {
        var cut = RenderComponent<MonitorSalud>(p => p.Add(c => c.Salud, (SystemHealthDto?)null));

        cut.Markup.Should().Contain("sin datos");
    }
}
