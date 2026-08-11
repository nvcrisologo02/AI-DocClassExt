#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DocumentIA.Core.Configuration;
using DocumentIA.Core.Models;
using DocumentIA.Functions.Services;
using DocumentIA.Functions.Services.Classification;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace DocumentIA.Tests.Unit.Services.Classification;

public class ConfigurableClasificarDataProviderRestriccionTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _registryPath;

    public ConfigurableClasificarDataProviderRestriccionTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"ConfigurableClasificarRestriccionTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDirectory);
        _registryPath = Path.Combine(_tempDirectory, "models.json");
        File.WriteAllText(_registryPath, @"{
            ""models"": [
                {
                    ""key"": ""classification.gpt4o-mini-fallback"",
                    ""provider"": ""azure-openai"",
                    ""useAsFallback"": true,
                    ""endpoint"": ""https://fake-openai.openai.azure.com/"",
                    ""apiKey"": ""fake-api-key"",
                    ""authMode"": ""ApiKey"",
                    ""deploymentName"": ""gpt-4o-mini-test"",
                    ""timeoutSeconds"": 30,
                    ""maxTokens"": 150
                }
            ]
        }");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }

    private static ClasificacionInput BuildInput(
        string provider,
        string[]? codigosPermitidos = null,
        bool proponerSiDesconocido = false,
        string[]? codigosIgnorados = null)
    {
        return new ClasificacionInput
        {
            Entrada = new ContratoEntrada
            {
                Documento = new Documento
                {
                    Name = "test-document.pdf",
                    Content = new ContenidoDocumento { Base64 = "dGVzdA==" }
                },
                Instrucciones = new Instrucciones
                {
                    Classification = new ConfiguracionIA { Provider = provider },
                    RestriccionTipologias = codigosPermitidos is null
                        ? null
                        : new RestriccionTipologias
                        {
                            Codigos = new List<string>(codigosPermitidos),
                            ProponerSiDesconocido = proponerSiDesconocido,
                            CodigosIgnorados = codigosIgnorados is null ? null : new List<string>(codigosIgnorados)
                        }
                }
            }
        };
    }

    private TestableProvider CreateSut(
        Func<string, ClasificacionInput, ResultadoClasificacion> behavior,
        bool useGlobalFallback = false)
    {
        return new TestableProvider(
            _registryPath,
            new ClassificationRoutingSettings
            {
                UseGlobalFallback = useGlobalFallback,
                GlobalFallbackProvider = "gpt"
            },
            behavior);
    }

    [Fact]
    public async Task Resultado_DentroDelConjunto_SeDevuelveConEcoDeRestriccion()
    {
        var sut = CreateSut((provider, _) => new ResultadoClasificacion
        {
            ProveedorClasif = provider,
            TipologiaDetectada = "SERE-25",
            Confianza = 0.9
        });

        var resultado = await sut.ClasificarAsync(
            BuildInput("gpt", new[] { "SERE-25", "nota-simple" }, codigosIgnorados: new[] { "SERE-99" }),
            CancellationToken.None);

        resultado.TipologiaDetectada.Should().Be("SERE-25");
        resultado.RestriccionTipologias.Should().NotBeNull();
        resultado.RestriccionTipologias!.Codigos.Should().BeEquivalentTo("SERE-25", "nota-simple");
        resultado.RestriccionTipologias.CodigosIgnorados.Should().BeEquivalentTo(new[] { "SERE-99" });
    }

    [Fact]
    public async Task Resultado_FueraDelConjunto_DevuelveDesconocidoSinPropuesta()
    {
        var sut = CreateSut((provider, _) => new ResultadoClasificacion
        {
            ProveedorClasif = provider,
            TipologiaDetectada = "ESCR-01",
            Confianza = 0.95
        });

        var resultado = await sut.ClasificarAsync(
            BuildInput("gpt", new[] { "SERE-25" }),
            CancellationToken.None);

        resultado.TipologiaDetectada.Should().Be("Desconocido");
        resultado.Confianza.Should().Be(0.0);
        resultado.FallbackRazon.Should().Be(RestriccionTipologiasMotivos.FueraDeConjunto);
        resultado.PropuestaTipologia.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task Resultado_FueraDelConjuntoConProponer_ReutilizaCandidatoDeLaCadenaSinLlamadaExtra()
    {
        var llamadasGpt = 0;
        var sut = CreateSut((provider, input) =>
        {
            if (input.OmitirRestriccionTipologias)
            {
                llamadasGpt++;
                return new ResultadoClasificacion { ProveedorClasif = provider, TipologiaDetectada = "NO-DEBE-USARSE", Confianza = 0.9 };
            }

            return new ResultadoClasificacion { ProveedorClasif = provider, TipologiaDetectada = "ESCR-01", Confianza = 0.95 };
        });

        var resultado = await sut.ClasificarAsync(
            BuildInput("gpt", new[] { "SERE-25" }, proponerSiDesconocido: true),
            CancellationToken.None);

        resultado.TipologiaDetectada.Should().Be("Desconocido");
        resultado.PropuestaTipologia.Should().Be("ESCR-01");
        llamadasGpt.Should().Be(0, "el candidato descartado de la cadena ya sirve como propuesta");
    }

    [Fact]
    public async Task Resultado_DesconocidoConProponer_EjecutaPasadaLibreYAnotaDetalle()
    {
        var sut = CreateSut((provider, input) =>
        {
            if (input.OmitirRestriccionTipologias)
            {
                return new ResultadoClasificacion { ProveedorClasif = provider, TipologiaDetectada = "ESCR-01", Confianza = 0.88 };
            }

            // El provider restringido no encuentra nada en el conjunto.
            return new ResultadoClasificacion
            {
                ProveedorClasif = provider,
                TipologiaDetectada = "Desconocido",
                Confianza = 0.0,
                FallbackRazon = RestriccionTipologiasMotivos.FueraDeConjunto
            };
        });

        var resultado = await sut.ClasificarAsync(
            BuildInput("gpt", new[] { "SERE-25" }, proponerSiDesconocido: true),
            CancellationToken.None);

        resultado.TipologiaDetectada.Should().Be("Desconocido");
        resultado.PropuestaTipologia.Should().Be("ESCR-01");
        resultado.DetalleProveedores.Should().Contain(p => p.Proveedor == RestriccionTipologiasMotivos.PropuestaLibre);
    }

    [Fact]
    public async Task PasadaLibreFalla_NoRompeElResultadoDesconocido()
    {
        var sut = CreateSut((provider, input) =>
        {
            if (input.OmitirRestriccionTipologias)
            {
                throw new InvalidOperationException("fallo simulado de la pasada libre");
            }

            return new ResultadoClasificacion
            {
                ProveedorClasif = provider,
                TipologiaDetectada = "Desconocido",
                Confianza = 0.0
            };
        });

        var resultado = await sut.ClasificarAsync(
            BuildInput("gpt", new[] { "SERE-25" }, proponerSiDesconocido: true),
            CancellationToken.None);

        resultado.TipologiaDetectada.Should().Be("Desconocido");
        resultado.PropuestaTipologia.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task SinRestriccion_ComportamientoIntacto()
    {
        var sut = CreateSut((provider, _) => new ResultadoClasificacion
        {
            ProveedorClasif = provider,
            TipologiaDetectada = "ESCR-01",
            Confianza = 0.95
        });

        var resultado = await sut.ClasificarAsync(BuildInput("gpt"), CancellationToken.None);

        resultado.TipologiaDetectada.Should().Be("ESCR-01");
        resultado.RestriccionTipologias.Should().BeNull();
    }

    private sealed class TestableProvider : ConfigurableClasificarDataProvider
    {
        private readonly Func<string, ClasificacionInput, ResultadoClasificacion> _providerBehavior;

        public TestableProvider(
            string registryPath,
            ClassificationRoutingSettings routingSettings,
            Func<string, ClasificacionInput, ResultadoClasificacion> providerBehavior)
            : base(
                mockProvider: null!,
                azureProvider: null!,
                gptProvider: null!,
                hybridTdnProvider: null!,
                ruleClassifier: null!,
                windowExtractor: null!,
                hybridOptions: Options.Create(new HybridTdnOptions()),
                modelRegistryLoader: new ClassificationModelRegistryLoader(registryPath),
                routingSettings: Options.Create(routingSettings),
                logger: Mock.Of<ILogger<ConfigurableClasificarDataProvider>>())
        {
            _providerBehavior = providerBehavior;
        }

        protected override Task<ResultadoClasificacion> ExecuteProviderAsync(
            string provider,
            ClasificacionInput input,
            CancellationToken cancellationToken)
            => Task.FromResult(_providerBehavior(provider, input));
    }
}
