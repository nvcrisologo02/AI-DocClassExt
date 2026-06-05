#nullable enable
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using DocumentIA.Core.Configuration;
using DocumentIA.Core.Mappers;
using DocumentIA.Data.Context;
using DocumentIA.Data.Entities;
using DocumentIA.Data.Repositories;
using DocumentIA.Functions.Triggers.Admin;
using FluentAssertions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DocumentIA.Tests.Unit.Triggers.Admin;

// ── HTTP Test Doubles ────────────────────────────────────────────────────────

/// <summary>HttpResponseData concreta para pruebas: soporta WriteAsJsonAsync.</summary>
internal sealed class TestHttpResponseData : HttpResponseData
{
    public TestHttpResponseData(FunctionContext ctx) : base(ctx) { }

    public override HttpStatusCode StatusCode { get; set; }

    private HttpHeadersCollection _headers = new HttpHeadersCollection();
    public override HttpHeadersCollection Headers { get => _headers; set => _headers = value; }

    private Stream _body = new MemoryStream();
    public override Stream Body { get => _body; set => _body = value; }

    public override HttpCookies Cookies => throw new NotImplementedException("Cookies not used in tests");
}

/// <summary>HttpRequestData concreto para pruebas: expone Body como stream JSON.</summary>
internal sealed class TestHttpRequestData : HttpRequestData
{
    private readonly Stream _body;
    private readonly FunctionContext _ctx;

    public TestHttpRequestData(FunctionContext ctx, string jsonBody = "{}") : base(ctx)
    {
        _ctx = ctx;
        _body = new MemoryStream(Encoding.UTF8.GetBytes(jsonBody));
    }

    public override Stream Body => _body;
    public override HttpHeadersCollection Headers { get; } = new HttpHeadersCollection();
    public override IReadOnlyCollection<IHttpCookie> Cookies { get; } = Array.Empty<IHttpCookie>();
    public override Uri Url => new Uri("http://localhost/api/management/tipologias");
    public override IEnumerable<ClaimsIdentity> Identities => Array.Empty<ClaimsIdentity>();
    public override string Method => "GET";
    public override HttpResponseData CreateResponse() => new TestHttpResponseData(_ctx);
}

// ── Test class ───────────────────────────────────────────────────────────────

/// <summary>
/// Tests T-6: Pruebas unitarias de TipologiasAdminFunction CRUD.
/// Usa InMemory DbContext para consultas directas y mocks para ITipologiaRepository.
/// </summary>
public class TipologiasAdminFunctionTests : IDisposable
{
    private readonly DocumentIADbContext _dbContext;
    private readonly Mock<ITipologiaRepository> _repoMock;
    private readonly Mock<ITipologiaConfigAuditRepository> _auditRepoMock;
    private readonly TipologiasAdminFunction _sut;
    private readonly FunctionContext _fakeCtx;

    public TipologiasAdminFunctionTests()
    {
        var options = new DbContextOptionsBuilder<DocumentIADbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new DocumentIADbContext(options);
        _repoMock = new Mock<ITipologiaRepository>();
        _auditRepoMock = new Mock<ITipologiaConfigAuditRepository>();

        _sut = new TipologiasAdminFunction(
            _dbContext,
            _repoMock.Object,
            _auditRepoMock.Object,
            new Mock<ILogger<TipologiasAdminFunction>>().Object,
            new MemoryCache(new MemoryCacheOptions()),
            Mock.Of<TipologiaMapper>());

        var mockCtx = new Mock<FunctionContext>();
        var services = new ServiceCollection();
        services.AddOptions();
        services.Configure<WorkerOptions>(opts =>
            opts.Serializer = new Azure.Core.Serialization.JsonObjectSerializer());
        mockCtx.Setup(c => c.InstanceServices).Returns(services.BuildServiceProvider());
        _fakeCtx = mockCtx.Object;
    }

    public void Dispose() => _dbContext.Dispose();

    // ── Helpers ──────────────────────────────────────────────────────────────

    private TestHttpRequestData MakeRequest(string jsonBody = "{}")
        => new TestHttpRequestData(_fakeCtx, jsonBody);

