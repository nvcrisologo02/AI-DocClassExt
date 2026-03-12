#nullable enable
using System.Text.Json;
using DocumentIA.Core.Configuration;
using DocumentIA.Functions.Services;
using FluentAssertions;

namespace DocumentIA.Tests.Unit.Services;

public class ContentUnderstandingResultMapperTests
{
    [Fact]
    public void Map_AutoMappedFields_ConvertsStructuredFieldsToDatosExtraidos()
    {
        using var analysisDocument = JsonDocument.Parse(@"{
            ""result"": {
                ""contents"": [
                    {
                        ""fields"": {
                            ""FincaRegistral"": { ""type"": ""string"", ""valueString"": ""NS14-88921"" },
                            ""VPO"": { ""type"": ""boolean"", ""valueBoolean"": false },
                            ""superficies"": {
                                ""type"": ""array"",
                                ""valueArray"": [
                                    {
                                        ""type"": ""object"",
                                        ""valueObject"": {
                                            ""valor"": { ""type"": ""number"", ""valueNumber"": 97.35 },
                                            ""UnidadSuperficie"": { ""type"": ""string"", ""valueString"": ""m2_construidos"" }
                                        }
                                    }
                                ]
                            }
                        }
                    }
                ]
            }
        }");

        var config = new TipologiaValidationConfig
        {
            Extraction = new TipologiaExtractionConfig
            {
                Enabled = true,
                Provider = "azure-content-understanding",
                AutoMapUnmappedFields = true
            },
            Fields = new List<FieldValidationConfig>
            {
                new() { Name = "FincaRegistral", Type = "string" },
                new() { Name = "VPO", Type = "boolean" },
                new() { Name = "superficies", Type = "array" }
            }
        };

        var mapper = new ContentUnderstandingResultMapper();

        var result = mapper.Map(analysisDocument, config);

        result["FincaRegistral"].Should().Be("NS14-88921");
        result["VPO"].Should().Be(false);
        result["superficies"].Should().BeOfType<object[]>();
        var superficies = (object[])result["superficies"];
        superficies.Should().HaveCount(1);
        var item = superficies[0].Should().BeOfType<Dictionary<string, object>>().Subject;
        item["valor"].Should().Be(97.35m);
        item["UnidadSuperficie"].Should().Be("m2_construidos");
    }

    [Fact]
    public void Map_ExplicitFieldMapping_UsesConfiguredSourcePath()
    {
        using var analysisDocument = JsonDocument.Parse(@"{
            ""result"": {
                ""contents"": [
                    {
                        ""fields"": {
                            ""Owner"": {
                                ""type"": ""object"",
                                ""valueObject"": {
                                    ""Name"": { ""type"": ""string"", ""valueString"": ""MARIA LOPEZ RUIZ"" }
                                }
                            }
                        }
                    }
                ]
            }
        }");

        var config = new TipologiaValidationConfig
        {
            Extraction = new TipologiaExtractionConfig
            {
                Enabled = true,
                Provider = "azure-content-understanding",
                AutoMapUnmappedFields = false,
                FieldMappings = new List<ExtractionFieldMappingConfig>
                {
                    new() { TargetField = "Titular", SourcePath = "Owner.Name" }
                }
            },
            Fields = new List<FieldValidationConfig>
            {
                new() { Name = "Titular", Type = "string" }
            }
        };

        var mapper = new ContentUnderstandingResultMapper();

        var result = mapper.Map(analysisDocument, config);

        result["Titular"].Should().Be("MARIA LOPEZ RUIZ");
    }
}