using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using DocumentIA.Plugins.Integration;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DocumentIA.Tests.Unit.Plugins
{
    public class CustomPluginTests
    {
        [Fact]
        public async Task ExecuteAsync_PreservesPayloadIdActivoWhenEnricherDoesNotReturnIt()
        {
            var loggerMock = new Mock<ILogger<CustomPlugin>>();
            var plugin = new CustomPlugin(loggerMock.Object);

            var enricherMock = new Mock<ICustomEnricher>();
            enricherMock.SetupGet(e => e.Name).Returns("FakeEnricher");
            enricherMock.SetupGet(e => e.Version).Returns("0.1");
            enricherMock.Setup(e => e.InitializeAsync(It.IsAny<Dictionary<string, object>>()))
                        .Returns(Task.CompletedTask);
            // Enricher returns data WITHOUT idActivo
            enricherMock.Setup(e => e.EnrichAsync(It.IsAny<Dictionary<string, object>>()))
                        .ReturnsAsync(new Dictionary<string, object> { ["foo"] = "bar" });

            // Inject mock into private field 'enricherInstance'
            var field = typeof(CustomPlugin).GetField("enricherInstance", BindingFlags.NonPublic | BindingFlags.Instance);
            field.Should().NotBeNull();
            field!.SetValue(plugin, enricherMock.Object);

            var payload = new Dictionary<string, object>
            {
                ["datosExtraidos"] = new Dictionary<string, object> { ["campo"] = "x" },
                ["idActivo"] = "ACT-CUST-1"
            };

            var result = await plugin.ExecuteAsync(payload);

            result.Success.Should().BeTrue();
            result.ResponseData.Should().ContainKey("idActivo");
            result.ResponseData["idActivo"]!.ToString().Should().Be("ACT-CUST-1");
        }

        [Fact]
        public async Task ExecuteAsync_UsesEnricherReturnedIdActivoWhenPresent()
        {
            var loggerMock = new Mock<ILogger<CustomPlugin>>();
            var plugin = new CustomPlugin(loggerMock.Object);

            var enricherMock = new Mock<ICustomEnricher>();
            enricherMock.SetupGet(e => e.Name).Returns("FakeEnricher");
            enricherMock.SetupGet(e => e.Version).Returns("0.1");
            enricherMock.Setup(e => e.InitializeAsync(It.IsAny<Dictionary<string, object>>()))
                        .Returns(Task.CompletedTask);
            // Enricher returns data WITH its own idActivo
            enricherMock.Setup(e => e.EnrichAsync(It.IsAny<Dictionary<string, object>>()))
                        .ReturnsAsync(new Dictionary<string, object> { ["idActivo"] = "ENR-999" });

            var field = typeof(CustomPlugin).GetField("enricherInstance", BindingFlags.NonPublic | BindingFlags.Instance);
            field.Should().NotBeNull();
            field!.SetValue(plugin, enricherMock.Object);

            var payload = new Dictionary<string, object>
            {
                ["datosExtraidos"] = new Dictionary<string, object> { ["campo"] = "x" },
                ["idActivo"] = "ACT-CUST-2"
            };

            var result = await plugin.ExecuteAsync(payload);

            result.Success.Should().BeTrue();
            result.ResponseData.Should().ContainKey("idActivo");
            result.ResponseData["idActivo"]!.ToString().Should().Be("ENR-999");
        }
    }
}
