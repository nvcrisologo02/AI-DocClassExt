#nullable enable
using DocumentIA.Core.Configuration;
using DocumentIA.Core.Models;
using DocumentIA.Core.Services;
using DocumentIA.Functions.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DocumentIA.Tests.Unit.Services;

/// <summary>
/// El componente "modelo de clasificacion" de la clave es el que el registro resolveria ahora
/// para la peticion, no el que acabe usandose: la clave se calcula antes de clasificar (AB#100930).
/// </summary>
public class ClaveIntencionResolverTests : IDisposable
{
    private readonly string _registroPath;

    public ClaveIntencionResolverTests()
    {
        _registroPath = Path.Combine(Path.GetTempPath(), $"clave-intencion-{Guid.NewGuid():N}.json");
        File.WriteAllText(_registroPath, """
        {"models":[
          {"key":"gpt.default","provider":"azure-openai","isDefault":true,"useAsFallback":true,"endpoint":"https://x","apiKey":"k","deploymentName":"gpt-4o-mini"},
          {"key":"gpt.mini5","provider":"azure-openai","endpoint":"https://x","apiKey":"k","deploymentName":"gpt-5-mini"},
          {"key":"di.cc","provider":"azure-document-intelligence","isDefault":true,"endpoint":"https://x","apiKey":"k","classifierId":"DocumentAICC_v1"}
        ]}
        """);
    }

    public void Dispose() => File.Delete(_registroPath);

    [Fact]
    public void Resolver_FlujoPorDefectoHibrido_UsaElDeploymentGptDeFallback()
    {
        var clave = Sut(DefaultSettings()).Resolver(new Instrucciones { ClassificationOnly = true });

        clave.Componentes.ModeloClasificacion.Should().Be("gpt-4o-mini");
        clave.Componentes.Modo.Should().Be(ModosIntencion.SoloClasificacion);
        clave.Componentes.Nivel.Should().Be("TDN1_TDN2");
        clave.Componentes.Universo.Should().Be("*");
        clave.Clave.Should().HaveLength(32);
    }

    [Fact]
    public void Resolver_ModeloExplicito_UsaEseModelo()
    {
        var instrucciones = new Instrucciones();
        instrucciones.Classification.Provider = "gpt";
        instrucciones.Classification.Model = "gpt.mini5";

        var clave = Sut(DefaultSettings()).Resolver(instrucciones);

        clave.Componentes.ModeloClasificacion.Should().Be("gpt-5-mini");
    }

    [Fact]
    public void Resolver_ProveedorDi_UsaElClassifierId()
    {
        var instrucciones = new Instrucciones();
        instrucciones.Classification.Provider = "azure-document-intelligence";

        var clave = Sut(DefaultSettings()).Resolver(instrucciones);

        clave.Componentes.ModeloClasificacion.Should().Be("documentaicc_v1");
    }

    [Fact]
    public void Resolver_ConExpectedType_NoConsultaElRegistro()
    {
        var instrucciones = new Instrucciones { ExpectedType = "nota.simple" };

        var clave = Sut(DefaultSettings()).Resolver(instrucciones);

        clave.Componentes.ModeloClasificacion.Should().Be(ClaveIntencionBuilder.ModeloExpectedType);
        clave.Componentes.ExpectedType.Should().Be("NOTA.SIMPLE");
    }

    [Fact]
    public void Resolver_RestriccionYNivel_EntranEnLaClave()
    {
        var instrucciones = new Instrucciones
        {
            RestriccionTipologias = new RestriccionTipologias { Codigos = new List<string> { "tasacion", "nota.simple" } }
        };
        instrucciones.Classification.NivelClasificacion = "TDN1";

        var clave = Sut(DefaultSettings()).Resolver(instrucciones);

        clave.Componentes.Universo.Should().Be("NOTA.SIMPLE,TASACION");
        clave.Componentes.Nivel.Should().Be("TDN1");
    }

    [Fact]
    public void Resolver_ModeloInexistente_DejaMarcadorEstable()
    {
        var instrucciones = new Instrucciones();
        instrucciones.Classification.Provider = "gpt";
        instrucciones.Classification.Model = "no.existe";

        var clave = Sut(DefaultSettings()).Resolver(instrucciones);

        clave.Componentes.ModeloClasificacion.Should().Be(ClaveIntencionResolver.ModeloNoResuelto);
    }

    private ClaveIntencionResolver Sut(ClassificationRoutingSettings settings) => new(
        Options.Create(settings),
        new ClassificationModelRegistryLoader(_registroPath),
        NullLogger<ClaveIntencionResolver>.Instance);

    private static ClassificationRoutingSettings DefaultSettings() => new()
    {
        DefaultProvider = "hybrid-tdn",
        DefaultFlow = "hybrid-tdn",
        NivelClasificacionDefault = "TDN1_TDN2",
        Flows = new Dictionary<string, ClassificationFlowSettings>(StringComparer.OrdinalIgnoreCase)
        {
            ["hybrid-tdn"] = new() { Providers = new List<string> { "hybrid-tdn" } }
        }
    };
}
