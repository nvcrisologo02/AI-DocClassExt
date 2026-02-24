using System.Collections.Generic;
using System.Text.Json;
using DocumentIA.Core.Configuration;
using DocumentIA.Core.Validation;
using DocumentIA.Core.Validation.Rules;
using DocumentIA.Core.Validation.Models;
using FluentAssertions;
using Xunit;


#nullable disable

namespace DocumentIA.Tests.Unit.Validation
{
    public class ArrayValidatorTests
    {
        /// <summary>
        /// Tests para Arrays que contienen objetos anidados (ej: Anejos en Nota Simple)
        /// </summary>
        /// 
        [Fact]
        public void Validate_ValidArrayOfObjects_ReturnsValid()
        {
            // Arrange
            var itemsConfig = new ItemsConfig
            {
                Type = "object",
                Properties = new List<FieldValidationConfig>
                {
                    new FieldValidationConfig
                    {
                        Name = "Descripcion",
                        Type = "string",
                        Required = true,
                        Rules = new List<ValidationRuleConfig>()
                    }
                }
            };

            var validator = new ArrayValidator(itemsConfig);

            var arrayData = new List<Dictionary<string, object>>
            {
                new() { { "Descripcion", "Anejo 1 - Documento" } },
                new() { { "Descripcion", "Anejo 2 - Foto" } }
            };

            // Act
            var result = validator.Validate("Anejos", arrayData);

            // Assert
            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_ValidJsonArrayString_ReturnsValid()
        {
            // Arrange
            var itemsConfig = new ItemsConfig
            {
                Type = "object",
                Properties = new List<FieldValidationConfig>
                {
                    new FieldValidationConfig
                    {
                        Name = "Tipo",
                        Type = "string",
                        Required = false
                    }
                }
            };

            var validator = new ArrayValidator(itemsConfig);
            var jsonArray = "[{\"Tipo\":\"Foto\"},{\"Tipo\":\"Documento\"}]";

            // Act
            var result = validator.Validate("Anejos", jsonArray);

            // Assert
            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_EmptyArray_ReturnsValid()
        {
            // Arrange
            var itemsConfig = new ItemsConfig { Type = "object" };
            var validator = new ArrayValidator(itemsConfig);

            // Act
            var result = validator.Validate("Anejos", new List<object>());

            // Assert
            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_NullValue_ReturnsValid()
        {
            // Arrange
            var itemsConfig = new ItemsConfig { Type = "object" };
            var validator = new ArrayValidator(itemsConfig);

            // Act
            var result = validator.Validate("Anejos", null);

            // Assert
            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_InvalidJsonString_ReturnsInvalid()
        {
            // Arrange
            var itemsConfig = new ItemsConfig { Type = "object" };
            var validator = new ArrayValidator(itemsConfig);
            var invalidJson = "not a valid json";

            // Act
            var result = validator.Validate("Anejos", invalidJson);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Message.Should().Contain("colección JSON válida");
        }

        [Fact]
        public void Validate_ArrayWithStringItems_ValidatesEachItem()
        {
            // Arrange
            var itemsConfig = new ItemsConfig { Type = "string" };
            var validator = new ArrayValidator(itemsConfig);

            var items = new List<object>
            {
                "Item 1",
                "Item 2",
                "Item 3"
            };

            // Act
            var result = validator.Validate("Tags", items);

            // Assert
            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_JsonElementArray_ReturnsValid()
        {
            // Arrange
            var itemsConfig = new ItemsConfig { Type = "object" };
            var validator = new ArrayValidator(itemsConfig);

            var json = @"[
                { ""id"": 1, ""name"": ""Anejo 1"" },
                { ""id"": 2, ""name"": ""Anejo 2"" }
            ]";

            var jsonElement = JsonSerializer.Deserialize<JsonElement>(json);

            // Act
            var result = validator.Validate("Anejos", jsonElement);

            // Assert
            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_ArrayWithValidTypes_ReturnsValid()
        {
            // Arrange - Validar que arrays de tipos variados se aceptan
            var itemsConfig = new ItemsConfig
            {
                Type = "object",
                Properties = new List<FieldValidationConfig>
                {
                    new FieldValidationConfig
                    {
                        Name = "Tipo",
                        Type = "string",
                        Required = true,
                        Rules = new List<ValidationRuleConfig>()
                    }
                }
            };

            var validator = new ArrayValidator(itemsConfig);

            var arrayData = new List<Dictionary<string, object>>
            {
                new() { { "Tipo", "Foto" } },
                new() { { "Tipo", "Documento" } }
            };

            // Act
            var result = validator.Validate("Anejos", arrayData);

            // Assert
            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_NestedArrayStructure_WithComplexData()
        {
            // Arrange - Simular Anejos con múltiples propiedades como en una tipología real
            var itemsConfig = new ItemsConfig
            {
                Type = "object",
                Properties = new List<FieldValidationConfig>
                {
                    new FieldValidationConfig
                    {
                        Name = "Tipo",
                        Type = "string",
                        Required = true
                    },
                    new FieldValidationConfig
                    {
                        Name = "Paginas",
                        Type = "integer",
                        Required = false
                    },
                    new FieldValidationConfig
                    {
                        Name = "Descripcion",
                        Type = "string",
                        Required = false
                    }
                }
            };

            var validator = new ArrayValidator(itemsConfig);

            var anejos = JsonSerializer.Deserialize<JsonElement>(
                @"[
                    { ""Tipo"": ""Anejos"", ""Paginas"": 5, ""Descripcion"": ""Documentación adjunta"" },
                    { ""Tipo"": ""Croquis"", ""Paginas"": 1, ""Descripcion"": ""Plano de ubicación"" }
                ]");

            // Act
            var result = validator.Validate("Anejos", anejos);

            // Assert
            result.IsValid.Should().BeTrue();
            result.FieldName.Should().Be("Anejos");
        }

        [Fact]
        public void Validate_ArrayWithNonObjectType_HandlesStringArray()
        {
            // Arrange
            var itemsConfig = new ItemsConfig { Type = "string" };
            var validator = new ArrayValidator(itemsConfig);

            var jsonArray = "[\"valor1\", \"valor2\", \"valor3\"]";

            // Act
            var result = validator.Validate("StringArray", jsonArray);

            // Assert
            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_LargeArray_ProcessesAllItems()
        {
            // Arrange
            var itemsConfig = new ItemsConfig { Type = "object" };
            var validator = new ArrayValidator(itemsConfig);

            // Crear array grande
            var largeArray = new List<Dictionary<string, object>>();
            for (int i = 0; i < 100; i++)
            {
                largeArray.Add(new() { { "Index", i } });
            }

            // Act
            var result = validator.Validate("LargeArray", largeArray);

            // Assert
            result.IsValid.Should().BeTrue();
        }
    }
}
