using System.Collections.Generic;
using DocumentIA.Core.Validation;
using DocumentIA.Core.Validation.Models;
using DocumentIA.Core.Validation.Rules;
using FluentAssertions;
using Xunit;


#nullable enable

namespace DocumentIA.Tests.Unit.Validation
{
    public class ValidationEngineTests
    {
        [Fact]
        public void ValidateDocument_NoRules_ReturnsValidReport()
        {
            var engine = new ValidationEngine();
            var data = new Dictionary<string, object?> { { "Campo", "valor" } };

            var report = engine.ValidateDocument(data);

            report.IsValid.Should().BeTrue();
            report.Results.Should().BeEmpty();
            report.ErrorCount.Should().Be(0);
        }

        [Fact]
        public void ValidateDocument_MissingRequiredField_ReturnsError()
        {
            var engine = new ValidationEngine()
                .AddRule("Campo", new RequiredFieldValidator());

            var data = new Dictionary<string, object?>();

            var report = engine.ValidateDocument(data);

            report.IsValid.Should().BeFalse();
            report.Results.Should().HaveCount(1);
            report.ErrorCount.Should().Be(1);
            report.Results[0].FieldName.Should().Be("Campo");
        }

        [Fact]
        public void ValidateDocument_ValidField_ReturnsNoErrors()
        {
            var engine = new ValidationEngine()
                .AddRule("Campo", new RequiredFieldValidator());

            var data = new Dictionary<string, object?> { { "Campo", "ok" } };

            var report = engine.ValidateDocument(data);

            report.IsValid.Should().BeTrue();
            report.Results.Should().BeEmpty();
        }

        /// <summary>
        /// Tests para cascada de validadores (múltiples reglas en el mismo campo)
        /// </summary>

        [Fact]
        public void ValidateDocument_MultipleValidatorsOnSameField_AllPass()
        {
            // Arrange
            var engine = new ValidationEngine()
                .AddRule("NIF", new RequiredFieldValidator())
                .AddRule("NIF", new NifValidator());

            var data = new Dictionary<string, object?> { { "NIF", "12345678Z" } };

            // Act
            var report = engine.ValidateDocument(data);

            // Assert
            report.IsValid.Should().BeTrue();
            report.ErrorCount.Should().Be(0);
        }

        [Fact]
        public void ValidateDocument_MultipleValidatorsOnSameField_OneFailsRequired()
        {
            // Arrange
            var engine = new ValidationEngine()
                .AddRule("NIF", new RequiredFieldValidator())
                .AddRule("NIF", new NifValidator());

            var data = new Dictionary<string, object?>();

            // Act
            var report = engine.ValidateDocument(data);

            // Assert
            report.IsValid.Should().BeFalse();
            report.ErrorCount.Should().Be(1);
        }

        [Fact]
        public void ValidateDocument_MultipleValidatorsOnSameField_OneFailsNif()
        {
            // Arrange
            var engine = new ValidationEngine()
                .AddRule("NIF", new RequiredFieldValidator())
                .AddRule("NIF", new NifValidator());

            var data = new Dictionary<string, object?> { { "NIF", "12345678INVALID" } };

            // Act
            var report = engine.ValidateDocument(data);

            // Assert
            report.IsValid.Should().BeFalse();
            report.ErrorCount.Should().Be(1);
            report.Results[0].FieldName.Should().Be("NIF");
        }

        [Fact]
        public void ValidateDocument_MultipleFields_MixedValidationResults()
        {
            // Arrange - Simular validación de Nota Simple compleja
            var engine = new ValidationEngine()
                .AddRule("Numero", new RequiredFieldValidator())
                .AddRule("FechaTasacion", new RequiredFieldValidator())
                .AddRule("FechaTasacion", new DateFormatValidator())
                .AddRule("ValorTasado", new RangeValidator(min: 1000, max: 2000000))
                .AddRule("ReferenciaCatastral", new CatastralReferenceValidator());

            var data = new Dictionary<string, object?>
            {
                { "Numero", "2025-001" },
                { "FechaTasacion", "31/12/2024" },
                { "ValorTasado", 50000 },
                { "ReferenciaCatastral", "1234567AB1234S0001ZX" }
            };

            // Act
            var report = engine.ValidateDocument(data);

            // Assert
            report.IsValid.Should().BeTrue();
            report.ErrorCount.Should().Be(0);
        }

        [Fact]
        public void ValidateDocument_MultipleFields_PartialFailures()
        {
            // Arrange
            var engine = new ValidationEngine()
                .AddRule("Numero", new RequiredFieldValidator())
                .AddRule("FechaTasacion", new RequiredFieldValidator())
                .AddRule("FechaTasacion", new DateFormatValidator())
                .AddRule("ValorTasado", new RangeValidator(min: 1000, max: 2000000));

            var data = new Dictionary<string, object?>
            {
                { "Numero", "2025-001" },
                { "FechaTasacion", "fecha invalida" },  // Falló en formato
                { "ValorTasado", 100 }  // Falló: menor que mínimo
            };

            // Act
            var report = engine.ValidateDocument(data);

            // Assert
            report.IsValid.Should().BeFalse();
            report.ErrorCount.Should().Be(2);
            report.WarningCount.Should().Be(0);
        }

