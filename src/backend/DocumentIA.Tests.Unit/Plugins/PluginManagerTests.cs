using System.Collections.Generic;
using System.Threading.Tasks;
using DocumentIA.Plugins.Integration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DocumentIA.Tests.Plugins
{
    public class PluginManagerTests
    {
        [Fact]
        public void RegisterPlugin_ValidPlugin_RegistersSuccessfully()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<PluginManager>>();
            var manager = new PluginManager(mockLogger.Object);
            var mockPlugin = new Mock<IIntegrationPlugin>();
            mockPlugin.Setup(p => p.PluginName).Returns("TestPlugin");
            mockPlugin.Setup(p => p.Version).Returns("1.0.0");

            // Act
            manager.RegisterPlugin("test-key", mockPlugin.Object);
            var retrieved = manager.GetPlugin("test-key");

            // Assert
            Assert.NotNull(retrieved);
            Assert.Equal("TestPlugin", retrieved.PluginName);
        }

        [Fact]
        public async Task ExecutePluginAsync_PluginNotFound_ReturnsError()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<PluginManager>>();
            var manager = new PluginManager(mockLogger.Object);

            // Act
            var result = await manager.ExecutePluginAsync("non-existent", new Dictionary<string, object>());

            // Assert
            Assert.False(result.Success);
            Assert.Equal("ERROR", result.Status);
            Assert.Contains("no encontrado", result.Message);
        }

        [Fact]
        public async Task ExecutePluginAsync_PluginThrowsException_ReturnsErrorResult()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<PluginManager>>();
            var manager = new PluginManager(mockLogger.Object);
            
            var mockPlugin = new Mock<IIntegrationPlugin>();
            mockPlugin.Setup(p => p.PluginName).Returns("FailingPlugin");
            mockPlugin.Setup(p => p.Version).Returns("1.0.0");
            mockPlugin.Setup(p => p.ExecuteAsync(It.IsAny<Dictionary<string, object>>()))
                .ThrowsAsync(new PluginException("FailingPlugin", "Test error", true));

            manager.RegisterPlugin("failing", mockPlugin.Object);

            // Act
            var result = await manager.ExecutePluginAsync("failing", new Dictionary<string, object>());

            // Assert
            Assert.False(result.Success);
            Assert.Equal("ERROR", result.Status);
            Assert.True((bool)result.Metadata["isTransient"]);
        }

        [Fact]
        public void ListPlugins_ReturnsAllRegisteredPlugins()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<PluginManager>>();
            var manager = new PluginManager(mockLogger.Object);

            var mockPlugin1 = CreateMockPlugin("Plugin1", "1.0.0");
            var mockPlugin2 = CreateMockPlugin("Plugin2", "2.0.0");

            manager.RegisterPlugin("key1", mockPlugin1);
            manager.RegisterPlugin("key2", mockPlugin2);

            // Act
            var plugins = manager.ListPlugins();

            // Assert
            Assert.Equal(2, plugins.Count);
            Assert.Contains(plugins, p => p.Name == "Plugin1");
            Assert.Contains(plugins, p => p.Name == "Plugin2");
        }

        private IIntegrationPlugin CreateMockPlugin(string name, string version)
        {
            var mock = new Mock<IIntegrationPlugin>();
            mock.Setup(p => p.PluginName).Returns(name);
            mock.Setup(p => p.Version).Returns(version);
            mock.Setup(p => p.ExecuteAsync(It.IsAny<Dictionary<string, object>>()))
                .ReturnsAsync(new IntegrationResult { Success = true, Status = "OK" });
            return mock.Object;
        }
    }
}
