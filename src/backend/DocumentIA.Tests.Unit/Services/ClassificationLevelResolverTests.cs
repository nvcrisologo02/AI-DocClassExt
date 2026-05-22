using DocumentIA.Core.Services;
using DocumentIA.Core.Models;
using FluentAssertions;

namespace DocumentIA.Tests.Unit.Services;

public class ClassificationLevelResolverTests
{
    [Fact]
    public void Resolve_WhenRequestValueIsNull_ReturnsGlobalDefault()
    {
        var result = ClassificationLevelResolver.Resolve(null, ClassificationLevelResolver.DefaultLevel);

        result.Should().Be(ClassificationLevelResolver.LevelTdn1Tdn2);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_WhenRequestValueIsEmptyOrWhitespace_ReturnsGlobalDefault(string requestValue)
    {
        var result = ClassificationLevelResolver.Resolve(requestValue, ClassificationLevelResolver.DefaultLevel);

        result.Should().Be(ClassificationLevelResolver.LevelTdn1Tdn2);
    }

    [Theory]
    [InlineData("TDN1")]
    [InlineData("TDN1_TDN2")]
    public void Resolve_WhenRequestValueIsAllowed_ReturnsRequestedValue(string requestValue)
    {
        var result = ClassificationLevelResolver.Resolve(requestValue, ClassificationLevelResolver.DefaultLevel);

        result.Should().Be(requestValue);
    }

    [Theory]
    [InlineData("tdn1", "TDN1")]
    [InlineData("tdn1_tdn2", "TDN1_TDN2")]
    public void Resolve_WhenRequestValueHasDifferentCasing_ReturnsCanonicalValue(string requestValue, string expected)
    {
        var result = ClassificationLevelResolver.Resolve(requestValue, ClassificationLevelResolver.DefaultLevel);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("TDN2")]
    [InlineData("TDN1-TDN2")]
    [InlineData("ALL")]
    [InlineData("TDN1_TDN2_TDN3")]
    public void Resolve_WhenRequestValueIsNotAllowed_ThrowsValidationError(string requestValue)
    {
        var act = () => ClassificationLevelResolver.Resolve(requestValue, ClassificationLevelResolver.DefaultLevel);

        act.Should()
            .Throw<ArgumentException>()
            .WithMessage("*NivelClasificacion*")
            .And.Message.Should().Contain(requestValue);
    }

    [Fact]
    public void Resolve_WhenGlobalDefaultIsInvalid_ThrowsValidationError()
    {
        var act = () => ClassificationLevelResolver.Resolve(null, "TDN3");

        act.Should()
            .Throw<ArgumentException>()
            .WithMessage("*NivelClasificacionDefault*");
    }

    [Fact]
    public void AllowedLevels_ContainsOnlySupportedClassificationLevels()
    {
        ClassificationLevelResolver.AllowedLevels.Should().Equal("TDN1", "TDN1_TDN2");
    }

    [Fact]
    public void ApplyTo_WhenRequestValueUsesDifferentCasing_StoresCanonicalValue()
    {
        var config = new ConfiguracionIA
        {
            NivelClasificacion = "tdn1"
        };

        var result = ClassificationLevelResolver.ApplyTo(config, ClassificationLevelResolver.DefaultLevel);

        result.Should().Be(ClassificationLevelResolver.LevelTdn1);
        config.NivelClasificacion.Should().Be(ClassificationLevelResolver.LevelTdn1);
    }

    [Fact]
    public void ApplyTo_WhenRequestValueIsNull_StoresGlobalDefault()
    {
        var config = new ConfiguracionIA();

        var result = ClassificationLevelResolver.ApplyTo(config, ClassificationLevelResolver.DefaultLevel);

        result.Should().Be(ClassificationLevelResolver.LevelTdn1Tdn2);
        config.NivelClasificacion.Should().Be(ClassificationLevelResolver.LevelTdn1Tdn2);
    }
}