        [Fact]
        public void ValidateDocument_ComplexDocument_AllFieldsValid()
        {
            // Arrange - Representar documento Nota Simple 1.3 completo
            var engine = new ValidationEngine()
                .AddRule("TipoDocumento", new RequiredFieldValidator())
                .AddRule("Numero", new RequiredFieldValidator())
                .AddRule("FechaTasacion", new DateFormatValidator(allowFutureDates: false))
                .AddRule("ReferenciaCatastral", new CatastralReferenceValidator())
                .AddRule("DireccionPropiedad", new AddressValidator(requireStreetNumber: true))
                .AddRule("NifTasador", new NifValidator())
                .AddRule("ValorTasado", new RangeValidator(min: 5000, max: 500000));

            var data = new Dictionary<string, object?>
            {
                { "TipoDocumento", "DN_301" },
                { "Numero", "ES-000123/2025" },
                { "FechaTasacion", "24/02/2025" },
                { "ReferenciaCatastral", "7949211YJ2874N0025RA" },
                { "DireccionPropiedad", "Calle Mayor 15, 28013 Madrid" },
                { "NifTasador", "12345678Z" },
                { "ValorTasado", 150000 }
            };

            // Act
            var report = engine.ValidateDocument(data);

            // Assert
            report.IsValid.Should().BeTrue();
            report.ErrorCount.Should().Be(0);
            report.WarningCount.Should().Be(0);
        }

        [Fact]
        public void ValidateDocument_ContextPassing_BetweenValidators()
        {
            // Arrange
            var engine = new ValidationEngine()
                .AddRule("Municipio", new RequiredFieldValidator())
                .AddRule("DireccionPropiedad", new AddressValidator(
                    requireMunicipality: true,
                    requireStreetNumber: true));

            var data = new Dictionary<string, object?>
            {
                { "Municipio", "Madrid" },
                { "DireccionPropiedad", "Calle Mayor 15, 28013 Madrid" }
            };

            var context = new Dictionary<string, object?>
            {
                { "municipality", "Madrid" }
            };

            // Act
            var report = engine.ValidateDocument(data, context);

            // Assert
            report.IsValid.Should().BeTrue();
        }

        [Fact]
        public void ValidateDocument_OptionalFields_NullValues_Accepted()
        {
            // Arrange
            var engine = new ValidationEngine()
                .AddRule("Numero", new RequiredFieldValidator())
                .AddRule("Observaciones", new LengthValidator(maxLength: 500));  // No required

            var data = new Dictionary<string, object?>
            {
                { "Numero", "2025-001" },
                { "Observaciones", null }  // Opcional
            };

            // Act
            var report = engine.ValidateDocument(data);

            // Assert
            report.IsValid.Should().BeTrue();
        }

        [Fact]
        public void ValidateDocument_EnumValidation_ValidEnum()
        {
            // Arrange
            var engine = new ValidationEngine()
                .AddRule("TipoDocumento", new EnumValidator(
                    new List<string> { "DN_301", "DN_302", "D_100" }));

            var data = new Dictionary<string, object?>
            {
                { "TipoDocumento", "DN_301" }
            };

            // Act
            var report = engine.ValidateDocument(data);

            // Assert
            report.IsValid.Should().BeTrue();
        }

        [Fact]
        public void ValidateDocument_EnumValidation_InvalidEnum()
        {
            // Arrange
            var engine = new ValidationEngine()
                .AddRule("TipoDocumento", new EnumValidator(
                    new List<string> { "DN_301", "DN_302", "D_100" }));

            var data = new Dictionary<string, object?>
            {
                { "TipoDocumento", "INVALID" }
            };

            // Act
            var report = engine.ValidateDocument(data);

            // Assert
            report.IsValid.Should().BeFalse();
            report.ErrorCount.Should().Be(1);
        }

        [Fact]
        public void ValidateDocument_RegexValidation_CatastralPattern()
        {
            // Arrange - Validar referencia catastral con regex (para 1.3)
            var catastralPattern = @"^[0-9]{7}[A-Z]{2}[0-9]{4}[A-Z]{1}[0-9]{4}[A-Z]{2}$";
            var engine = new ValidationEngine()
                .AddRule("ReferenciaCatastral", new RegexValidator(catastralPattern));

            var validRef = "7949211YJ2874N0025RA";
            var invalidRef = "7949211YJ2874N002RA";  // Falta un dígito

            // Act
            var validReport = engine.ValidateDocument(
                new Dictionary<string, object?> { { "ReferenciaCatastral", validRef } });
            var invalidReport = engine.ValidateDocument(
                new Dictionary<string, object?> { { "ReferenciaCatastral", invalidRef } });

            // Assert
            validReport.IsValid.Should().BeTrue();
            invalidReport.IsValid.Should().BeFalse();
        }

