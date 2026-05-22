#nullable enable
using DocumentIA.Core.Configuration;
using DocumentIA.Data.Entities;
using FluentAssertions;

namespace DocumentIA.Tests.Unit.Configuration;

public class TipologiaVersionResolverTests
{
    private readonly List<TipologiaEntity> _entities = new();

    [Fact]
    public void Resolve_WithFamily_ReturnsDefaultVersion()
    {
        WriteValidationConfig("nota.simple.1_3", "nota-simple", "1.3", false);
        WriteValidationConfig("nota.simple.1_4", "nota-simple", "1.4", true);

        var sut = CreateSut();

        var result = sut.Resolve("nota-simple");

        result.TipologiaId.Should().Be("nota-simple");
        result.Version.Should().Be("1.4");
        result.TechnicalKey.Should().Be("nota.simple.1_4");
        result.IsDefault.Should().BeTrue();
    }

    [Fact]
    public void Resolve_WithFamilyAndVersion_ReturnsRequestedVersion()
    {
        WriteValidationConfig("nota.simple.1_3", "nota-simple", "1.3", false);
        WriteValidationConfig("nota.simple.1_4", "nota-simple", "1.4", true);

        var sut = CreateSut();

        var result = sut.Resolve("nota-simple@1.3");

        result.Version.Should().Be("1.3");
        result.TechnicalKey.Should().Be("nota.simple.1_3");
        result.IsDefault.Should().BeFalse();
    }

    [Fact]
    public void Resolve_WithLegacyTechnicalKey_ReturnsSameTechnicalKey()
    {
        WriteValidationConfig("nota.simple.1_4", "nota-simple", "1.4", true);

        var sut = CreateSut();

        var result = sut.Resolve("nota.simple.1_4");

        result.TechnicalKey.Should().Be("nota.simple.1_4");
        result.TipologiaId.Should().Be("nota-simple");
        result.Version.Should().Be("1.4");
    }

    [Fact]
    public void Resolve_WithTechnicalKeyAndVersionSuffix_ReturnsTechnicalKeyMatch()
    {
        WriteValidationConfig("IBI_1.1", "IBI", "1.1", true);

        var sut = CreateSut();

        var result = sut.Resolve("IBI_1.1@1.1");

        result.TechnicalKey.Should().Be("IBI_1.1");
        result.TipologiaId.Should().Be("IBI");
        result.Version.Should().Be("1.1");
    }

    [Fact]
    public void Resolve_WithoutDefaultAndMultipleVersions_Throws()
    {
        WriteValidationConfig("nota.simple.1_3", "nota-simple", "1.3", false);
        WriteValidationConfig("nota.simple.1_4", "nota-simple", "1.4", false);

        var sut = CreateSut();

        var action = () => sut.Resolve("nota-simple");

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*no tiene ninguna version default*");
    }

    [Fact]
    public void Resolve_WithMultipleDefaultVersions_Throws()
    {
        WriteValidationConfig("nota.simple.1_3", "nota-simple", "1.3", true);
        WriteValidationConfig("nota.simple.1_4", "nota-simple", "1.4", true);

        var sut = CreateSut();

        var action = () => sut.Resolve("nota-simple");

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*multiples versiones marcadas como default*");
    }

    [Fact]
    public void Resolve_WithUnknownVersion_Throws()
    {
        WriteValidationConfig("nota.simple.1_4", "nota-simple", "1.4", true);

        var sut = CreateSut();

        var action = () => sut.Resolve("nota-simple@1.2");

        action.Should().Throw<KeyNotFoundException>()
            .WithMessage("*1.2*");
    }

    [Fact]
    public void GetVersions_ReturnsOrderedVersionsForFamily()
    {
        WriteValidationConfig("nota.simple.1_4", "nota-simple", "1.4", true);
        WriteValidationConfig("nota.simple.1_0", "nota-simple", "1.0", false);
        WriteValidationConfig("tasacion", "tasacion", "1.0", true);

        var sut = CreateSut();

        var result = sut.GetVersions("nota-simple");

        result.Should().Equal("1.0", "1.4");
    }

        [Fact]
        public void Resolve_WithPromptOnlyTypology_SetsPromptAndExtractionFlags()
        {
                var content = """
                {
                    "tipologiaId": "resumen-documental",
                    "tipologiaNombre": "Resumen Documental",
                    "version": "1.0",
                    "isDefault": true,
                    "extraction": {
                        "enabled": false,
                        "provider": "mock",
                        "modelKey": "unused"
                    },
                    "promptConfig": {
                        "enabled": true,
                        "modelKey": "default.gpt4o-mini",
                        "systemPrompt": "",
                        "userPromptTemplate": "Resume: {contenido}",
                        "maxTokens": 300,
                        "temperature": 0.0,
                        "contentMode": "vision"
                    },
                    "fields": []
                }
                """;

                AddEntity("resumen.documental", content);

                var sut = CreateSut();

                var result = sut.Resolve("resumen-documental");

                result.PromptEnabled.Should().BeTrue();
                result.ExtractionEnabled.Should().BeFalse();
        }

    private TipologiaVersionResolver CreateSut()
    {
        return new TipologiaVersionResolver(() => _entities);
    }

    private void AddEntity(string codigo, string configuracionJson)
    {
        _entities.Add(new TipologiaEntity
        {
            Codigo = codigo,
            Activa = true,
            Estado = EstadoTipologia.Published,
            ConfiguracionJson = configuracionJson
        });
    }

    private void WriteValidationConfig(string technicalKey, string tipologiaId, string version, bool isDefault)
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

        AddEntity(technicalKey, content);
    }
}