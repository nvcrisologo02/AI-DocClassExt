#nullable enable
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DocumentIA.Core.Configuration;
using DocumentIA.Core.Models;
using DocumentIA.Functions.Services;
using DocumentIA.Functions.Services.Classification;
using DocumentIA.Functions.Services.Resilience;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace DocumentIA.Tests.Unit.Services.Classification;

/// <summary>
/// Verifica que <see cref="ConfigurableClasificarDataProvider"/> NO absorbe
/// <see cref="RateLimitExhaustedException"/> en la rama GlobalFallback. Cuando GPT está
/// configurado SOLO como GlobalFallbackProvider (no como paso del flujo, p.ej. flujo "di"),
/// una 429 agotada debe propagar para que la capa superior marque PENDIENTE_REINTENTO,
/// en vez de degradarse a un resultado "Desconocido".
/// </summary>
public class ConfigurableClasificarDataProviderGlobalFallbackTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _registryPath;

    public ConfigurableClasificarDataProviderGlobalFallbackTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"ConfigurableClasificarGlobalFallbackTests_{Guid.NewGuid()}");
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

    [Fact]
    public async Task ClasificarAsync_WhenGlobalFallbackThrowsRateLimitExhausted_Propagates()
    {
        // Flujo "di" (sin gpt en el loop) + GlobalFallbackProvider="gpt".
        // El provider del flujo devuelve Desconocido (no satisfactorio) y el fallback global lanza 429 agotada.
        var sut = new TestableProvider(
            _registryPath,
            new ClassificationRoutingSettings
            {
                UseGlobalFallback = true,
                GlobalFallbackProvider = "gpt"
            },
            providerBehavior: provider =>
            {
                if (string.Equals(provider, "gpt", StringComparison.OrdinalIgnoreCase))
                {
                    throw new RateLimitExhaustedException("cuota agotada en el fallback global");
                }

                return new ResultadoClasificacion
                {
                    ProveedorClasif = provider,
                    TipologiaDetectada = "Desconocido",
                    Confianza = 0.0
                };
            });

        var act = async () => await sut.ClasificarAsync(BuildInput("di"), CancellationToken.None);

        await act.Should().ThrowAsync<RateLimitExhaustedException>();
    }

    private static ClasificacionInput BuildInput(string provider)
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
                    Classification = new ConfiguracionIA { Provider = provider }
                }
            }
        };
    }

    /// <summary>
    /// Subclase de test que intercepta <c>ExecuteProviderAsync</c> para simular el resultado/fallo
    /// de cada provider sin construir los providers concretos (constructores con dependencias pesadas).
    /// </summary>
    private sealed class TestableProvider : ConfigurableClasificarDataProvider
    {
        private readonly Func<string, ResultadoClasificacion> _providerBehavior;

        public TestableProvider(
            string registryPath,
            ClassificationRoutingSettings routingSettings,
            Func<string, ResultadoClasificacion> providerBehavior)
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
            => Task.FromResult(_providerBehavior(provider));
    }
}
