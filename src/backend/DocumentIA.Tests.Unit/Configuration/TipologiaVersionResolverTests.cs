#nullable enable
using DocumentIA.Core.Configuration;
using FluentAssertions;

namespace DocumentIA.Tests.Unit.Configuration;

public class TipologiaVersionResolverTests : IDisposable
{
    private readonly string _tempDirectory;

    public TipologiaVersionResolverTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"tipologia-version-resolver-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public void Resolve_WithFamily_ReturnsDefaultVersion()
    {
        WriteValidationConfig("nota.simple.1_3.validation.json", "nota-simple", "1.3", false);
        WriteValidationConfig("nota.simple.1_4.validation.json", "nota-simple", "1.4", true);

        var sut = new TipologiaVersionResolver(_tempDirectory);

        var result = sut.Resolve("nota-simple");

        result.TipologiaId.Should().Be("nota-simple");
        result.Version.Should().Be("1.4");
        result.TechnicalKey.Should().Be("nota.simple.1_4");
        result.IsDefault.Should().BeTrue();
    }

    [Fact]
    public void Resolve_WithFamilyAndVersion_ReturnsRequestedVersion()
    {
        WriteValidationConfig("nota.simple.1_3.validation.json", "nota-simple", "1.3", false);
        WriteValidationConfig("nota.simple.1_4.validation.json", "nota-simple", "1.4", true);

        var sut = new TipologiaVersionResolver(_tempDirectory);

        var result = sut.Resolve("nota-simple@1.3");

        result.Version.Should().Be("1.3");
        result.TechnicalKey.Should().Be("nota.simple.1_3");
        result.IsDefault.Should().BeFalse();
    }

    [Fact]
    public void Resolve_WithLegacyTechnicalKey_ReturnsSameTechnicalKey()
    {
        WriteValidationConfig("nota.simple.1_4.validation.json", "nota-simple", "1.4", true);

        var sut = new TipologiaVersionResolver(_tempDirectory);

        var result = sut.Resolve("nota.simple.1_4");

        result.TechnicalKey.Should().Be("nota.simple.1_4");
        result.TipologiaId.Should().Be("nota-simple");
        result.Version.Should().Be("1.4");
    }

    [Fact]
    public void Resolve_WithoutDefaultAndMultipleVersions_Throws()
    {
        WriteValidationConfig("nota.simple.1_3.validation.json", "nota-simple", "1.3", false);
        WriteValidationConfig("nota.simple.1_4.validation.json", "nota-simple", "1.4", false);

        var sut = new TipologiaVersionResolver(_tempDirectory);

        var action = () => sut.Resolve("nota-simple");

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*no tiene ninguna version default*");
    }

    [Fact]
    public void Resolve_WithMultipleDefaultVersions_Throws()
    {
        WriteValidationConfig("nota.simple.1_3.validation.json", "nota-simple", "1.3", true);
        WriteValidationConfig("nota.simple.1_4.validation.json", "nota-simple", "1.4", true);

        var sut = new TipologiaVersionResolver(_tempDirectory);

        var action = () => sut.Resolve("nota-simple");

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*multiples versiones marcadas como default*");
    }

    [Fact]
    public void Resolve_WithUnknownVersion_Throws()
    {
        WriteValidationConfig("nota.simple.1_4.validation.json", "nota-simple", "1.4", true);

        var sut = new TipologiaVersionResolver(_tempDirectory);

        var action = () => sut.Resolve("nota-simple@1.2");

        action.Should().Throw<KeyNotFoundException>()
            .WithMessage("*1.2*");
    }

    [Fact]
    public void GetVersions_ReturnsOrderedVersionsForFamily()
    {
        WriteValidationConfig("nota.simple.1_4.validation.json", "nota-simple", "1.4", true);
        WriteValidationConfig("nota.simple.1_0.validation.json", "nota-simple", "1.0", false);
        WriteValidationConfig("tasacion.validation.json", "tasacion", "1.0", true);

        var sut = new TipologiaVersionResolver(_tempDirectory);

        var result = sut.GetVersions("nota-simple");

        result.Should().Equal("1.0", "1.4");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }

    private void WriteValidationConfig(string fileName, string tipologiaId, string version, bool isDefault)
    {
        var content = $$"""
        {
          "tipologiaId": "{{tipologiaId}}",
          "tipologiaNombre": "Test",
          "version": "{{version}}",
          "isDefault": {{isDefault.ToString().ToLowerInvariant()}},
          "fields": []
        }
        """;

        File.WriteAllText(Path.Combine(_tempDirectory, fileName), content);
    }
}