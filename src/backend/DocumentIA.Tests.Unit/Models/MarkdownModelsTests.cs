using DocumentIA.Core.Models;
using FluentAssertions;

namespace DocumentIA.Tests.Unit.Models;

public class MarkdownModelsTests
{
    private static ResultadoMarkdown Con(string? md, int paginas, bool completo) => new()
    {
        Markdown = md,
        Paginas = paginas,
        Completo = completo,
        Fuente = FuenteMarkdown.Layout
    };

    [Fact]
    public void Cubre_CompletoSirveParaCualquierNecesidad()
    {
        Con("# x", 14, completo: true).Cubre(NecesidadMarkdown.Completo()).Should().BeTrue();
        Con("# x", 14, completo: true).Cubre(NecesidadMarkdown.Paginas(3)).Should().BeTrue();
    }

    [Fact]
    public void Cubre_ParcialSirveSoloSiLlegaALasPaginasPedidas()
    {
        Con("# x", 5, completo: false).Cubre(NecesidadMarkdown.Paginas(3)).Should().BeTrue();
        Con("# x", 5, completo: false).Cubre(NecesidadMarkdown.Paginas(5)).Should().BeTrue();
        Con("# x", 2, completo: false).Cubre(NecesidadMarkdown.Paginas(3)).Should().BeFalse();
        Con("# x", 5, completo: false).Cubre(NecesidadMarkdown.Completo()).Should().BeFalse();
    }

    [Fact]
    public void Cubre_SinContenidoNuncaCubre()
    {
        Con(null, 99, completo: true).Cubre(NecesidadMarkdown.Paginas(1)).Should().BeFalse();
        Con("   ", 99, completo: true).Cubre(NecesidadMarkdown.Completo()).Should().BeFalse();
    }

    [Fact]
    public void Paginas_NuncaBajaDeUna()
    {
        NecesidadMarkdown.Paginas(0).PaginasMinimas.Should().Be(1);
        NecesidadMarkdown.Paginas(-4).PaginasMinimas.Should().Be(1);
        NecesidadMarkdown.Completo().DocumentoCompleto.Should().BeTrue();
    }

    // ========== AB#100880: causa de "sin contenido" ==========

    [Theory]
    [InlineData(MotivoSinContenido.LayoutTimeout, null, true)]
    [InlineData(MotivoSinContenido.LayoutError, 429, true)]
    [InlineData(MotivoSinContenido.LayoutError, 500, true)]
    [InlineData(MotivoSinContenido.LayoutError, 503, true)]
    [InlineData(MotivoSinContenido.LayoutError, 400, false)]
    [InlineData(MotivoSinContenido.LayoutError, 401, false)]
    [InlineData(MotivoSinContenido.LayoutError, null, false)]
    [InlineData(MotivoSinContenido.SinFuente, null, false)]
    [InlineData(MotivoSinContenido.FormatoNoSoportado, null, false)]
    [InlineData(MotivoSinContenido.DocumentoSinTexto, null, false)]
    public void CausaSinContenido_EsTransitoria_SoloConTimeoutO429O5xx(MotivoSinContenido motivo, int? codigoHttp, bool esperado)
    {
        new CausaSinContenido { Motivo = motivo, CodigoHttp = codigoHttp }.EsTransitoria.Should().Be(esperado);
    }

    [Fact]
    public void CausaSinContenido_DesdeExcepcion_TimeoutEsLayoutTimeout()
    {
        var causa = CausaSinContenido.DesdeExcepcion(new TimeoutException("Timeout esperando resultado de DI layout"));

        causa.Motivo.Should().Be(MotivoSinContenido.LayoutTimeout);
        causa.CodigoHttp.Should().BeNull();
        causa.Detalle.Should().Contain("TimeoutException");
    }

    [Fact]
    public void CausaSinContenido_DesdeExcepcion_CancelacionInternaDelClienteHttpEsLayoutTimeout()
    {
        // HttpClient.Timeout agota en TaskCanceledException sin que nadie haya cancelado el token.
        var causa = CausaSinContenido.DesdeExcepcion(new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout of 100 seconds elapsing."));

        causa.Motivo.Should().Be(MotivoSinContenido.LayoutTimeout);
    }

    [Fact]
    public void CausaSinContenido_DesdeExcepcion_LayoutRequestLlevaElCodigoHttp()
    {
        var causa = CausaSinContenido.DesdeExcepcion(new LayoutRequestException(
            401, "Error iniciando DI layout. Status=401. Body={\"error\":{\"code\":\"PermissionDenied\"}}"));

        causa.Motivo.Should().Be(MotivoSinContenido.LayoutError);
        causa.CodigoHttp.Should().Be(401);
        causa.Detalle.Should().Contain("LayoutRequestException").And.Contain("PermissionDenied");
    }

    [Fact]
    public void CausaSinContenido_DesdeExcepcion_415EsFormatoNoSoportado()
    {
        var causa = CausaSinContenido.DesdeExcepcion(new LayoutRequestException(415, "Unsupported media type"));

        causa.Motivo.Should().Be(MotivoSinContenido.FormatoNoSoportado);
        causa.CodigoHttp.Should().Be(415);
    }

    [Fact]
    public void CausaSinContenido_DesdeExcepcion_HttpRequestConCodigoEsLayoutErrorConCodigo()
    {
        var causa = CausaSinContenido.DesdeExcepcion(
            new HttpRequestException("Service Unavailable", null, System.Net.HttpStatusCode.ServiceUnavailable));

        causa.Motivo.Should().Be(MotivoSinContenido.LayoutError);
        causa.CodigoHttp.Should().Be(503);
        causa.EsTransitoria.Should().BeTrue();
    }

    [Fact]
    public void CausaSinContenido_DesdeExcepcion_OtraExcepcionEsLayoutErrorConSuTipo()
    {
        var causa = CausaSinContenido.DesdeExcepcion(
            new InvalidOperationException("El modelo de layout no tiene Endpoint configurado en base de datos."));

        causa.Motivo.Should().Be(MotivoSinContenido.LayoutError);
        causa.CodigoHttp.Should().BeNull();
        causa.Detalle.Should().StartWith("InvalidOperationException: El modelo de layout");
    }

    [Fact]
    public void CausaSinContenido_Describir_IncluyeMotivoCodigoYDetalle()
    {
        new CausaSinContenido { Motivo = MotivoSinContenido.LayoutError, CodigoHttp = 400, Detalle = "LayoutRequestException: InvalidContent" }
            .Describir().Should().Be("LayoutError (HTTP 400): LayoutRequestException: InvalidContent");
        new CausaSinContenido { Motivo = MotivoSinContenido.SinFuente }.Describir().Should().Be("SinFuente");
    }

    [Fact]
    public void CausaSinContenido_Detalle_SeRecortaAUnTamanoRazonable()
    {
        var causa = CausaSinContenido.DesdeExcepcion(new InvalidOperationException(new string('x', 2000)));

        causa.Detalle!.Length.Should().BeLessThanOrEqualTo(300);
    }
}
