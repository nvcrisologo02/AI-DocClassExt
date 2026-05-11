#nullable enable
using DocumentIA.Core.Configuration;
using DocumentIA.Functions.Activities;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DocumentIA.Tests.Unit.Activities;

public class ResolverTipologiaActivityTests
{
    private readonly Mock<ITipologiaVersionResolver> _resolver;
    private readonly Mock<ILogger<ResolverTipologiaActivity>> _logger;
    private readonly ResolverTipologiaActivity _sut;

    public ResolverTipologiaActivityTests()
    {
        _resolver = new Mock<ITipologiaVersionResolver>(MockBehavior.Strict);
        _logger = new Mock<ILogger<ResolverTipologiaActivity>>();
        _sut = new ResolverTipologiaActivity(_resolver.Object, _logger.Object);
    }

    [Fact]
    public void Run_WithFamilyInput_ReturnsResolvedTipologia()
    {
        var expected = new ResolvedTipologia("nota-simple", "nota-simple", "1.4", "nota.simple.1_4", true);
        _resolver.Setup(r => r.Resolve("nota-simple")).Returns(expected);

        var result = _sut.Run("nota-simple");

        result.Should().BeSameAs(expected);
        _resolver.Verify(r => r.Resolve("nota-simple"), Times.Once);
    }

    [Fact]
    public void Run_WithFamilyAtVersionInput_ReturnsSpecificVersion()
    {
        var expected = new ResolvedTipologia("nota-simple@1.3", "nota-simple", "1.3", "nota.simple.1_3", false);
        _resolver.Setup(r => r.Resolve("nota-simple@1.3")).Returns(expected);

        var result = _sut.Run("nota-simple@1.3");

        result.TechnicalKey.Should().Be("nota.simple.1_3");
        result.IsDefault.Should().BeFalse();
        _resolver.Verify(r => r.Resolve("nota-simple@1.3"), Times.Once);
    }

    [Fact]
    public void Run_WithLegacyTechnicalKey_ReturnsResolvedTipologia()
    {
        var expected = new ResolvedTipologia("nota.simple.1_4", "nota-simple", "1.4", "nota.simple.1_4", true);
        _resolver.Setup(r => r.Resolve("nota.simple.1_4")).Returns(expected);

        var result = _sut.Run("nota.simple.1_4");

        result.TipologiaId.Should().Be("nota-simple");
        result.Version.Should().Be("1.4");
        _resolver.Verify(r => r.Resolve("nota.simple.1_4"), Times.Once);
    }

    [Fact]
    public void Run_WhenResolverThrows_PropagatesException()
    {
        _resolver.Setup(r => r.Resolve("familia-desconocida"))
            .Throws(new KeyNotFoundException("familia-desconocida no registrada"));

        var action = () => _sut.Run("familia-desconocida");

        action.Should().Throw<KeyNotFoundException>()
            .WithMessage("*familia-desconocida*");
    }
}
