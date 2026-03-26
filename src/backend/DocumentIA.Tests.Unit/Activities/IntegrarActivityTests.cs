#nullable enable
using DocumentIA.Core.Models;
using DocumentIA.Functions.Activities;
using DocumentIA.Plugins.Integration;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DocumentIA.Tests.Unit.Activities;

public class IntegrarActivityTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly PluginManager _pluginManager;
    private readonly IntegrarActivity _sut;

    public IntegrarActivityTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"IntegrarActivityTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDirectory);

        File.WriteAllText(
            Path.Combine(_tempDirectory, "test.tipologia.plugins.json"),
            """
            {
              "tipologiaId": "test.tipologia",
              "plugins": [
                {
                  "pluginKey": "mock-plugin",
                  "pluginType": "custom",
                  "enabled": true,
                  "priority": 1,
                  "configuration": {}
                }
              ]
            }
            """);

        _pluginManager = new PluginManager(new Mock<ILogger<PluginManager>>().Object);

        var configLoader = new PluginConfigLoader(
            _tempDirectory,
            new Mock<ILogger<PluginConfigLoader>>().Object);

        var httpClientFactory = new Mock<IHttpClientFactory>();
        var pluginFactoryLogger = new Mock<ILogger<PluginFactory>>();
        var loggerFactory = new Mock<ILoggerFactory>();

        var pluginFactory = new PluginFactory(
            httpClientFactory.Object,
            pluginFactoryLogger.Object,
            loggerFactory.Object);

        _sut = new IntegrarActivity(
            new Mock<ILogger<IntegrarActivity>>().Object,
            _pluginManager,
            configLoader,
            pluginFactory);
    }

    [Fact]
    public async Task Run_WhenPluginReturnsDifferentIdActivo_MarksIdActivoAsChanged()
    {
        var plugin = new Mock<IIntegrationPlugin>(MockBehavior.Strict);
        plugin.SetupGet(p => p.PluginName).Returns("Mock Plugin");
        plugin.SetupGet(p => p.Version).Returns("1.0.0");
        plugin.Setup(p => p.ExecuteAsync(It.IsAny<Dictionary<string, object>>()))
            .ReturnsAsync(new IntegrationResult
            {
                Success = true,
                Status = "OK",
                Message = "ok",
                StatusCode = 200,
                Duration = TimeSpan.FromMilliseconds(12),
                ResponseData = new Dictionary<string, object>
                {
                    ["idActivo"] = "ACT-999",
                    ["otroCampo"] = "valor"
                }
            });

        _pluginManager.RegisterPlugin("mock-plugin", plugin.Object);

        var input = new IntegrarInput
        {
            Tipologia = "test.tipologia",
            DocumentoId = "doc-1",
            IdActivo = "ACT-001",
            DatosExtraidos = new Dictionary<string, object>
            {
                ["campoInicial"] = "valor-inicial"
            },
            Metadata = new Dictionary<string, object>()
        };

        var result = await _sut.Run(input);

        result.Estado.Should().Be("OK");
        result.IdActivoEntrada.Should().Be("ACT-001");
        result.IdActivoResuelto.Should().Be("ACT-999");
        result.IdActivoCambiado.Should().BeTrue();
        result.DatosFinales.Should().ContainKey("idActivo");
        result.DatosFinales["idActivo"].Should().Be("ACT-999");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }
}
