using System.Linq;
using DocumentIA.Core.Configuration;
using FluentAssertions;
using Xunit;

namespace DocumentIA.Tests.Unit.Configuration;

public class PromptConfigTests
{
    [Fact]
    public void WithModelKey_DevuelveCopiaConTodasLasPropiedadesYSoloCambiaLaClave()
    {
        var original = new PromptConfig
        {
            Enabled = true,
            ModelKey = "",
            SystemPrompt = "Sistema",
            UserPromptTemplate = "Usuario {contenido}",
            MaxTokens = 1234,
            Temperature = 0.7,
            ContentMode = "vision"
        };

        var copia = original.WithModelKey("prompt.gpt56-luna");

        copia.Should().NotBeSameAs(original);
        copia.ModelKey.Should().Be("prompt.gpt56-luna");
        original.ModelKey.Should().BeEmpty();

        // Comprobación por reflexión: cualquier propiedad nueva de PromptConfig queda copiada
        // sin tocar este test ni los sitios que usan WithModelKey.
        var propiedades = typeof(PromptConfig).GetProperties().Where(p => p.Name != nameof(PromptConfig.ModelKey));
        foreach (var propiedad in propiedades)
        {
            propiedad.GetValue(copia).Should().Be(propiedad.GetValue(original), $"la propiedad {propiedad.Name} debe copiarse");
        }
    }
}
