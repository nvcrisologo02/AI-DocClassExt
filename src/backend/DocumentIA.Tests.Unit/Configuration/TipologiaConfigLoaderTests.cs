#nullable enable
using DocumentIA.Core.Configuration;
using DocumentIA.Core.Validation;
using DocumentIA.Core.Validation.Models;
using DocumentIA.Core.Validation.Rules;
using DocumentIA.Data.Entities;
using DocumentIA.Data.Repositories;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace DocumentIA.Tests.Unit.Configuration
{
    /// <summary>
    /// Tests para TipologiaConfigLoader - cargador de configuraciones de validación desde archivos JSON
    /// </summary>
    public class TipologiaConfigLoaderTests : IDisposable
    {
        private readonly string _tempDirectory;

        public TipologiaConfigLoaderTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), $"TipologiaConfigLoaderTests_{Guid.NewGuid()}");
            Directory.CreateDirectory(_tempDirectory);
        }

        #region LoadConfig Tests

        [Fact]
        public void LoadConfig_ValidJsonFile_ReturnsCorrectConfig()
        {
            // Arrange
            var configPath = Path.Combine(_tempDirectory, "notasimple.validation.json");
            var jsonContent = @"{
                ""tipologiaId"": ""notasimple"",
                ""tipologiaNombre"": ""Nota Simple"",
                ""version"": ""1.0"",
                ""fields"": [
                    {
                        ""name"": ""Titular"",
                        ""type"": ""string"",
                        ""required"": true,
                        ""rules"": []
                    }
                ]
            }";
            File.WriteAllText(configPath, jsonContent);

            var loader = CreateLoader();

            // Act
            var config = loader.LoadConfig("notasimple");

            // Assert
            config.Should().NotBeNull();
            config.TipologiaId.Should().Be("notasimple");
            config.TipologiaNombre.Should().Be("Nota Simple");
            config.Version.Should().Be("1.0");
            config.Fields.Should().HaveCount(1);
            config.Fields[0].Name.Should().Be("Titular");
            config.Fields[0].Required.Should().BeTrue();
        }

        [Fact]
        public void LoadConfig_WithExtractionSection_DeserializesExtractionSettings()
        {
            var configPath = Path.Combine(_tempDirectory, "extract.validation.json");
            var jsonContent = @"{
                ""tipologiaId"": ""extract"",
                ""tipologiaNombre"": ""Extraction Config"",
                ""version"": ""1.0"",
                ""extraction"": {
                    ""enabled"": true,
                    ""provider"": ""azure-content-understanding"",
                    ""modelKey"": ""nota.simple.1_4.azure-cu"",
                    ""autoMapUnmappedFields"": false,
                    ""fieldMappings"": [
                        {
                            ""targetField"": ""Titular"",
                            ""sourcePath"": ""Owner.Name""
                        }
                    ]
                },
                ""fields"": []
            }";
            File.WriteAllText(configPath, jsonContent);

            var loader = CreateLoader();

            var config = loader.LoadConfig("extract");

            config.Extraction.Enabled.Should().BeTrue();
            config.Extraction.Provider.Should().Be("azure-content-understanding");
            config.Extraction.ModelKey.Should().Be("nota.simple.1_4.azure-cu");
            config.Extraction.AutoMapUnmappedFields.Should().BeFalse();
            config.Extraction.FieldMappings.Should().ContainSingle();
        }

        [Fact]
        public void LoadConfig_WithPromptSection_DeserializesPromptSettings()
        {
            var configPath = Path.Combine(_tempDirectory, "prompt.validation.json");
            var jsonContent = @"{
                ""tipologiaId"": ""prompt-test"",
                ""tipologiaNombre"": ""Prompt Test"",
                ""version"": ""1.0"",
                ""extraction"": {
                    ""enabled"": false,
                    ""provider"": ""mock"",
                    ""modelKey"": ""unused""
                },
                ""promptConfig"": {
                    ""enabled"": true,
                    ""modelKey"": ""default.gpt4o-mini"",
                    ""systemPrompt"": ""Sistema"",
                    ""userPromptTemplate"": ""Resumen: {contenido}"",
                    ""maxTokens"": 800,
                    ""temperature"": 0.1,
                    ""contentMode"": ""vision""
                },
                ""fields"": []
            }";
            File.WriteAllText(configPath, jsonContent);

            var loader = CreateLoader();

            var config = loader.LoadConfig("prompt");

            config.PromptConfig.Should().NotBeNull();
            config.PromptConfig!.Enabled.Should().BeTrue();
            config.PromptConfig.ModelKey.Should().Be("default.gpt4o-mini");
            config.PromptConfig.SystemPrompt.Should().Be("Sistema");
            config.PromptConfig.UserPromptTemplate.Should().Contain("{contenido}");
            config.PromptConfig.ContentMode.Should().Be("vision");
            config.Extraction.Enabled.Should().BeFalse();
        }

        [Fact]
        public void LoadConfig_NonExistentFile_ThrowsFileNotFoundException()
        {
            // Arrange
            var loader = CreateLoader();

            // Act & Assert
            var exception = Assert.Throws<FileNotFoundException>(() => loader.LoadConfig("nonexistent"));
            exception.Message.Should().Contain("No se encontro configuracion publicada para tipologia 'nonexistent'");
        }

        [Fact]
        public void LoadConfig_InvalidJson_ThrowsJsonException()
        {
            // Arrange
            var configPath = Path.Combine(_tempDirectory, "invalid.validation.json");
            var invalidJson = "{ invalid json }";
            File.WriteAllText(configPath, invalidJson);

            var loader = CreateLoader();

            // Act & Assert
            Assert.Throws<System.Text.Json.JsonException>(() => loader.LoadConfig("invalid"));
        }

        [Fact]
        public void LoadConfig_NullDeserializedObject_ThrowsInvalidDataException()
        {
            // Arrange
            var configPath = Path.Combine(_tempDirectory, "null.validation.json");
            File.WriteAllText(configPath, "null");

            var loader = CreateLoader();

            // Act & Assert
            Assert.Throws<InvalidDataException>(() => loader.LoadConfig("null"));
        }

        [Fact]
        public void LoadConfig_CaseInsensitiveProperties_DeserializesCorrectly()
        {
            // Arrange
            var configPath = Path.Combine(_tempDirectory, "casetest.validation.json");
            var jsonContent = @"{
                ""tipologiaID"": ""test"",
                ""TipologiaNombre"": ""Test"",
                ""VERSION"": ""1.0"",
                ""fields"": []
            }";
            File.WriteAllText(configPath, jsonContent);

            var loader = CreateLoader();

            // Act
            var config = loader.LoadConfig("casetest");

            // Assert
            config.TipologiaId.Should().Be("test");
            config.Version.Should().Be("1.0");
        }

        [Fact]
        public void LoadConfig_EmptyFields_ReturnsEmptyFieldsList()
        {
            // Arrange
            var configPath = Path.Combine(_tempDirectory, "empty.validation.json");
            var jsonContent = @"{
                ""tipologiaId"": ""empty"",
                ""tipologiaNombre"": ""Empty"",
                ""version"": ""1.0"",
                ""fields"": []
            }";
            File.WriteAllText(configPath, jsonContent);

            var loader = CreateLoader();

            // Act
            var config = loader.LoadConfig("empty");

            // Assert
            config.Fields.Should().BeEmpty();
        }

        #endregion

        #region BuildValidationEngine - Basic Tests

        [Fact]
        public void BuildValidationEngine_SimpleRequiredField_CreatesRequiredFieldValidator()
        {
            // Arrange
            var configPath = Path.Combine(_tempDirectory, "simple.validation.json");
            var jsonContent = @"{
                ""tipologiaId"": ""simple"",
                ""tipologiaNombre"": ""Simple"",
                ""version"": ""1.0"",
                ""fields"": [
                    {
                        ""name"": ""Nombre"",
                        ""type"": ""string"",
                        ""required"": true,
                        ""rules"": []
                    }
                ]
            }";
            File.WriteAllText(configPath, jsonContent);

            var loader = CreateLoader();

            // Act
            var engine = loader.BuildValidationEngine("simple");

            // Assert
            engine.Should().NotBeNull();
            var data = new Dictionary<string, object?>();
            var report = engine.ValidateDocument(data);
            report.IsValid.Should().BeFalse();
            report.ErrorCount.Should().Be(1);
        }

        [Fact]
        public void BuildValidationEngine_BooleanField_AddsBooleanValidator()
        {
            // Arrange
            var configPath = Path.Combine(_tempDirectory, "bool.validation.json");
            var jsonContent = @"{
                ""tipologiaId"": ""bool"",
                ""tipologiaNombre"": ""Boolean Test"",
                ""version"": ""1.0"",
                ""fields"": [
                    {
                        ""name"": ""Activo"",
                        ""type"": ""boolean"",
                        ""required"": false,
                        ""rules"": []
                    }
                ]
            }";
            File.WriteAllText(configPath, jsonContent);

            var loader = CreateLoader();

            // Act
            var engine = loader.BuildValidationEngine("bool");

            // Assert
            var data = new Dictionary<string, object?> { { "Activo", true } };
            var report = engine.ValidateDocument(data);
            report.IsValid.Should().BeTrue();
        }

        [Fact]
        public void BuildValidationEngine_NoRulesField_OnlyRequiredValidation()
        {
            // Arrange
            var configPath = Path.Combine(_tempDirectory, "norules.validation.json");
            var jsonContent = @"{
                ""tipologiaId"": ""norules"",
                ""tipologiaNombre"": ""No Rules"",
                ""version"": ""1.0"",
                ""fields"": [
                    {
                        ""name"": ""Campo"",
                        ""type"": ""string"",
                        ""required"": false,
                        ""rules"": []
                    }
                ]
            }";
            File.WriteAllText(configPath, jsonContent);

            var loader = CreateLoader();

            // Act
            var engine = loader.BuildValidationEngine("norules");

            // Assert
            var data = new Dictionary<string, object?> { { "Campo", "" } };
            var report = engine.ValidateDocument(data);
            report.IsValid.Should().BeTrue();
        }

        #endregion

        #region BuildValidationEngine - Rule Type Tests

        [Fact]
        public void BuildValidationEngine_LengthRules_CreatesLengthValidator()
        {
            // Arrange
            var configPath = Path.Combine(_tempDirectory, "length.validation.json");
            var jsonContent = @"{
                ""tipologiaId"": ""length"",
                ""tipologiaNombre"": ""Length Test"",
                ""version"": ""1.0"",
                ""fields"": [
                    {
                        ""name"": ""Titulo"",
                        ""type"": ""string"",
                        ""required"": true,
                        ""rules"": [
                            {
                                ""ruleType"": ""minlength"",
                                ""severity"": ""Error"",
                                ""parameters"": { ""value"": 5 }
                            },
                            {
                                ""ruleType"": ""maxlength"",
                                ""severity"": ""Error"",
                                ""parameters"": { ""value"": 100 }
                            }
                        ]
                    }
                ]
            }";
            File.WriteAllText(configPath, jsonContent);

            var loader = CreateLoader();

            // Act
            var engine = loader.BuildValidationEngine("length");

            // Assert
            var data = new Dictionary<string, object?> { { "Titulo", "AB" } };
            var report = engine.ValidateDocument(data);
            report.IsValid.Should().BeFalse();
            report.ErrorCount.Should().BeGreaterThanOrEqualTo(1);
        }

        [Fact]
        public void BuildValidationEngine_EnumRule_CreatesEnumValidator()
        {
            // Arrange
            var configPath = Path.Combine(_tempDirectory, "enum.validation.json");
            var jsonContent = @"{
                ""tipologiaId"": ""enum"",
                ""tipologiaNombre"": ""Enum Test"",
                ""version"": ""1.0"",
                ""fields"": [
                    {
                        ""name"": ""Estado"",
                        ""type"": ""string"",
                        ""required"": true,
                        ""rules"": [
                            {
                                ""ruleType"": ""enum"",
                                ""severity"": ""Error"",
                                ""parameters"": {
                                    ""values"": [""Activo"", ""Inactivo"", ""Pendiente""]
                                }
                            }
                        ]
                    }
                ]
            }";
            File.WriteAllText(configPath, jsonContent);

            var loader = CreateLoader();

            // Act
            var engine = loader.BuildValidationEngine("enum");

            // Assert - Valid value
            var validData = new Dictionary<string, object?> { { "Estado", "Activo" } };
            var validReport = engine.ValidateDocument(validData);
            validReport.IsValid.Should().BeTrue();

            // Invalid value
            var invalidData = new Dictionary<string, object?> { { "Estado", "Invalid" } };
            var invalidReport = engine.ValidateDocument(invalidData);
            invalidReport.IsValid.Should().BeFalse();
        }

        [Fact]
        public void BuildValidationEngine_RegexRule_CreatesRegexValidator()
        {
            // Arrange
            var configPath = Path.Combine(_tempDirectory, "regex.validation.json");
            var jsonContent = @"{
                ""tipologiaId"": ""regex"",
                ""tipologiaNombre"": ""Regex Test"",
                ""version"": ""1.0"",
                ""fields"": [
                    {
                        ""name"": ""CodigoPostal"",
                        ""type"": ""string"",
                        ""required"": true,
                        ""rules"": [
                            {
                                ""ruleType"": ""regex"",
                                ""severity"": ""Error"",
                                ""parameters"": { ""pattern"": ""^\\d{5}$"" }
                            }
                        ]
                    }
                ]
            }";
            File.WriteAllText(configPath, jsonContent);

            var loader = CreateLoader();

            // Act
            var engine = loader.BuildValidationEngine("regex");

            // Assert
            var validData = new Dictionary<string, object?> { { "CodigoPostal", "28001" } };
            var validReport = engine.ValidateDocument(validData);
            validReport.IsValid.Should().BeTrue();

            var invalidData = new Dictionary<string, object?> { { "CodigoPostal", "280A1" } };
            var invalidReport = engine.ValidateDocument(invalidData);
            invalidReport.IsValid.Should().BeFalse();
        }

        [Fact]
        public void BuildValidationEngine_RangeRule_CreatesRangeValidator()
        {
            // Arrange
            var configPath = Path.Combine(_tempDirectory, "range.validation.json");
            var jsonContent = @"{
                ""tipologiaId"": ""range"",
                ""tipologiaNombre"": ""Range Test"",
                ""version"": ""1.0"",
                ""fields"": [
                    {
                        ""name"": ""Edad"",
                        ""type"": ""decimal"",
                        ""required"": true,
                        ""rules"": [
                            {
                                ""ruleType"": ""range"",
                                ""severity"": ""Error"",
                                ""parameters"": { ""min"": 0, ""max"": 150 }
                            }
                        ]
                    }
                ]
            }";
            File.WriteAllText(configPath, jsonContent);

            var loader = CreateLoader();

            // Act
            var engine = loader.BuildValidationEngine("range");

            // Assert
            var validData = new Dictionary<string, object?> { { "Edad", "25" } };
            var validReport = engine.ValidateDocument(validData);
            validReport.IsValid.Should().BeTrue();

            var invalidData = new Dictionary<string, object?> { { "Edad", "200" } };
            var invalidReport = engine.ValidateDocument(invalidData);
            invalidReport.IsValid.Should().BeFalse();
        }

        [Fact]
        public void BuildValidationEngine_DateRule_CreatesDateFormatValidator()
        {
            // Arrange
            var configPath = Path.Combine(_tempDirectory, "date.validation.json");
            var jsonContent = @"{
                ""tipologiaId"": ""date"",
                ""tipologiaNombre"": ""Date Test"",
                ""version"": ""1.0"",
                ""fields"": [
                    {
                        ""name"": ""Nacimiento"",
                        ""type"": ""date"",
                        ""required"": true,
                        ""rules"": [
                            {
                                ""ruleType"": ""date"",
                                ""severity"": ""Error"",
                                ""parameters"": {
                                    ""formats"": [""dd/MM/yyyy"", ""yyyy-MM-dd""],
                                    ""allowFuture"": false,
                                    ""allowPast"": true
                                }
                            }
                        ]
                    }
                ]
            }";
            File.WriteAllText(configPath, jsonContent);

            var loader = CreateLoader();

            // Act
            var engine = loader.BuildValidationEngine("date");

            // Assert
            var validData = new Dictionary<string, object?> { { "Nacimiento", "15/03/1990" } };
            var validReport = engine.ValidateDocument(validData);
            validReport.IsValid.Should().BeTrue();

            var invalidData = new Dictionary<string, object?> { { "Nacimiento", "not-a-date" } };
            var invalidReport = engine.ValidateDocument(invalidData);
            invalidReport.IsValid.Should().BeFalse();
        }

        #endregion

        #region BuildValidationEngine - Severity Tests

        [Fact]
        public void BuildValidationEngine_SeverityError_StopsValidation()
        {
            // Arrange
            var configPath = Path.Combine(_tempDirectory, "severity_error.validation.json");
            var jsonContent = @"{
                ""tipologiaId"": ""severity_error"",
                ""tipologiaNombre"": ""Severity Error"",
                ""version"": ""1.0"",
                ""fields"": [
                    {
                        ""name"": ""Campo"",
                        ""type"": ""string"",
                        ""required"": false,
                        ""rules"": [
                            {
                                ""ruleType"": ""minlength"",
                                ""severity"": ""Error"",
                                ""parameters"": { ""value"": 5 }
                            }
                        ]
                    }
                ]
            }";
            File.WriteAllText(configPath, jsonContent);

            var loader = CreateLoader();

            // Act
            var engine = loader.BuildValidationEngine("severity_error");

            // Assert
            var data = new Dictionary<string, object?> { { "Campo", "AB" } };
            var report = engine.ValidateDocument(data);
            report.IsValid.Should().BeFalse();
            report.ErrorCount.Should().Be(1);
        }

        [Fact]
        public void BuildValidationEngine_SeverityWarning_ContinuesValidation()
        {
            // Arrange
            var configPath = Path.Combine(_tempDirectory, "severity_warning.validation.json");
            var jsonContent = @"{
                ""tipologiaId"": ""severity_warning"",
                ""tipologiaNombre"": ""Severity Warning"",
                ""version"": ""1.0"",
                ""fields"": [
                    {
                        ""name"": ""Campo"",
                        ""type"": ""string"",
                        ""required"": false,
                        ""rules"": [
                            {
                                ""ruleType"": ""minlength"",
                                ""severity"": ""Warning"",
                                ""parameters"": { ""value"": 5 }
                            }
                        ]
                    }
                ]
            }";
            File.WriteAllText(configPath, jsonContent);

            var loader = CreateLoader();

            // Act
            var engine = loader.BuildValidationEngine("severity_warning");

            // Assert
            var data = new Dictionary<string, object?> { { "Campo", "AB" } };
            var report = engine.ValidateDocument(data);
            report.IsValid.Should().BeTrue();
            report.WarningCount.Should().Be(1);
        }

        [Fact]
        public void BuildValidationEngine_MultipleSeverities_CountsCorrectly()
        {
            // Arrange
            var configPath = Path.Combine(_tempDirectory, "multi_severity.validation.json");
            var jsonContent = @"{
                ""tipologiaId"": ""multi_severity"",
                ""tipologiaNombre"": ""Multi Severity"",
                ""version"": ""1.0"",
                ""fields"": [
                    {
                        ""name"": ""Campo1"",
                        ""type"": ""string"",
                        ""required"": false,
                        ""rules"": [
                            {
                                ""ruleType"": ""minlength"",
                                ""severity"": ""Warning"",
                                ""parameters"": { ""value"": 5 }
                            }
                        ]
                    },
                    {
                        ""name"": ""Campo2"",
                        ""type"": ""string"",
                        ""required"": false,
                        ""rules"": [
                            {
                                ""ruleType"": ""minlength"",
                                ""severity"": ""Error"",
                                ""parameters"": { ""value"": 5 }
                            }
                        ]
                    }
                ]
            }";
            File.WriteAllText(configPath, jsonContent);

            var loader = CreateLoader();

            // Act
            var engine = loader.BuildValidationEngine("multi_severity");

            // Assert
            var data = new Dictionary<string, object?> { { "Campo1", "AB" }, { "Campo2", "CD" } };
            var report = engine.ValidateDocument(data);
            report.IsValid.Should().BeFalse();
            report.ErrorCount.Should().Be(1);
            report.WarningCount.Should().Be(1);
        }

        #endregion

        #region BuildValidationEngine - Array Tests

        [Fact]
        public void BuildValidationEngine_ArrayField_CreatesArrayValidator()
        {
            // Arrange
            var configPath = Path.Combine(_tempDirectory, "array.validation.json");
            var jsonContent = @"{
                ""tipologiaId"": ""array"",
                ""tipologiaNombre"": ""Array Test"",
                ""version"": ""1.0"",
                ""fields"": [
                    {
                        ""name"": ""Items"",
                        ""type"": ""array"",
                        ""required"": false,
                        ""items"": {
                            ""type"": ""object"",
                            ""properties"": [
                                {
                                    ""name"": ""Nombre"",
                                    ""type"": ""string"",
                                    ""required"": true,
                                    ""rules"": []
                                }
                            ]
                        }
                    }
                ]
            }";
            File.WriteAllText(configPath, jsonContent);

            var loader = CreateLoader();

            // Act
            var engine = loader.BuildValidationEngine("array");

            // Assert
            engine.Should().NotBeNull();
        }

        #endregion

        #region BuildValidationEngine - Address Tests

        [Fact]
        public void BuildValidationEngine_AddressRule_CreatesAddressValidatorWithDefaults()
        {
            // Arrange
            var configPath = Path.Combine(_tempDirectory, "address.validation.json");
            var jsonContent = @"{
                ""tipologiaId"": ""address"",
                ""tipologiaNombre"": ""Address Test"",
                ""version"": ""1.0"",
                ""fields"": [
                    {
                        ""name"": ""Direccion"",
                        ""type"": ""string"",
                        ""required"": true,
                        ""rules"": [
                            {
                                ""ruleType"": ""address"",
                                ""severity"": ""Error"",
                                ""parameters"": {}
                            }
                        ]
                    }
                ]
            }";
            File.WriteAllText(configPath, jsonContent);

            var loader = CreateLoader();

            // Act
            var engine = loader.BuildValidationEngine("address");

            // Assert
            engine.Should().NotBeNull();
        }

        [Fact]
        public void BuildValidationEngine_AddressRuleWithParameters_AppliesCustomSettings()
        {
            // Arrange
            var configPath = Path.Combine(_tempDirectory, "address_custom.validation.json");
            var jsonContent = @"{
                ""tipologiaId"": ""address_custom"",
                ""tipologiaNombre"": ""Address Custom"",
                ""version"": ""1.0"",
                ""fields"": [
                    {
                        ""name"": ""Direccion"",
                        ""type"": ""string"",
                        ""required"": true,
                        ""rules"": [
                            {
                                ""ruleType"": ""address"",
                                ""severity"": ""Error"",
                                ""parameters"": {
                                    ""minLength"": 10,
                                    ""maxLength"": 200,
                                    ""requireStreetNumber"": true,
                                    ""requireMunicipality"": false,
                                    ""requireProvince"": false
                                }
                            }
                        ]
                    }
                ]
            }";
            File.WriteAllText(configPath, jsonContent);

            var loader = CreateLoader();

            // Act
            var engine = loader.BuildValidationEngine("address_custom");

            // Assert
            engine.Should().NotBeNull();
        }

        #endregion

        #region BuildValidationEngine - Error Cases

        [Fact]
        public void BuildValidationEngine_UnsupportedRuleType_ThrowsNotSupportedException()
        {
            // Arrange
            var configPath = Path.Combine(_tempDirectory, "unsupported.validation.json");
            var jsonContent = @"{
                ""tipologiaId"": ""unsupported"",
                ""tipologiaNombre"": ""Unsupported"",
                ""version"": ""1.0"",
                ""fields"": [
                    {
                        ""name"": ""Campo"",
                        ""type"": ""string"",
                        ""required"": false,
                        ""rules"": [
                            {
                                ""ruleType"": ""unknownrule"",
                                ""severity"": ""Error"",
                                ""parameters"": {}
                            }
                        ]
                    }
                ]
            }";
            File.WriteAllText(configPath, jsonContent);

            var loader = CreateLoader();

            // Act & Assert
            var exception = Assert.Throws<NotSupportedException>(() => loader.BuildValidationEngine("unsupported"));
            exception.Message.Should().Contain("unknownrule");
            exception.Message.Should().Contain("no soportado");
        }

        [Fact]
        public void BuildValidationEngine_InvalidSeverity_IgnoresAndUsesDefault()
        {
            // Arrange
            var configPath = Path.Combine(_tempDirectory, "invalid_severity.validation.json");
            var jsonContent = @"{
                ""tipologiaId"": ""invalid_severity"",
                ""tipologiaNombre"": ""Invalid Severity"",
                ""version"": ""1.0"",
                ""fields"": [
                    {
                        ""name"": ""Campo"",
                        ""type"": ""string"",
                        ""required"": false,
                        ""rules"": [
                            {
                                ""ruleType"": ""minlength"",
                                ""severity"": ""InvalidSeverity"",
                                ""parameters"": { ""value"": 5 }
                            }
                        ]
                    }
                ]
            }";
            File.WriteAllText(configPath, jsonContent);

            var loader = CreateLoader();

            // Act
            var engine = loader.BuildValidationEngine("invalid_severity");

            // Assert
            engine.Should().NotBeNull();
        }

        #endregion

        private TipologiaConfigLoader CreateLoader()
        {
            var repository = new Mock<ITipologiaRepository>();
            repository
                .Setup(x => x.GetByCodigoAsync(It.IsAny<string>()))
                .ReturnsAsync((string codigo) =>
                {
                    var configPath = Path.Combine(_tempDirectory, $"{codigo}.validation.json");
                    if (!File.Exists(configPath))
                    {
                        return null;
                    }

                    return new TipologiaEntity
                    {
                        Codigo = codigo,
                        Activa = true,
                        Estado = EstadoTipologia.Published,
                        ConfiguracionJson = File.ReadAllText(configPath)
                    };
                });

            var provider = new Mock<IServiceProvider>();
            provider
                .Setup(x => x.GetService(typeof(ITipologiaRepository)))
                .Returns(repository.Object);

            var scope = new Mock<IServiceScope>();
            scope.SetupGet(x => x.ServiceProvider).Returns(provider.Object);

            var scopeFactory = new Mock<IServiceScopeFactory>();
            scopeFactory.Setup(x => x.CreateScope()).Returns(scope.Object);

            var cache = new MemoryCache(new MemoryCacheOptions());
            return new TipologiaConfigLoader(cache, scopeFactory.Object);
        }

        #region Cleanup

        public void Dispose()
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }

        #endregion
    }
}

