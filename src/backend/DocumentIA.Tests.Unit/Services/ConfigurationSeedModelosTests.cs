#nullable enable
using System.Text.Json;
using System.Text.Json.Nodes;
using DocumentIA.Data.Context;
using DocumentIA.Data.Entities;
using DocumentIA.Functions.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace DocumentIA.Tests.Unit.Services;

/// <summary>
/// Cubre el merge de seeds de ModeloConfigs (ConfigurationSeedService.SeedModelosAsync) frente al
/// cutover a recursos de IA propios por entorno (AB#100317): el seed rellena cualquier propiedad
/// ausente o vacia de la fila, asi que un seed con Endpoint volveria a inyectar ese host en toda fila
/// a la que el cutover se lo haya quitado. Los seeds del repositorio solo llevan ResourceAlias y un
/// test guardian vigila que siga siendo asi.
/// </summary>
public class ConfigurationSeedModelosTests : IDisposable
{
    private static readonly string[] SeedDirectories = ["classification", "extraction", "layout", "prompt"];

    private readonly string _configRoot = Path.Combine(Path.GetTempPath(), "documentia-seed-" + Guid.NewGuid().ToString("N"));
    private readonly DocumentIADbContext _context;
    private readonly ILogger _logger = new Mock<ILogger>().Object;

    public ConfigurationSeedModelosTests()
    {
        var options = new DbContextOptionsBuilder<DocumentIADbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new DocumentIADbContext(options);
        Directory.CreateDirectory(_configRoot);
    }

    public void Dispose()
    {
        _context.Dispose();
        if (Directory.Exists(_configRoot))
        {
            Directory.Delete(_configRoot, recursive: true);
        }
    }

    [Fact]
    public async Task SeedAsync_SeedSinEndpointYFilaConAliasSinEndpoint_NoIntroduceNingunHost()
    {
        WriteExtractionSeed("""{"Key":"m.cu","Provider":"azure-content-understanding","ResourceAlias":"cu_primary","AnalyzerId":"A1"}""");
        AddModelo("m.cu", """{"ResourceAlias":"cu_primary","AnalyzerId":"A1"}""");

        await ConfigurationSeedService.SeedAsync(_context, _logger, _configRoot);

        var config = ReadConfig("m.cu");
        EndpointOf(config).Should().BeNullOrEmpty();
        config["ResourceAlias"]!.GetValue<string>().Should().Be("cu_primary");
    }

    [Fact]
    public async Task SeedAsync_FilaConEndpointExplicito_LoConservaAunqueElSeedNoLoLleve()
    {
        WriteExtractionSeed("""{"Key":"m.cu","Provider":"azure-content-understanding","ResourceAlias":"cu_primary","AnalyzerId":"A1"}""");
        AddModelo("m.cu", """{"Endpoint":"https://explicito.example/","ResourceAlias":"cu_primary","AnalyzerId":"A1"}""");

        await ConfigurationSeedService.SeedAsync(_context, _logger, _configRoot);

        EndpointOf(ReadConfig("m.cu")).Should().Be("https://explicito.example/");
    }

    [Fact]
    public async Task SeedAsync_SeedConEndpoint_LoReinyectaEnLaFilaQueLoTieneVacio()
    {
        // Documenta la trampa que motiva el test guardian: si un seed vuelve a llevar Endpoint, el
        // cutover (que deja la propiedad vacia) se deshace en el siguiente arranque.
        WriteExtractionSeed("""{"Key":"m.cu","Provider":"azure-content-understanding","Endpoint":"https://pro.example/","ResourceAlias":"cu_primary","AnalyzerId":"A1"}""");
        AddModelo("m.cu", """{"Endpoint":"","ResourceAlias":"cu_primary","AnalyzerId":"A1"}""");

        await ConfigurationSeedService.SeedAsync(_context, _logger, _configRoot);

        EndpointOf(ReadConfig("m.cu")).Should().Be("https://pro.example/");
    }

    [Theory]
    [InlineData("classification")]
    [InlineData("extraction")]
    [InlineData("layout")]
    [InlineData("prompt")]
    public void SeedsDelRepositorio_SoloLlevanResourceAliasSinEndpoint(string directory)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "config", directory, "models.json");
        File.Exists(path).Should().BeTrue($"el seed {directory}/models.json debe copiarse a la salida de tests");

        var models = (JsonNode.Parse(File.ReadAllText(path)) as JsonObject)?["Models"] as JsonArray;
        models.Should().NotBeNullOrEmpty();

        foreach (var model in models!.OfType<JsonObject>())
        {
            var key = model["Key"]?.GetValue<string>() ?? "<sin Key>";
            EndpointOf(model).Should().BeNullOrEmpty(
                $"el modelo '{key}' de {directory}/models.json no debe fijar Endpoint: el seed lo reinyectaria en cada arranque y anularia el cutover por alias");
            model["ResourceAlias"]?.GetValue<string>().Should().NotBeNullOrWhiteSpace(
                $"el modelo '{key}' de {directory}/models.json debe resolver su endpoint por ResourceAlias");
        }
    }

    [Fact]
    public void SeedsDelRepositorio_CubrenLosCuatroRegistros()
    {
        foreach (var directory in SeedDirectories)
        {
            File.Exists(Path.Combine(AppContext.BaseDirectory, "config", directory, "models.json")).Should().BeTrue(directory);
        }
    }

    private void WriteExtractionSeed(string modelJson)
    {
        var directory = Path.Combine(_configRoot, "extraction");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "models.json"), $$"""{"Models":[{{modelJson}}]}""");
    }

    private void AddModelo(string key, string configuracionJson)
    {
        _context.ModeloConfigs.Add(new ModeloConfigEntity
        {
            Tipo = TipoModelo.Extraccion,
            Key = key,
            Provider = "azure-content-understanding",
            Activo = true,
            ConfiguracionJson = configuracionJson,
            CreadoPor = "test",
            FechaCreacion = DateTime.UtcNow
        });
        _context.SaveChanges();
    }

    private JsonObject ReadConfig(string key)
    {
        var entity = _context.ModeloConfigs.AsNoTracking().Single(m => m.Key == key);
        return JsonNode.Parse(entity.ConfiguracionJson) as JsonObject ?? throw new JsonException("ConfiguracionJson no es un objeto");
    }

    private static string? EndpointOf(JsonObject config)
    {
        foreach (var property in config)
        {
            if (string.Equals(property.Key, "Endpoint", StringComparison.OrdinalIgnoreCase))
            {
                return property.Value?.GetValue<string>();
            }
        }

        return null;
    }
}
