using Bunit;
using DocumentIA.Admin.Components.Monitor;
using DocumentIA.Admin.Services;
using FluentAssertions;

namespace DocumentIA.Tests.Admin.Components;

/// <summary>
/// Una fila servida por reutilizacion tiene que distinguirse a simple vista: sus
/// confianzas y su tipologia son las del original y sin la marca se leerian como si
/// el documento se hubiera procesado otra vez (AB#100258).
/// </summary>
public class MonitorTablaReutilizadasTests : TestContext
{
    [Fact]
    public void Tabla_Should_MarcarLaFilaReutilizada()
    {
        var cut = RenderComponent<MonitorTabla>(p => p.Add(c => c.Pagina, UnaPagina(reutilizada: true)));

        cut.Markup.Should().Contain("Reutilizada");
    }

    [Fact]
    public void Tabla_Should_NoMarcarUnaEjecucionNormal()
    {
        var cut = RenderComponent<MonitorTabla>(p => p.Add(c => c.Pagina, UnaPagina(reutilizada: false)));

        cut.Markup.Should().NotContain("Reutilizada");
    }

    private static PagedResultDto<EjecucionResumenDto> UnaPagina(bool reutilizada) => new()
    {
        Page = 1,
        PageSize = 25,
        Total = 1,
        Items =
        [
            new EjecucionResumenDto
            {
                Id = reutilizada ? 3 : 1,
                EjecucionGuid = "8f3a91c2-4d7e-4b1a-9c33-0e5f7a2b6d18",
                FechaEjecucion = new DateTime(2026, 9, 10, 8, 0, 0, DateTimeKind.Unspecified),
                NombreDocumento = "nota_simple.pdf",
                Tipologia = "TDN1-NOTA",
                TipoFlujo = "Completo",
                EstadoFinal = "OK",
                ConfianzaGlobal = 0.91,
                DuracionTotalMs = reutilizada ? 40 : 18400,
                ReutilizadaPorDuplicado = reutilizada,
                EjecucionOriginalId = reutilizada ? 2 : null
            }
        ]
    };
}
