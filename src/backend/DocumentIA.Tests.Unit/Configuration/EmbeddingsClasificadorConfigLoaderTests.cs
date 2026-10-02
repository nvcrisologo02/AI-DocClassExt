#nullable enable
using DocumentIA.Core.Configuration;
using DocumentIA.Core.Models;
using DocumentIA.Data.Entities;
using DocumentIA.Data.Repositories;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace DocumentIA.Tests.Unit.Configuration;

public class EmbeddingsClasificadorConfigLoaderTests
{
    private const string JsonValido = """
    {
      "DeploymentName": "text-embedding-3-large-030358",
      "ResourceAlias": "openai_primary",
      "AuthMode": "DefaultAzureCredential",
      "Artefacto": { "Container": "documentai", "BlobPath": "modelos/clasificador-embeddings/v1/clasificador-embeddings-v1.json" },
      "Modo": "sombra",
      "UmbralConfianza": 0.6,
      "Restringido": { "Modo": "sombra", "UmbralMasa": 0.5, "UmbralConfianzaCondicionada": 0.8 },
      "MaxChars": 24000,
      "TimeoutSeconds": 20
    }
    """;

    private static EmbeddingsClasificadorConfigLoader CrearLoader(
        IReadOnlyCollection<ModeloConfigEntity> filas,
        IAiEndpointResolver? resolver = null,
        ILogger<EmbeddingsClasificadorConfigLoader>? logger = null)
    {
        var repo = new Mock<IModeloConfigRepository>();
        repo.Setup(r => r.GetAllActivosByTipoAsync(TipoModelo.Embeddings)).ReturnsAsync(filas);

        var services = new ServiceCollection();
        services.AddSingleton(repo.Object);
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        resolver ??= new AiEndpointResolver(new Dictionary<string, AiResourceEntry>
        {
            ["openai_primary"] = new() { Endpoint = "https://srbaisrv01devdocai.openai.azure.com/" }
        });

        return new EmbeddingsClasificadorConfigLoader(new MemoryCache(new MemoryCacheOptions()), scopeFactory, resolver, logger);
    }

    private static ModeloConfigEntity Fila(string json, bool activo = true, string key = EmbeddingsClasificadorConfig.ClaveRegistro) => new()
    {
        Tipo = TipoModelo.Embeddings,
        Key = key,
        Provider = "azure-openai",
        Activo = activo,
        ConfiguracionJson = json
    };

    [Fact]
    public void Parse_JsonValido_CargaTodosLosCampos()
    {
        var c = EmbeddingsClasificadorConfigLoader.Parse(JsonValido);

        c.DeploymentName.Should().Be("text-embedding-3-large-030358");
        c.ResourceAlias.Should().Be("openai_primary");
        c.AuthMode.Should().Be("DefaultAzureCredential");
        c.Artefacto.Container.Should().Be("documentai");
        c.Artefacto.BlobPath.Should().EndWith("clasificador-embeddings-v1.json");
        c.ModoNormalizado.Should().Be(ModosEmbeddings.Sombra);
        c.UmbralConfianza.Should().Be(0.6);
        c.Restringido.ModoNormalizado.Should().Be(ModosEmbeddings.Sombra);
        c.Restringido.UmbralMasa.Should().Be(0.5);
        c.Restringido.UmbralConfianzaCondicionada.Should().Be(0.8);
        c.MaxChars.Should().Be(24000);
        c.TimeoutSeconds.Should().Be(20);
        c.EstaActiva.Should().BeTrue();
    }

    [Fact]
    public void Parse_ToleraClavesEnMinusculas()
    {
        var c = EmbeddingsClasificadorConfigLoader.Parse("""{"deploymentName":"x","modo":"HIBRIDO","artefacto":{"blobPath":"a/b.json"}}""");

        c.DeploymentName.Should().Be("x");
        c.ModoNormalizado.Should().Be(ModosEmbeddings.Hibrido);
        c.Artefacto.Container.Should().Be("documentai", "valor por defecto");
    }

    [Fact]
    public void Parse_JsonInvalidoONulo_DevuelveOffSinLanzar()
    {
        EmbeddingsClasificadorConfigLoader.Parse("{ no es json").EstaActiva.Should().BeFalse();
        EmbeddingsClasificadorConfigLoader.Parse(null).EstaActiva.Should().BeFalse();
        EmbeddingsClasificadorConfigLoader.Parse("{ no es json").MotivoDesactivacion.Should().Be("json_invalido");
    }

    [Fact]
    public void Parse_ModoDesconocido_EsOff()
    {
        EmbeddingsClasificadorConfigLoader.Parse("""{"Modo":"activo"}""").ModoNormalizado.Should().Be(ModosEmbeddings.Off);
    }

