using Bunit;
using DocumentIA.Admin.Components.Monitor;
using DocumentIA.Admin.Services;
using FluentAssertions;

namespace DocumentIA.Tests.Admin.Components;

public class MonitorTablaTests : TestContext
{
    private static PagedResultDto<EjecucionResumenDto> UnaPagina(
        int total = 120, int pageSize = 25, DateTime? fecha = null) => new()
    {
        Page = 1,
        PageSize = pageSize,
        Total = total,
        Items =
        [
            new EjecucionResumenDto
            {
                EjecucionGuid = "8f3a91c2-4d7e-4b1a-9c33-0e5f7a2b6d18",
                // Como llega del backend: valor UTC serializado sin sufijo Z,
                // por tanto con Kind=Unspecified.
                FechaEjecucion = fecha ?? new DateTime(2026, 8, 10, 9, 11, 49, DateTimeKind.Unspecified),
                NombreDocumento = "nota_simple.pdf",
                Tipologia = "TDN1-NOTA",
                TipoFlujo = "Completo",
                EstadoFinal = "OK",
                ConfianzaGlobal = 0.91,
                DuracionTotalMs = 18400
            }
        ]
    };

    // El render ocurre en el servidor (App Service en UTC), asi que la hora debe
    // convertirse explicitamente a la peninsular en vez de confiar en la zona
    // del proceso.
    [Fact]
    public void FechaDeEjecucion_SeMuestraEnHoraPeninsular()
    {
        var cut = RenderComponent<MonitorTabla>(p => p.Add(c => c.Pagina, UnaPagina()));

        cut.Markup.Should().Contain("10/08/2026 11:11",
            "las 09:11 UTC de un 10 de agosto son las 11:11 en la peninsula");
    }

    [Fact]
    public void TamanoDePagina_OfreceLasTresOpciones()
    {
        var cut = RenderComponent<MonitorTabla>(p => p
            .Add(c => c.Pagina, UnaPagina())
            .Add(c => c.TamanoPagina, 25));

        var opciones = cut.FindAll("select option").Select(o => o.TextContent.Trim()).ToList();

        opciones.Should().Contain(["25", "50", "100"]);
    }

    [Fact]
    public void TamanoDePagina_AlCambiarloEmiteElNuevoValor()
    {
        var emitido = 0;
        var cut = RenderComponent<MonitorTabla>(p => p
            .Add(c => c.Pagina, UnaPagina())
            .Add(c => c.TamanoPagina, 25)
            .Add(c => c.TamanoPaginaCambiado, (int v) => emitido = v));

        cut.Find("select").Change("100");

        emitido.Should().Be(100);
    }

    // El pie con el selector es el unico control para cambiar el tamaño, asi que
    // debe estar tambien cuando hay una sola pagina de resultados.
    [Fact]
    public void TamanoDePagina_SeMuestraAunqueHayaUnaSolaPagina()
    {
        var cut = RenderComponent<MonitorTabla>(p => p
            .Add(c => c.Pagina, UnaPagina(total: 3))
            .Add(c => c.TamanoPagina, 25));

        cut.FindAll("select").Should().NotBeEmpty(
            "sin el selector visible no habria forma de volver a 50 o 100 tras filtrar");
    }
}