    private static string BuildValidConfigJson(string tipologiaId, string version) =>
        JsonSerializer.Serialize(new
        {
            tipologiaId,
            version,
            skipGDCUpload = true,
            extraction = new { enabled = false },
        });

    private static string BuildValidPayload(
        string codigo = "nota.simple",
        string nombre = "Nota Simple",
        string version = "1.0",
        string? usuario = "test-user",
        string? configJson = null)
    {
        var cfg = configJson ?? BuildValidConfigJson(codigo, version);
        return JsonSerializer.Serialize(new
        {
            codigo,
            nombre,
            version,
            usuario,
            configuracionJson = cfg
        });
    }

    // ── GetTipologias ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTipologias_CuandoExistenEntidades_Devuelve200ConLista()
    {
        _dbContext.Tipologias.AddRange(
            new TipologiaEntity { Codigo = "tipo-a", Nombre = "Tipo A", FechaCreacion = DateTime.UtcNow },
            new TipologiaEntity { Codigo = "tipo-b", Nombre = "Tipo B", FechaCreacion = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();

        var req = MakeRequest();
        var response = await _sut.GetTipologias(req);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetTipologias_CuandoNoHayEntidades_Devuelve200ListaVacia()
    {
        var req = MakeRequest();
        var response = await _sut.GetTipologias(req);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── GetTipologiaById ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetTipologiaById_CuandoNoExisteId_Devuelve404()
    {
        var req = MakeRequest();
        var response = await _sut.GetTipologiaById(req, id: 9999);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetTipologiaById_CuandoExiste_Devuelve200()
    {
        var entity = new TipologiaEntity
        {
            Codigo = "nota.simple",
            Nombre = "Nota Simple",
            FechaCreacion = DateTime.UtcNow
        };
        _dbContext.Tipologias.Add(entity);
        await _dbContext.SaveChangesAsync();

        var req = MakeRequest();
        var response = await _sut.GetTipologiaById(req, entity.Id);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── CreateTipologia ──────────────────────────────────────────────────────

    [Fact]
    public async Task CreateTipologia_PayloadValido_LlamaAddAsyncYDevuelve201()
    {
        var body = BuildValidPayload(codigo: "nota.simple", nombre: "Nota Simple", version: "1.0");
        var req = MakeRequest(body);

        var response = await _sut.CreateTipologia(req);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        _repoMock.Verify(r => r.AddAsync(It.IsAny<TipologiaEntity>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task CreateTipologia_CodigoYaExiste_Devuelve409()
    {
        _dbContext.Tipologias.Add(new TipologiaEntity
        {
            Codigo = "nota.simple",
            Nombre = "Nota Simple",
            FechaCreacion = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        var body = BuildValidPayload(codigo: "nota.simple");
        var req = MakeRequest(body);

        var response = await _sut.CreateTipologia(req);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        _repoMock.Verify(r => r.AddAsync(It.IsAny<TipologiaEntity>(), It.IsAny<string>()), Times.Never);
    }

    // ── UpdateTipologia ──────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateTipologia_CuandoEstadoPublished_Devuelve409()
    {
        var entity = new TipologiaEntity
        {
            Codigo = "nota.simple",
            Nombre = "Nota Simple",
            Version = "1.0",
            Estado = EstadoTipologia.Published,
            FechaCreacion = DateTime.UtcNow
        };
        _dbContext.Tipologias.Add(entity);
        await _dbContext.SaveChangesAsync();

        var body = BuildValidPayload(codigo: "nota.simple");
        var req = MakeRequest(body);

        var response = await _sut.UpdateTipologia(req, entity.Id);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        _repoMock.Verify(r => r.UpdateAsync(It.IsAny<TipologiaEntity>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task UpdateTipologia_CuandoIdNoExiste_Devuelve404()
    {
        var req = MakeRequest(BuildValidPayload());
        var response = await _sut.UpdateTipologia(req, id: 9999);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── RetirarTipologia ─────────────────────────────────────────────────────

    [Fact]
    public async Task RetirarTipologia_CuandoNoExiste_Devuelve404()
    {
        var req = MakeRequest("{}");
        var response = await _sut.RetirarTipologia(req, id: 9999);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
