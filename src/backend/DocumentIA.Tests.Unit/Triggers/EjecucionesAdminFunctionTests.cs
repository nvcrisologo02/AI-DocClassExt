using DocumentIA.Functions.Triggers.Admin;
using FluentAssertions;

namespace DocumentIA.Tests.Unit.Triggers;

public class EjecucionesAdminFunctionTests
{
    [Fact]
    public void ParsePaginacion_Should_UsarValoresPorDefecto()
    {
        var (page, pageSize) = EjecucionesAdminFunction.ParsePaginacion(null, null);

        page.Should().Be(1);
        pageSize.Should().Be(25);
    }

    [Theory]
    [InlineData("0", 1)]
    [InlineData("-5", 1)]
    [InlineData("3", 3)]
    public void ParsePaginacion_Should_AcotarLaPaginaAlMinimo(string entrada, int esperado)
    {
        var (page, _) = EjecucionesAdminFunction.ParsePaginacion(entrada, null);
        page.Should().Be(esperado);
    }

    [Theory]
    [InlineData("0", 1)]
    [InlineData("500", 200)]
    [InlineData("50", 50)]
    [InlineData("noesunnumero", 25)]
    public void ParsePaginacion_Should_AcotarElTamanoDePagina(string entrada, int esperado)
    {
        var (_, pageSize) = EjecucionesAdminFunction.ParsePaginacion(null, entrada);
        pageSize.Should().Be(esperado);
    }

    [Fact]
    public void ParseFiltro_Should_UsarLosUltimosSieteDiasSiNoHayRango()
    {
        var filtro = EjecucionesAdminFunction.ParseFiltro(new Dictionary<string, string>());

        (filtro.Hasta - filtro.Desde).TotalDays.Should().BeApproximately(7, 0.01);
    }

    [Fact]
    public void ParseFiltro_Should_LeerElRangoIndicado()
    {
        var filtro = EjecucionesAdminFunction.ParseFiltro(new Dictionary<string, string>
        {
            ["desde"] = "2026-08-01T00:00:00Z",
            ["hasta"] = "2026-08-05T00:00:00Z"
        });

        filtro.Desde.Should().Be(new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc));
        filtro.Hasta.Should().Be(new DateTime(2026, 8, 5, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void ParseFiltro_Should_RecogerLosDemasCriterios()
    {
        var filtro = EjecucionesAdminFunction.ParseFiltro(new Dictionary<string, string>
        {
            ["tipologia"] = "NOTS",
            ["estado"] = "ERROR",
            ["flujo"] = "Clasificacion",
            ["q"] = "escritura",
            ["submittedby"] = "batch-integracion",
            ["sourcesystem"] = "Colabora"
        });

        filtro.Tipologia.Should().Be("NOTS");
        filtro.Estado.Should().Be("ERROR");
        filtro.Flujo.Should().Be("Clasificacion");
        filtro.Busqueda.Should().Be("escritura");
        filtro.SubmittedBy.Should().Be("batch-integracion");
        filtro.SourceSystem.Should().Be("Colabora");
    }

    [Fact]
    public void ParseFiltro_Should_IgnorarUnRangoInvertido()
    {
        var filtro = EjecucionesAdminFunction.ParseFiltro(new Dictionary<string, string>
        {
            ["desde"] = "2026-08-10T00:00:00Z",
            ["hasta"] = "2026-08-01T00:00:00Z"
        });

        filtro.Desde.Should().BeBefore(filtro.Hasta, "un rango invertido devolveria siempre vacio sin explicacion");
    }
}
