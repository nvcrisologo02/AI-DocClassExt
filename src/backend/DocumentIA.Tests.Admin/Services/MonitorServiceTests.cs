using System.Net;
using DocumentIA.Admin.Services;
using DocumentIA.Tests.Admin.Helpers;
using FluentAssertions;

namespace DocumentIA.Tests.Admin.Services;

public class MonitorServiceTests
{
    private static MonitorService CreateService(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var client = new HttpClient(new StubHttpMessageHandler(responder))
        {
            BaseAddress = new Uri("http://localhost/api/")
        };
        return new MonitorService(client);
    }

    [Fact]
    public async Task GetEjecucionDetalleAsync_GuidInexistente404_DevuelveNull()
    {
        var service = CreateService(_ => StubHttpMessageHandler.Json("", HttpStatusCode.NotFound));

        var detalle = await service.GetEjecucionDetalleAsync(Guid.NewGuid().ToString());

        detalle.Should().BeNull("un 404 significa que no existe ninguna ejecucion con ese identificador, no un fallo de comunicacion");
    }

    [Fact]
    public async Task GetEjecucionDetalleAsync_ErrorServidor500_LanzaInvalidOperationException()
    {
        var service = CreateService(_ => StubHttpMessageHandler.Json("", HttpStatusCode.InternalServerError));

        var accion = async () => await service.GetEjecucionDetalleAsync(Guid.NewGuid().ToString());

        await accion.Should().ThrowAsync<InvalidOperationException>("un fallo de comunicacion distinto de 404 debe seguir reportandose como error tecnico");
    }

    [Fact]
    public async Task GetEjecucionesAsync_RespuestaJsonConFormaInesperada_LanzaInvalidOperationException()
    {
        // Un backend en una version distinta (Admin y Functions se despliegan por
        // separado) podria devolver un array plano en vez del contrato paginado.
        var service = CreateService(_ => StubHttpMessageHandler.Json("[]"));

        var accion = async () => await service.GetEjecucionesAsync(new MonitorFiltroDto());

        await accion.Should().ThrowAsync<InvalidOperationException>(
            "un fallo de deserializacion no debe escapar como JsonException y tumbar el circuito de Blazor Server");
    }

    [Fact]
    public async Task GetEjecucionDetalleAsync_RespuestaJsonConFormaInesperada_LanzaInvalidOperationException()
    {
        var service = CreateService(_ => StubHttpMessageHandler.Json("[]"));

        var accion = async () => await service.GetEjecucionDetalleAsync(Guid.NewGuid().ToString());

        await accion.Should().ThrowAsync<InvalidOperationException>(
            "un fallo de deserializacion no debe escapar como JsonException y tumbar el circuito de Blazor Server");
    }

    [Fact]
    public async Task GetAgregadosAsync_RespuestaJsonConFormaInesperada_DevuelveNullSinLanzar()
    {
        // Los agregados son auxiliares del cuadro de mando: un fallo aqui no debe
        // impedir ver el resto de la pagina.
        var service = CreateService(_ => StubHttpMessageHandler.Json("[]"));

        var agregados = await service.GetAgregadosAsync(new MonitorFiltroDto());

        agregados.Should().BeNull();
    }

    // La ventana temporal se materializa contra el reloj en cada consulta. Cuando
    // el filtro guardaba fechas absolutas calculadas al construirlo, el
    // auto-refresco repetia siempre la misma consulta y las ejecuciones nuevas no
    // aparecian nunca.
    [Fact]
    public async Task ToQueryString_DosConsultasSeparadasEnElTiempo_AvanzanElExtremoSuperior()
    {
        var filtro = new MonitorFiltroDto();

        var primera = ExtraerInstante(filtro.ToQueryString(), "hasta");
        await Task.Delay(200);
        var segunda = ExtraerInstante(filtro.ToQueryString(), "hasta");

        segunda.Should().BeAfter(primera,
            "cada refresco debe mirar hasta el instante actual; si no, las ejecuciones nuevas quedan siempre fuera de la ventana");
    }

