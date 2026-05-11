using System.Text.Json;
using DocumentIA.Admin.Services;
using DocumentIA.Core.Configuration;
using FluentAssertions;

namespace DocumentIA.Tests.Admin.Wizard;

public class TipologiaWizardStateServiceBuildConfigTests
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public void BuildConfigurationJson_EmptyFields_ProducesValidJson()
    {
        var svc = new TipologiaWizardStateService();
        svc.Draft.Codigo = "test-tipo";
        svc.Draft.Nombre = "Test";
        svc.Draft.Version = "1.0.0";
        svc.Draft.SkipGdcUpload = true;

        var json = svc.BuildConfigurationJson();
        var config = JsonSerializer.Deserialize<TipologiaValidationConfig>(json, JsonOpts);

        config.Should().NotBeNull();
        config!.TipologiaId.Should().Be("test-tipo");
        config.Fields.Should().BeEmpty();
    }

    [Fact]
    public void BuildConfigurationJson_RangeRule_EmitsMinMax()
    {
        var svc = CreateServiceWithRangeRule("0", "100");

        var json = svc.BuildConfigurationJson();
        var config = JsonSerializer.Deserialize<TipologiaValidationConfig>(json, JsonOpts)!;
        var rule = config.Fields[0].Rules[0];

        rule.RuleType.Should().Be("range");
        rule.Parameters.Should().ContainKey("min");
        rule.Parameters.Should().ContainKey("max");
    }

    [Fact]
    public void BuildConfigurationJson_RangeRule_EmptyMinMax_EmitsEmptyParameters()
    {
        var svc = CreateServiceWithRangeRule(string.Empty, string.Empty);

        var json = svc.BuildConfigurationJson();
        var config = JsonSerializer.Deserialize<TipologiaValidationConfig>(json, JsonOpts)!;
        var rule = config.Fields[0].Rules[0];

        rule.Parameters.Should().NotContainKey("min");
        rule.Parameters.Should().NotContainKey("max");
    }

    [Fact]
    public void BuildConfigurationJson_RegexRule_EmitsPattern()
    {
        var svc = new TipologiaWizardStateService();
        svc.Draft.SkipGdcUpload = true;
        svc.AddField();
        svc.Draft.Fields[0].Name = "Campo1";
        svc.Draft.Fields[0].Type = "string";
        svc.AddRule(0);
        svc.Draft.Fields[0].Rules[0].RuleType = "regex";
        svc.Draft.Fields[0].Rules[0].RegexPattern = @"^\d{8}$";

        var json = svc.BuildConfigurationJson();
        var config = JsonSerializer.Deserialize<TipologiaValidationConfig>(json, JsonOpts)!;
        var rule = config.Fields[0].Rules[0];

        rule.RuleType.Should().Be("regex");
        rule.Parameters.Should().ContainKey("pattern");
    }

    [Fact]
    public void BuildConfigurationJson_EnumRule_EmitsValuesAndCaseSensitive()
    {
        var svc = new TipologiaWizardStateService();
        svc.Draft.SkipGdcUpload = true;
        svc.AddField();
        svc.Draft.Fields[0].Name = "Estado";
        svc.Draft.Fields[0].Type = "string";
        svc.AddRule(0);
        svc.Draft.Fields[0].Rules[0].RuleType = "enum";
        svc.Draft.Fields[0].Rules[0].EnumValues = "Activo, Inactivo, Pendiente";
        svc.Draft.Fields[0].Rules[0].EnumCaseSensitive = false;

        var json = svc.BuildConfigurationJson();
        var config = JsonSerializer.Deserialize<TipologiaValidationConfig>(json, JsonOpts)!;
        var rule = config.Fields[0].Rules[0];

        rule.Parameters.Should().ContainKey("values");
        rule.Parameters.Should().ContainKey("caseSensitive");
    }

    [Fact]
    public void BuildConfigurationJson_DateRule_EmitsFormatsAndFlags()
    {
        var svc = new TipologiaWizardStateService();
        svc.Draft.SkipGdcUpload = true;
        svc.AddField();
        svc.Draft.Fields[0].Name = "FechaDoc";
        svc.Draft.Fields[0].Type = "date";
        svc.AddRule(0);
        svc.Draft.Fields[0].Rules[0].RuleType = "date";
        svc.Draft.Fields[0].Rules[0].DateFormats = "dd/MM/yyyy;yyyy-MM-dd";
        svc.Draft.Fields[0].Rules[0].DateAllowFuture = false;
        svc.Draft.Fields[0].Rules[0].DateAllowPast = true;

        var json = svc.BuildConfigurationJson();
        var config = JsonSerializer.Deserialize<TipologiaValidationConfig>(json, JsonOpts)!;
        var rule = config.Fields[0].Rules[0];

        rule.Parameters.Should().ContainKey("formats");
        rule.Parameters.Should().ContainKey("allowFuture");
        rule.Parameters.Should().ContainKey("allowPast");
    }

    [Fact]
    public void BuildConfigurationJson_MinLengthRule_EmitsValue()
    {
        var svc = new TipologiaWizardStateService();
        svc.Draft.SkipGdcUpload = true;
        svc.AddField();
        svc.Draft.Fields[0].Name = "Referencia";
        svc.Draft.Fields[0].Type = "string";
        svc.AddRule(0);
        svc.Draft.Fields[0].Rules[0].RuleType = "minlength";
        svc.Draft.Fields[0].Rules[0].LengthValue = "5";

        var json = svc.BuildConfigurationJson();
        var config = JsonSerializer.Deserialize<TipologiaValidationConfig>(json, JsonOpts)!;
        var rule = config.Fields[0].Rules[0];

        rule.Parameters.Should().ContainKey("value");
    }

    [Fact]
    public void BuildConfigurationJson_MaxLengthRule_EmitsValue()
    {
        var svc = new TipologiaWizardStateService();
        svc.Draft.SkipGdcUpload = true;
        svc.AddField();
        svc.Draft.Fields[0].Name = "Codigo";
        svc.Draft.Fields[0].Type = "string";
        svc.AddRule(0);
        svc.Draft.Fields[0].Rules[0].RuleType = "maxlength";
        svc.Draft.Fields[0].Rules[0].LengthValue = "20";

        var json = svc.BuildConfigurationJson();
        var config = JsonSerializer.Deserialize<TipologiaValidationConfig>(json, JsonOpts)!;
        var rule = config.Fields[0].Rules[0];

        rule.RuleType.Should().Be("maxlength");
        rule.Parameters.Should().ContainKey("value");
    }

    [Fact]
    public void BuildConfigurationJson_RequiredRule_EmitsNoParameters()
    {
        var svc = new TipologiaWizardStateService();
        svc.Draft.SkipGdcUpload = true;
        svc.AddField();
        svc.Draft.Fields[0].Name = "Campo1";
        svc.Draft.Fields[0].Type = "string";
        svc.AddRule(0);
        svc.Draft.Fields[0].Rules[0].RuleType = "required";

        var json = svc.BuildConfigurationJson();
        var config = JsonSerializer.Deserialize<TipologiaValidationConfig>(json, JsonOpts)!;
        var rule = config.Fields[0].Rules[0];

        rule.RuleType.Should().Be("required");
        rule.Parameters.Should().BeEmpty();
    }

    [Fact]
    public void BuildConfigurationJson_FieldWithEmptyName_IsOmitted()
    {
        var svc = new TipologiaWizardStateService();
        svc.Draft.SkipGdcUpload = true;
        svc.AddField(); // Empty name → should be excluded
        svc.AddField();
        svc.Draft.Fields[1].Name = "CampoValido";

        var json = svc.BuildConfigurationJson();
        var config = JsonSerializer.Deserialize<TipologiaValidationConfig>(json, JsonOpts)!;

        config.Fields.Should().HaveCount(1);
        config.Fields[0].Name.Should().Be("CampoValido");
    }

    [Fact]
    public void BuildConfigurationJson_FieldSourcePath_CreatesFieldMapping()
    {
        var svc = new TipologiaWizardStateService();
        svc.Draft.SkipGdcUpload = true;
        svc.Draft.EnableExtraction = true;
        svc.AddField();
        svc.Draft.Fields[0].Name = "Titular";
        svc.Draft.Fields[0].SourcePath = "document.titular";

        var json = svc.BuildConfigurationJson();
        var config = JsonSerializer.Deserialize<TipologiaValidationConfig>(json, JsonOpts)!;

        config.Extraction.FieldMappings.Should().HaveCount(1);
        config.Extraction.FieldMappings[0].TargetField.Should().Be("Titular");
        config.Extraction.FieldMappings[0].SourcePath.Should().Be("document.titular");
    }

    private static TipologiaWizardStateService CreateServiceWithRangeRule(string min, string max)
    {
        var svc = new TipologiaWizardStateService();
        svc.Draft.SkipGdcUpload = true;
        svc.AddField();
        svc.Draft.Fields[0].Name = "Valor";
        svc.Draft.Fields[0].Type = "number";
        svc.AddRule(0);
        svc.Draft.Fields[0].Rules[0].RuleType = "range";
        svc.Draft.Fields[0].Rules[0].RangeMin = min;
        svc.Draft.Fields[0].Rules[0].RangeMax = max;
        return svc;
    }
}