    [Fact]
    public void Load_FilaAusente_DevuelveOff()
    {
        var c = CrearLoader(Array.Empty<ModeloConfigEntity>()).Load();

        c.EstaActiva.Should().BeFalse();
        c.MotivoDesactivacion.Should().Be("fila_ausente");
    }

    [Fact]
    public void Load_FilaInactiva_DevuelveOff()
    {
        var logger = new Mock<ILogger<EmbeddingsClasificadorConfigLoader>>();

        var c = CrearLoader(new[] { Fila(JsonValido, activo: false) }, logger: logger.Object).Load();

        c.EstaActiva.Should().BeFalse();
        c.MotivoDesactivacion.Should().Be("fila_ausente", "una fila inactiva se trata como ausente");
        VerificarAviso(logger);
    }

    [Fact]
    public void Load_FilaConOtraClave_SeIgnora()
    {
        var c = CrearLoader(new[] { Fila(JsonValido, key: "clasificador.otro") }).Load();

        c.EstaActiva.Should().BeFalse();
        c.MotivoDesactivacion.Should().Be("fila_ausente");
    }

    [Fact]
    public void Load_FilaValida_ResuelveElEndpointPorAlias()
    {
        var c = CrearLoader(new[] { Fila(JsonValido) }).Load();

        c.EstaActiva.Should().BeTrue();
        c.Endpoint.Should().Be("https://srbaisrv01devdocai.openai.azure.com/");
    }

    [Fact]
    public void Load_AliasSinMapear_DevuelveOffSinLanzar()
    {
        var c = CrearLoader(new[] { Fila(JsonValido) }, AiEndpointResolver.Empty).Load();

        c.EstaActiva.Should().BeFalse();
        c.MotivoDesactivacion.Should().StartWith("alias_sin_mapear");
    }

    [Fact]
    public void Load_ActivaSinDeploymentOSinArtefacto_DevuelveOff()
    {
        var sinDeployment = CrearLoader(new[] { Fila("""{"Modo":"sombra","ResourceAlias":"openai_primary","Artefacto":{"BlobPath":"a.json"}}""") }).Load();
        var sinArtefacto = CrearLoader(new[] { Fila("""{"Modo":"sombra","ResourceAlias":"openai_primary","DeploymentName":"d"}""") }).Load();

        sinDeployment.EstaActiva.Should().BeFalse();
        sinDeployment.MotivoDesactivacion.Should().Be("configuracion_incompleta");
        sinArtefacto.EstaActiva.Should().BeFalse();
    }

    [Fact]
    public void Load_ModoOffEnAmbos_NoExigeDeploymentNiResuelveAlias()
    {
        // La fila de PRE y PRO entra en off sin artefacto: no debe quejarse de nada.
        var c = CrearLoader(new[] { Fila("""{"Modo":"off","Restringido":{"Modo":"off"}}""") }, AiEndpointResolver.Empty).Load();

        c.EstaActiva.Should().BeFalse();
        c.MotivoDesactivacion.Should().BeNull();
    }

    [Fact]
    public void ToParametros_CopiaModosYUmbrales()
    {
        var c = EmbeddingsClasificadorConfigLoader.Parse(JsonValido);

        var p = c.ToParametros(expectedTypeInformado: true, restriccion: new[] { "a.01" });

        p.Modo.Should().Be(ModosEmbeddings.Sombra);
        p.UmbralConfianza.Should().Be(0.6);
        p.ModoRestringido.Should().Be(ModosEmbeddings.Sombra);
        p.UmbralMasa.Should().Be(0.5);
        p.UmbralConfianzaCondicionada.Should().Be(0.8);
        p.ExpectedTypeInformado.Should().BeTrue();
        p.EsRestringida.Should().BeTrue();
    }

    private static void VerificarAviso(Mock<ILogger<EmbeddingsClasificadorConfigLoader>> logger) =>
        logger.Verify(l => l.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);

    [Fact]
    public void Load_FilaAusente_RegistraAviso()
    {
        var logger = new Mock<ILogger<EmbeddingsClasificadorConfigLoader>>();

        CrearLoader(Array.Empty<ModeloConfigEntity>(), logger: logger.Object).Load();

        VerificarAviso(logger);
    }

    [Fact]
    public void Load_JsonInvalido_RegistraAviso()
    {
        var logger = new Mock<ILogger<EmbeddingsClasificadorConfigLoader>>();

        var c = CrearLoader(new[] { Fila("{ no es json") }, logger: logger.Object).Load();

        c.MotivoDesactivacion.Should().Be("json_invalido");
        VerificarAviso(logger);
    }
}
