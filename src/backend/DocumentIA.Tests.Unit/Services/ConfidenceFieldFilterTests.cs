using DocumentIA.Core.Configuration;
using DocumentIA.Core.Services;
using FluentAssertions;

namespace DocumentIA.Tests.Unit.Services;

public class ConfidenceFieldFilterTests
{
    [Fact]
    public void GetAvoidConfidenceFields_ReturnsOnlyMarkedFields_CaseInsensitive()
    {
        var config = new TipologiaValidationConfig
        {
            Fields = new List<FieldValidationConfig>
            {
                new() { Name = "Titular", AvoidConfidence = false },
                new() { Name = "Motivacion", AvoidConfidence = true },
                new() { Name = "", AvoidConfidence = true }
            }
        };

        var result = ConfidenceFieldFilter.GetAvoidConfidenceFields(config);

        result.Should().ContainSingle().Which.Should().Be("Motivacion");
        result.Contains("motivacion").Should().BeTrue();
    }

    [Fact]
    public void FilterFieldConfidences_ExcludesAvoidConfidenceFields_AndKeepsOriginalMapComplete()
    {
        var confidenceMap = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["Titular"] = 0.95,
            ["Motivacion"] = 0.10,
            ["Importe"] = 0.85
        };
        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "motivacion" };

        var result = ConfidenceFieldFilter.FilterFieldConfidences(confidenceMap, excluded);

        result.Should().Equal(0.95, 0.85);
        confidenceMap.Should().ContainKey("Motivacion");
        confidenceMap["Motivacion"].Should().Be(0.10);
    }

    [Fact]
    public void FilterFieldConfidences_ReturnsNull_WhenAllFieldsAreExcluded()
    {
        var confidenceMap = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["Motivacion"] = 0.10
        };
        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Motivacion" };

        var result = ConfidenceFieldFilter.FilterFieldConfidences(confidenceMap, excluded);

        result.Should().BeNull();
    }

    [Fact]
    public void GetLowConfidenceFields_ExcludesAvoidConfidenceFields_ButKeepsOtherLowFields()
    {
        var confidenceMap = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["Titular"] = 0.95,
            ["Motivacion"] = 0.10,
            ["Importe"] = 0.40
        };
        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Motivacion" };

        var result = ConfidenceFieldFilter.GetLowConfidenceFields(confidenceMap, 0.60, excluded);

        result.Should().Equal("Importe");
    }

    [Fact]
    public void AvoidConfidenceFiltering_DoesNotChangeRequiredCompletenessInputs()
    {
        var fields = new List<FieldValidationConfig>
        {
            new() { Name = "Titular", Required = true },
            new() { Name = "Motivacion", Required = true, AvoidConfidence = true }
        };
        var datosExtraidos = new Dictionary<string, object>
        {
            ["Titular"] = "Valor"
        };
        var confidenceMap = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["Titular"] = 0.95,
            ["Motivacion"] = 0.10
        };
        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Motivacion" };

        var filteredConfs = ConfidenceFieldFilter.FilterFieldConfidences(confidenceMap, excluded);
        var requiredFields = fields.Count(f => f.Required);
        var requiredPresent = fields.Where(f => f.Required).Count(f => datosExtraidos.ContainsKey(f.Name));
        var (confidence, metrics) = ConfidenceCalculator.ExtracCU(
            filteredConfs,
            camposPresentes: datosExtraidos.Count,
            camposTotales: fields.Count,
            camposRequeridos: requiredFields,
            camposRequeridosPresentes: requiredPresent,
            warnings: 0);

        metrics.RatioRequeridos.Should().Be(0.5);
        confidence.Should().BeApproximately(0.75, 0.0001);
    }
}
