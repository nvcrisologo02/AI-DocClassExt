using DocumentIA.Core.Configuration;
using FluentAssertions;

namespace DocumentIA.Tests.Unit.Configuration;

public class AzureContentUnderstandingOptionsTests
{
    [Fact]
    public void Defaults_TimeoutYReintentos_SonRealistasParaDocumentosLargos()
    {
        var options = new AzureContentUnderstandingOptions();

        // p90 de análisis CU exitoso en prod es 95-170s: 90s por intento mataba
        // análisis legítimos; 3 intentos quemaban ~280s sin resultado.
        options.HardTimeoutSeconds.Should().Be(300);
        options.MaxRetries.Should().Be(2);
    }
}
