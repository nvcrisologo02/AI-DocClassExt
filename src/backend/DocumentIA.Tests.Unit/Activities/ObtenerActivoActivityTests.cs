#nullable enable
using System.Net;
using System.Text.Json;
using DocumentIA.Core.Models;
using DocumentIA.Functions.Activities;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;

namespace DocumentIA.Tests.Unit.Activities;

public class ObtenerActivoActivityTests
{
    private readonly Mock<ILogger<ObtenerActivoActivity>> _loggerMock = new();

    private ObtenerActivoActivity CreateSut(HttpResponseMessage response)
    {
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);

        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("http://localhost:5006/")
        };

        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(f => f.CreateClient("AssetResolver")).Returns(httpClient);

        return new ObtenerActivoActivity(_loggerMock.Object, factoryMock.Object);
    }

    private ObtenerActivoActivity CreateSut(
        HttpResponseMessage response,
        Action<HttpRequestMessage> requestAssert)
    {
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>((req, _) =>
            {
                requestAssert(req);
                return Task.FromResult(response);
            });

        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("http://localhost:5006/")
        };

        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(f => f.CreateClient("AssetResolver")).Returns(httpClient);

        return new ObtenerActivoActivity(_loggerMock.Object, factoryMock.Object);
    }

    private static ObtenerActivoInput CreateInput(
        string? idufirOverride = null,
        string? refCatOverride = null,
        List<string>? camposSolicitados = null)
    {
        return new ObtenerActivoInput
        {
            CorrelationId = "test-corr-001",
            Tipologia = "nota_simple.v1",
            DatosExtraidos = new Dictionary<string, object>
            {
                ["IDUFIR"] = "12345678901234",
                ["ReferenciaCatastral"] = "1234567890123456789012"
            },
            CamposSolicitados = camposSolicitados,
            IdufirOverride = idufirOverride,
            ReferenciaCatastralOverride = refCatOverride
        };
    }

    [Fact]
    public async Task Run_SingleMatch_ReturnsOneActivo()
    {
        var pluginResponse = new
        {
            CorrelationId = "test-corr-001",
            Found = true,
            Count = 1,
            CriteriosUsados = new { Idufir = "12345678901234", ReferenciaCatastral = (string?)null },
            Activos = new[]
            {
                new
                {
                    IdActivo = "100001",
                    FchCierre = DateTime.UtcNow,
                    CamposSolicitados = new Dictionary<string, object?> { ["DesProvnc"] = "MADRID" }
                }
            },
            CamposConError = new List<string>(),
            Message = "Se encontró 1 activo.",
            DuracionMs = 42,
            Error = (string?)null
        };

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(pluginResponse), System.Text.Encoding.UTF8, "application/json")
        };

        var sut = CreateSut(response);
        var resultado = await sut.Run(CreateInput());

        resultado.Ejecutado.Should().BeTrue();
        resultado.Exitoso.Should().BeTrue();
        resultado.Count.Should().Be(1);
        resultado.Activos.Should().HaveCount(1);
        resultado.Activos[0].IdActivo.Should().Be("100001");
    }

    [Fact]
    public async Task Run_MultipleMatches_ReturnsMultipleActivos()
    {
        var pluginResponse = new
        {
            CorrelationId = "test-corr-001",
            Found = true,
            Count = 3,
            CriteriosUsados = new { Idufir = "12345678901234", ReferenciaCatastral = (string?)null },
            Activos = new[]
            {
                new { IdActivo = "100001", FchCierre = DateTime.UtcNow, CamposSolicitados = new Dictionary<string, object?>() },
                new { IdActivo = "100002", FchCierre = DateTime.UtcNow, CamposSolicitados = new Dictionary<string, object?>() },
                new { IdActivo = "100003", FchCierre = DateTime.UtcNow, CamposSolicitados = new Dictionary<string, object?>() }
            },
            CamposConError = new List<string>(),
            Message = "Se encontraron 3 activos.",
            DuracionMs = 55,
            Error = (string?)null
        };

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(pluginResponse), System.Text.Encoding.UTF8, "application/json")
        };

        var sut = CreateSut(response);
        var resultado = await sut.Run(CreateInput());

        resultado.Ejecutado.Should().BeTrue();
        resultado.Exitoso.Should().BeTrue();
        resultado.Count.Should().Be(3);
        resultado.Activos.Should().HaveCount(3);
    }

    [Fact]
    public async Task Run_NoMatch_ReturnsNotFound()
    {
        var pluginResponse = new
        {
            CorrelationId = "test-corr-001",
            Found = false,
            Count = 0,
            CriteriosUsados = new { Idufir = "99999999999999", ReferenciaCatastral = (string?)null },
            Activos = Array.Empty<object>(),
            CamposConError = new List<string>(),
            Message = "No se encontraron activos.",
            DuracionMs = 10,
            Error = (string?)null
        };

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(pluginResponse), System.Text.Encoding.UTF8, "application/json")
        };

        var sut = CreateSut(response);
        var resultado = await sut.Run(CreateInput());

        resultado.Ejecutado.Should().BeTrue();
        resultado.Exitoso.Should().BeFalse();
        resultado.Count.Should().Be(0);
        resultado.Activos.Should().BeEmpty();
    }

    [Fact]
    public async Task Run_InvalidField_ReturnsCamposConError()
    {
        var pluginResponse = new
        {
            CorrelationId = "test-corr-001",
            Found = true,
            Count = 1,
            CriteriosUsados = new { Idufir = "12345678901234", ReferenciaCatastral = (string?)null },
            Activos = new[]
            {
                new { IdActivo = "100001", FchCierre = DateTime.UtcNow, CamposSolicitados = new Dictionary<string, object?>() }
            },
            CamposConError = new List<string> { "CampoInventado" },
            Message = "Se encontró 1 activo.",
            DuracionMs = 30,
            Error = (string?)null
        };

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(pluginResponse), System.Text.Encoding.UTF8, "application/json")
        };

        var sut = CreateSut(response);
        var resultado = await sut.Run(CreateInput(camposSolicitados: new List<string> { "DesProvnc", "CampoInventado" }));

        resultado.CamposConError.Should().Contain("CampoInventado");
    }

    [Fact]
    public async Task Run_PluginReturnsHttpError_ReturnsError()
    {
        var response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("Internal Server Error")
        };

        var sut = CreateSut(response);
        var resultado = await sut.Run(CreateInput());

        resultado.Ejecutado.Should().BeTrue();
        resultado.Exitoso.Should().BeFalse();
        resultado.Mensaje.Should().Contain("500");
    }

    [Fact]
    public async Task Run_PluginThrowsException_ReturnsError()
    {
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("http://localhost:5006/")
        };

        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(f => f.CreateClient("AssetResolver")).Returns(httpClient);

        var sut = new ObtenerActivoActivity(_loggerMock.Object, factoryMock.Object);
        var resultado = await sut.Run(CreateInput());

        resultado.Ejecutado.Should().BeTrue();
        resultado.Exitoso.Should().BeFalse();
        resultado.Error.Should().Contain("Connection refused");
    }

    [Fact]
    public async Task Run_WithDireccionTipificada_SendsTypedFieldsToPlugin()
    {
        var pluginResponse = new
        {
            CorrelationId = "test-corr-001",
            Found = true,
            Count = 1,
            CriteriosUsados = new { Idufir = "12345678901234", ReferenciaCatastral = (string?)null },
            Activos = new[]
            {
                new { IdActivo = "100001", FchCierre = DateTime.UtcNow, CamposSolicitados = new Dictionary<string, object?>() }
            },
            CamposConError = new List<string>(),
            Message = "ok",
            DuracionMs = 25,
            Error = (string?)null
        };

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(pluginResponse), System.Text.Encoding.UTF8, "application/json")
        };

        var sut = CreateSut(response, req =>
        {
            var raw = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            root.GetProperty("busquedaDireccionTipificadaHabilitada").GetBoolean().Should().BeTrue();
            var dt = root.GetProperty("direccionTipificada");
            dt.GetProperty("municipio").GetString().Should().Be("Azuqueca de Henares");
            dt.GetProperty("calle").GetString().Should().Be("Torrente Ballester");
            dt.GetProperty("numero").GetString().Should().Be("7");
        });

        var input = CreateInput();
        input.BusquedaDireccionTipificadaHabilitada = true;
        input.DireccionTipificada = new DireccionTipificadaInputActivo
        {
            Municipio = "Azuqueca de Henares",
            Calle = "Torrente Ballester",
            Numero = "7"
        };

        var resultado = await sut.Run(input);

        resultado.Exitoso.Should().BeTrue();
        resultado.Count.Should().Be(1);
    }

    [Fact]
    public async Task Run_PluginReturnsDireccionTipificadaCriterio_MapsToOutput()
    {
        var pluginResponse = new
        {
            CorrelationId = "test-corr-001",
            Found = true,
            Count = 1,
            CriteriosUsados = new
            {
                Idufir = (string?)null,
                ReferenciaCatastral = (string?)null,
                ModoCombinacionCriterios = "OR",
                DireccionTipificada = new
                {
                    Pais = "ESPANA",
                    Provincia = "GUADALAJARA",
                    ComunidadAutonoma = "CASTILLA-LA MANCHA",
                    Municipio = "AZUQUECA DE HENARES",
                    Poblacion = "AZUQUECA DE HENARES",
                    TipoVia = "CALLE",
                    Calle = "TORRENTE BALLESTER",
                    Numero = "7",
                    Bloque = (string?)null,
                    Puerta = "4C",
                    CodigoPostal = "19200",
                    Planta = "4",
                    CandidatosEvaluados = 3,
                    Razon = "1 activos encontrados con filtros tipificados"
                }
            },
            Activos = new[]
            {
                new { IdActivo = "2691", FchCierre = DateTime.UtcNow, CamposSolicitados = new Dictionary<string, object?>() }
            },
            CamposConError = new List<string>(),
            Message = "ok",
            DuracionMs = 32,
            Error = (string?)null
        };

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(pluginResponse), System.Text.Encoding.UTF8, "application/json")
        };

        var sut = CreateSut(response);
        var resultado = await sut.Run(CreateInput());

        resultado.CriteriosUsados.DireccionTipificada.Should().NotBeNull();
        resultado.CriteriosUsados.DireccionTipificada!.Municipio.Should().Be("AZUQUECA DE HENARES");
        resultado.CriteriosUsados.DireccionTipificada.CandidatosEvaluados.Should().Be(3);
        resultado.CriteriosUsados.DireccionTipificada.Razon.Should().Contain("tipificados");
    }
}