        [Fact]
        public void ValidateDocument_BooleanValidation()
        {
            // Arrange
            var engine = new ValidationEngine()
                .AddRule("EsActualizado", new BooleanValidator())
                .AddRule("ConDiscrepancias", new BooleanValidator());

            var data = new Dictionary<string, object?>
            {
                { "EsActualizado", true },
                { "ConDiscrepancias", "false" }  // String que será convertido
            };

            // Act
            var report = engine.ValidateDocument(data);

            // Assert
            report.IsValid.Should().BeTrue();
        }

        [Fact]
        public void ValidateDocument_LengthValidation_MinMax()
        {
            // Arrange
            var engine = new ValidationEngine()
                .AddRule("Notas", new LengthValidator(minLength: 10, maxLength: 500));

            var data1 = new Dictionary<string, object?>
            {
                { "Notas", "Una nota corta" }  // 14 caracteres - válida
            };

            var data2 = new Dictionary<string, object?>
            {
                { "Notas", "Muy corta" }  // 9 caracteres - inválida
            };

            // Act
            var report1 = engine.ValidateDocument(data1);
            var report2 = engine.ValidateDocument(data2);

            // Assert
            report1.IsValid.Should().BeTrue();
            report2.IsValid.Should().BeFalse();
        }

        [Fact]
        public void ValidateDocument_ComplexDocument_WithArrayField()
        {
            // Arrange - Documento con Anejos (array)
            var itemsConfig = new DocumentIA.Core.Configuration.ItemsConfig
            {
                Type = "object",
                Properties = new List<DocumentIA.Core.Configuration.FieldValidationConfig>
                {
                    new DocumentIA.Core.Configuration.FieldValidationConfig
                    {
                        Name = "Tipo",
                        Type = "string",
                        Required = false
                    }
                }
            };

            var engine = new ValidationEngine()
                .AddRule("Numero", new RequiredFieldValidator())
                .AddRule("Anejos", new ArrayValidator(itemsConfig));

            var data = new Dictionary<string, object?>
            {
                { "Numero", "2025-001" },
                { "Anejos", new List<Dictionary<string, object>>
                    {
                        new() { { "Tipo", "Documento" } },
                        new() { { "Tipo", "Foto" } }
                    }
                }
            };

            // Act
            var report = engine.ValidateDocument(data);

            // Assert
            report.IsValid.Should().BeTrue();
            report.ErrorCount.Should().Be(0);
        }

        [Fact]
        public void ValidateDocument_ErrorCountAndWarningCount()
        {
            // Arrange
            var engine = new ValidationEngine()
                .AddRule("Field1", new RequiredFieldValidator())
                .AddRule("Field2", new RequiredFieldValidator())
                .AddRule("Field3", new RequiredFieldValidator());

            var data = new Dictionary<string, object?>();

            // Act
            var report = engine.ValidateDocument(data);

            // Assert
            report.IsValid.Should().BeFalse();
            report.ErrorCount.Should().Be(3);
            report.WarningCount.Should().Be(0);
            report.Results.Should().HaveCount(3);
        }

        [Fact]
        public void ValidateDocument_AllValidatorsPass_CompleteCase()
        {
            // Arrange - Caso de uso real completo
            var engine = new ValidationEngine()
                .AddRule("TipoDocumento", new EnumValidator(new List<string> { "DN_301", "DN_302" }))
                .AddRule("Numero", new RequiredFieldValidator())
                .AddRule("Numero", new LengthValidator(minLength: 5, maxLength: 50))
                .AddRule("FechaTasacion", new DateFormatValidator(allowFutureDates: false))
                .AddRule("ValorTasado", new RangeValidator(min: 1000, max: 5000000))
                .AddRule("ReferenciaCatastral", new CatastralReferenceValidator())
                .AddRule("NifTasador", new NifValidator());

            var data = new Dictionary<string, object?>
            {
                { "TipoDocumento", "DN_301" },
                { "Numero", "2025-00001" },
                { "FechaTasacion", "24/02/2025" },
                { "ValorTasado", 250000 },
                { "ReferenciaCatastral", "7949211YJ2874N0025RA" },
                { "NifTasador", "12345678Z" }
            };

            // Act
            var report = engine.ValidateDocument(data);

            // Assert
            report.IsValid.Should().BeTrue();
            report.ErrorCount.Should().Be(0);
            report.Results.Should().BeEmpty();
        }
    }
}
