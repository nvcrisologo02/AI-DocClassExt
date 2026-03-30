using System.Reflection;
using DocumentIA.Functions.Triggers.Admin;
using FluentAssertions;

namespace DocumentIA.Tests.Unit.Triggers.Admin;

public class PluginsTipologiaAdminFunctionValidationTests
{
    [Fact]
    public void TryValidatePluginConfig_WhenJsonIsValid_ReturnsTrueAndNoError()
    {
        var method = GetValidationMethod();
        var args = new object?[]
        {
            """
            {
              "plugins": [
                { "pluginKey": "gdc", "pluginType": "soap", "enabled": true, "priority": 1 }
              ]
            }
            """,
            "nota-simple",
            null
        };

        var result = (bool)method.Invoke(null, args)!;

        result.Should().BeTrue();
        args[2].Should().BeNull();
    }

    [Fact]
    public void TryValidatePluginConfig_WhenJsonIsInvalid_ReturnsFalseWithError()
    {
        var method = GetValidationMethod();
        var args = new object?[]
        {
            "{ invalid json }",
            "nota-simple",
            null
        };

        var result = (bool)method.Invoke(null, args)!;

        result.Should().BeFalse();
        args[2].Should().BeOfType<string>();
        args[2]!.ToString().Should().StartWith("Configuracion de plugins invalida:");
    }

    [Fact]
    public void TryValidatePluginConfig_WhenJsonIsEmptyObject_ReturnsTrue()
    {
        var method = GetValidationMethod();
        var args = new object?[]
        {
            "{}",
            "nota-simple",
            null
        };

        var result = (bool)method.Invoke(null, args)!;

        result.Should().BeTrue();
        args[2].Should().BeNull();
    }

    private static MethodInfo GetValidationMethod()
    {
        return typeof(PluginsTipologiaAdminFunction)
            .GetMethod("TryValidatePluginConfig", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("No se encontro metodo TryValidatePluginConfig.");
    }
}
