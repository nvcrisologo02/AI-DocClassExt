using DocumentIA.Functions.Services;
using FluentAssertions;

namespace DocumentIA.Tests.Unit.Services;

public class GptHierarchicalClassificationParserTests
{
    [Fact]
    public void ParsePhase1_WhenJsonIsValid_ReturnsParsedPayload()
    {
        const string response = """
            {
              "tdn1": "nots",
              "propuesta": "Parece una nota simple"
            }
            """;

        var result = GptHierarchicalClassificationParser.ParsePhase1(response);

        result.Success.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Tdn1.Should().Be("NOTS");
        result.Value.Propuesta.Should().Be("Parece una nota simple");
    }

    [Fact]
    public void ParsePhase1_WhenTdn1IsNull_ReturnsParsedPayload()
    {
        const string response = """
            {
              "tdn1": null,
              "propuesta": "Sugerencia libre"
            }
            """;

        var result = GptHierarchicalClassificationParser.ParsePhase1(response);

        result.Success.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Tdn1.Should().BeNull();
        result.Value.Propuesta.Should().Be("Sugerencia libre");
    }

    [Fact]
    public void ParsePhase1_WhenResumenIsProvided_ReturnsResumen()
    {
        const string response = """
            {
              "tdn1": "escr",
              "propuesta": "Parece una escritura",
              "resumen": "Resumen ejecutivo"
            }
            """;

        var result = GptHierarchicalClassificationParser.ParsePhase1(response);

        result.Success.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Tdn1.Should().Be("ESCR");
        result.Value.Propuesta.Should().Be("Parece una escritura");
        result.Value.Resumen.Should().Be("Resumen ejecutivo");
    }

    [Fact]
    public void ParsePhase1_WhenJsonIsInvalid_ReturnsControlledError()
    {
        const string response = "no es json";

        var result = GptHierarchicalClassificationParser.ParsePhase1(response);

        result.Success.Should().BeFalse();
        result.ErrorReason.Should().Be(GptHierarchicalClassificationParser.Phase1ParsingErrorReason);
    }

    [Fact]
    public void ParsePhase1_WhenFieldsAreMissing_ReturnsControlledError()
    {
        const string response = """
            {
              "tdn1": "NOTS"
            }
            """;

        var result = GptHierarchicalClassificationParser.ParsePhase1(response);

        result.Success.Should().BeFalse();
        result.ErrorReason.Should().Be(GptHierarchicalClassificationParser.Phase1ParsingErrorReason);
    }

    [Fact]
    public void ParsePhase2_WhenJsonIsValid_ReturnsParsedPayload()
    {
        const string response = """
            {
              "tdn2": "nots-01"
            }
            """;

        var result = GptHierarchicalClassificationParser.ParsePhase2(response);

        result.Success.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Tdn2.Should().Be("NOTS-01");
    }

    [Fact]
    public void ParsePhase2_WhenTdn2IsMissing_ReturnsControlledError()
    {
        const string response = "{}";

        var result = GptHierarchicalClassificationParser.ParsePhase2(response);

        result.Success.Should().BeFalse();
        result.ErrorReason.Should().Be(GptHierarchicalClassificationParser.Phase2ParsingErrorReason);
    }
}