    [Fact]
    public void ToQueryString_RangoDias_ProduceUnaVentanaDeEsaAnchura()
    {
        var filtro = new MonitorFiltroDto { RangoDias = 30 };

        var query = filtro.ToQueryString();

        var desde = ExtraerInstante(query, "desde");
        var hasta = ExtraerInstante(query, "hasta");
        (hasta - desde).TotalDays.Should().BeApproximately(30, 0.01);
    }

    [Fact]
    public void ToQueryString_ExtremoSuperior_EsElInstanteActual()
    {
        var hasta = ExtraerInstante(new MonitorFiltroDto().ToQueryString(), "hasta");

        hasta.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    private static DateTime ExtraerInstante(string query, string parametro)
    {
        var parte = query.Split('&').Single(p => p.StartsWith($"{parametro}=", StringComparison.Ordinal));
        var valor = Uri.UnescapeDataString(parte[(parametro.Length + 1)..]);
        return DateTime.Parse(valor, null,
            System.Globalization.DateTimeStyles.AdjustToUniversal |
            System.Globalization.DateTimeStyles.AssumeUniversal);
    }

    [Fact]
    public void ToQueryString_ConSubmittedBy_IncluyeElParametroCodificado()
    {
        var filtro = new MonitorFiltroDto { SubmittedBy = "juan perez" };

        var query = filtro.ToQueryString();

        query.Should().Contain("submittedby=juan%20perez");
    }

    [Fact]
    public void ToQueryString_SinSubmittedBy_NoIncluyeElParametro()
    {
        var filtro = new MonitorFiltroDto { SubmittedBy = null };

        var query = filtro.ToQueryString();

        query.Should().NotContain("submittedby");
    }

    [Fact]
    public void Clonar_CopiaSubmittedBy()
    {
        var filtro = new MonitorFiltroDto { SubmittedBy = "juan perez" };

        var clon = filtro.Clonar();

        clon.SubmittedBy.Should().Be("juan perez");
    }

    [Fact]
    public void Clonar_CopiaRangoDias()
    {
        var filtro = new MonitorFiltroDto { RangoDias = 90 };

        var clon = filtro.Clonar();

        clon.RangoDias.Should().Be(90);
    }

    // El timeout de HttpClient (100 s por defecto) llega como TaskCanceledException,
    // no como HttpRequestException. Sin traducirlo, escapa de los catch de la pagina
    // del Monitor y termina el circuito de Blazor Server con "Server returned an
    // error on close" en vez de mostrar el mensaje de error.
    [Fact]
    public async Task GetEjecucionesAsync_TimeoutDeHttpClient_LanzaInvalidOperationException()
    {
        var service = CreateService(_ => throw new TaskCanceledException(
            "The request was canceled due to the configured HttpClient.Timeout", new TimeoutException()));

        var accion = async () => await service.GetEjecucionesAsync(new MonitorFiltroDto());

        await accion.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GetEjecucionDetalleAsync_TimeoutDeHttpClient_LanzaInvalidOperationException()
    {
        var service = CreateService(_ => throw new TaskCanceledException(
            "The request was canceled due to the configured HttpClient.Timeout", new TimeoutException()));

        var accion = async () => await service.GetEjecucionDetalleAsync(Guid.NewGuid().ToString());

        await accion.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GetAgregadosAsync_TimeoutDeHttpClient_DevuelveNullSinLanzar()
    {
        var service = CreateService(_ => throw new TaskCanceledException(
            "The request was canceled due to the configured HttpClient.Timeout", new TimeoutException()));

        var agregados = await service.GetAgregadosAsync(new MonitorFiltroDto());

        agregados.Should().BeNull("los agregados son auxiliares: un timeout no debe impedir ver el listado");
    }

    // La cancelacion propia (navegar fuera de la pagina mientras hay una peticion en
    // vuelo) tambien llega como OperationCanceledException y no debe tumbar nada.
    [Fact]
    public async Task GetEjecucionesAsync_CancelacionDelLlamante_LanzaInvalidOperationException()
    {
        var service = CreateService(_ => throw new OperationCanceledException());

        var accion = async () => await service.GetEjecucionesAsync(new MonitorFiltroDto());

        await accion.Should().ThrowAsync<InvalidOperationException>();
    }
}
