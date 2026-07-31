using Xunit;
using FluentAssertions;
using Moq;
using Azure.Core.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using DocumentIA.Data.Context;
using DocumentIA.Data.Entities;
using DocumentIA.Functions.Services;
using DocumentIA.Functions.Triggers.Admin;

namespace DocumentIA.Functions.Tests.Triggers.Admin;

/// <summary>
/// Tests for ModelosAdminFunction - Admin API CRUD operations on ModeloConfigs, con foco en el
/// enmascarado de secretos de ConfiguracionJson (AB#99981).
/// </summary>
public class ModelosAdminFunctionTests : IDisposable
{
    private readonly DocumentIADbContext _dbContext;
    private readonly ModelosAdminFunction _function;

    public ModelosAdminFunctionTests()
    {
        var options = new DbContextOptionsBuilder<DocumentIADbContext>()
            .UseInMemoryDatabase(databaseName: $"ModelosAdminTests_{Guid.NewGuid()}")
            .Options;

        _dbContext = new DocumentIADbContext(options);
        _function = new ModelosAdminFunction(_dbContext);
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    // ==================== Helper Methods ====================

    private HttpRequestData CreateMockHttpRequest(string method, string? body = null)
    {
        var services = new ServiceCollection();
        services.AddOptions();
        services.Configure<WorkerOptions>(options =>
        {
            options.Serializer = new JsonObjectSerializer(new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        });
        var serviceProvider = services.BuildServiceProvider();

        var mockFunctionContext = new Mock<FunctionContext>();
        mockFunctionContext.Setup(c => c.InstanceServices).Returns(serviceProvider);

        return new FakeHttpRequestData(mockFunctionContext.Object, method, body);
    }

    private async Task<(HttpStatusCode status, T? data)> ExecuteAndDeserialize<T>(Func<Task<HttpResponseData>> action)
    {
        var response = await action();
        var status = response.StatusCode;

        response.Body.Position = 0;
        using var reader = new StreamReader(response.Body);
        var json = await reader.ReadToEndAsync();

        var data = string.IsNullOrEmpty(json) ? default : JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return (status, data);
    }

    private sealed class ModeloConfigDto
    {
        public int Id { get; set; }
        public int Tipo { get; set; }
        public string Key { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;
        public bool Activo { get; set; } = true;
        public string ConfiguracionJson { get; set; } = "{}";
        public string? CreadoPor { get; set; }
    }

    // ==================== Tests: GET Admin_GetModelosByTipo ====================

    [Fact]
    public async Task Admin_GetModelosByTipo_MasksApiKey_KeepsOtherFieldsIntact()
    {
        // Arrange
        _dbContext.ModeloConfigs.Add(new ModeloConfigEntity
        {
            Tipo = TipoModelo.Clasificacion,
            Key = "gpt-clasificacion",
            Provider = "OpenAI",
            Activo = true,
            ConfiguracionJson = "{\"Endpoint\":\"https://example.com\",\"ApiKey\":\"sk-real-secret\",\"Modelo\":\"gpt-4\"}",
            CreadoPor = "admin",
            FechaCreacion = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        var request = CreateMockHttpRequest("GET");

        // Act
        var (status, data) = await ExecuteAndDeserialize<List<ModeloConfigDto>>(
            () => _function.GetModelosByTipo(request, "clasificacion"));

        // Assert
        status.Should().Be(HttpStatusCode.OK);
        data.Should().NotBeNull();
        data!.Should().HaveCount(1);

        var configJson = JsonNode.Parse(data[0].ConfiguracionJson)!;
        configJson["ApiKey"]!.GetValue<string>().Should().Be(ModelConfigSecretMasker.Mask);
        configJson["Endpoint"]!.GetValue<string>().Should().Be("https://example.com");
        configJson["Modelo"]!.GetValue<string>().Should().Be("gpt-4");
        data[0].Key.Should().Be("gpt-clasificacion");
        data[0].Provider.Should().Be("OpenAI");
    }

    // ==================== Tests: PUT Admin_UpdateModelo ====================

    [Fact]
    public async Task Admin_UpdateModelo_ApiKeyMasked_PreservesStoredApiKey()
    {
        // Arrange
        var entity = new ModeloConfigEntity
        {
            Tipo = TipoModelo.Clasificacion,
            Key = "gpt-clasificacion",
            Provider = "OpenAI",
            Activo = true,
            ConfiguracionJson = "{\"Endpoint\":\"https://example.com\",\"ApiKey\":\"sk-real-secret\"}",
            CreadoPor = "admin",
            FechaCreacion = DateTime.UtcNow
        };
        _dbContext.ModeloConfigs.Add(entity);
        await _dbContext.SaveChangesAsync();

        var payload = new
        {
            Tipo = "clasificacion",
            Key = "gpt-clasificacion",
            Provider = "OpenAI",
            Activo = true,
            ConfiguracionJson = "{\"Endpoint\":\"https://example.com/updated\",\"ApiKey\":\"***\"}"
        };
        var request = CreateMockHttpRequest("PUT", JsonSerializer.Serialize(payload));

        // Act
        var response = await _function.UpdateModelo(request, entity.Id);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var dbEntity = await _dbContext.ModeloConfigs.AsNoTracking().FirstAsync(m => m.Id == entity.Id);
        var storedJson = JsonNode.Parse(dbEntity.ConfiguracionJson)!;
        storedJson["ApiKey"]!.GetValue<string>().Should().Be("sk-real-secret");
        storedJson["Endpoint"]!.GetValue<string>().Should().Be("https://example.com/updated");
    }

    [Fact]
    public async Task Admin_UpdateModelo_ApiKeyReplaced_UpdatesStoredApiKey()
    {
        // Arrange
        var entity = new ModeloConfigEntity
        {
            Tipo = TipoModelo.Clasificacion,
            Key = "gpt-clasificacion",
            Provider = "OpenAI",
            Activo = true,
            ConfiguracionJson = "{\"Endpoint\":\"https://example.com\",\"ApiKey\":\"sk-real-secret\"}",
            CreadoPor = "admin",
            FechaCreacion = DateTime.UtcNow
        };
        _dbContext.ModeloConfigs.Add(entity);
        await _dbContext.SaveChangesAsync();

        var payload = new
        {
            Tipo = "clasificacion",
            Key = "gpt-clasificacion",
            Provider = "OpenAI",
            Activo = true,
            ConfiguracionJson = "{\"Endpoint\":\"https://example.com\",\"ApiKey\":\"nueva-clave\"}"
        };
        var request = CreateMockHttpRequest("PUT", JsonSerializer.Serialize(payload));

        // Act
        var response = await _function.UpdateModelo(request, entity.Id);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var dbEntity = await _dbContext.ModeloConfigs.AsNoTracking().FirstAsync(m => m.Id == entity.Id);
        var storedJson = JsonNode.Parse(dbEntity.ConfiguracionJson)!;
        storedJson["ApiKey"]!.GetValue<string>().Should().Be("nueva-clave");
    }
}

/// <summary>
/// Tests unitarios directos de ModelConfigSecretMasker.
/// </summary>
public class ModelConfigSecretMaskerTests
{
    [Theory]
    [InlineData("ApiKey", true)]
    [InlineData("apikey", true)]
    [InlineData("OpenAiApiKey", true)]
    [InlineData("Password", true)]
    [InlineData("dbPassword", true)]
    [InlineData("Secret", true)]
    [InlineData("ClientSecret", true)]
    [InlineData("AccountKey", true)]
    [InlineData("StorageAccountKey", true)]
    [InlineData("Endpoint", false)]
    [InlineData("Modelo", false)]
    public void IsSensitiveKey_DetectsExpectedFragments(string name, bool expected)
    {
        ModelConfigSecretMasker.IsSensitiveKey(name).Should().Be(expected);
    }

    [Fact]
    public void MaskJson_NestedObject_MasksSensitiveValuesRecursively()
    {
        var json = """
        {
            "Endpoint": "https://example.com",
            "Auth": {
                "ApiKey": "sk-real-secret",
                "Region": "westeurope"
            }
        }
        """;

        var masked = ModelConfigSecretMasker.MaskJson(json);

        var node = JsonNode.Parse(masked!)!;
        node["Endpoint"]!.GetValue<string>().Should().Be("https://example.com");
        node["Auth"]!["ApiKey"]!.GetValue<string>().Should().Be(ModelConfigSecretMasker.Mask);
        node["Auth"]!["Region"]!.GetValue<string>().Should().Be("westeurope");
    }

    [Fact]
    public void MaskJson_ArrayOfObjects_MasksSensitiveValuesInEachElement()
    {
        var json = """
        {
            "Providers": [
                { "Name": "openai", "ApiKey": "sk-1" },
                { "Name": "azure", "ApiKey": "sk-2" }
            ]
        }
        """;

        var masked = ModelConfigSecretMasker.MaskJson(json);

        var node = JsonNode.Parse(masked!)!;
        var providers = node["Providers"]!.AsArray();
        providers[0]!["Name"]!.GetValue<string>().Should().Be("openai");
        providers[0]!["ApiKey"]!.GetValue<string>().Should().Be(ModelConfigSecretMasker.Mask);
        providers[1]!["Name"]!.GetValue<string>().Should().Be("azure");
        providers[1]!["ApiKey"]!.GetValue<string>().Should().Be(ModelConfigSecretMasker.Mask);
    }

    [Theory]
    [InlineData("not a json")]
    [InlineData("")]
    [InlineData(null)]
    public void MaskJson_InvalidOrEmptyJson_ReturnsUnchanged(string? json)
    {
        var result = ModelConfigSecretMasker.MaskJson(json);
        result.Should().Be(json);
    }

    [Fact]
    public void UnmaskJson_MaskedNestedValue_RestoresFromStoredSamePath()
    {
        var stored = """
        {
            "Endpoint": "https://old.example.com",
            "Auth": { "ApiKey": "sk-real-secret" }
        }
        """;
        var incoming = """
        {
            "Endpoint": "https://new.example.com",
            "Auth": { "ApiKey": "***" }
        }
        """;

        var result = ModelConfigSecretMasker.UnmaskJson(incoming, stored);

        var node = JsonNode.Parse(result!)!;
        node["Endpoint"]!.GetValue<string>().Should().Be("https://new.example.com");
        node["Auth"]!["ApiKey"]!.GetValue<string>().Should().Be("sk-real-secret");
    }

    [Fact]
    public void UnmaskJson_MaskedValueWithoutStoredCounterpart_LeavesMaskAsIs()
    {
        var incoming = """{ "ApiKey": "***" }""";

        var result = ModelConfigSecretMasker.UnmaskJson(incoming, storedJson: null);

        var node = JsonNode.Parse(result!)!;
        node["ApiKey"]!.GetValue<string>().Should().Be(ModelConfigSecretMasker.Mask);
    }

    [Theory]
    [InlineData("not a json")]
    [InlineData("")]
    [InlineData(null)]
    public void UnmaskJson_InvalidIncomingJson_ReturnsUnchanged(string? incoming)
    {
        var result = ModelConfigSecretMasker.UnmaskJson(incoming, storedJson: "{}");
        result.Should().Be(incoming);
    }
}
