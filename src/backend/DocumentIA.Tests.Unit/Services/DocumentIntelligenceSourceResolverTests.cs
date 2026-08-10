#nullable enable
using DocumentIA.Core.Configuration;
using FluentAssertions;

namespace DocumentIA.Tests.Unit.Services;

public class DocumentIntelligenceSourceResolverTests
{
    [Fact]
    public void DocumentIntelligenceSettings_PorDefecto_NoUsaContenidoInline()
    {
        var settings = new DocumentIntelligenceSettings();

        settings.UseInlineContent.Should().BeFalse();
    }
}
