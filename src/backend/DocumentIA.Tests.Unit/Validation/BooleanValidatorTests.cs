using DocumentIA.Core.Validation.Rules;
using FluentAssertions;
using Xunit;

namespace DocumentIA.Tests.Unit.Validation
{
    public class BooleanValidatorTests
    {
        /// <summary>
        /// Tests para validador booleano
        /// Cubre: bool nativo, conversiones de string, variantes (sí/no, 1/0, true/false)
        /// </summary>

        #region Constructor y Configuración

        [Fact]
        public void Constructor_Succeeds()
        {
            // Arrange & Act
            var validator = new BooleanValidator();

            // Assert
            validator.RuleName.Should().Be("BooleanValidator");
        }

        #endregion

        #region Valores Booleanos Nativos

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Validate_NativeBoolValue_ReturnsValid(bool value)
        {
            // Arrange
            var validator = new BooleanValidator();

            // Act
            var result = validator.Validate("Flag", value);

            // Assert
            result.IsValid.Should().BeTrue();
        }

        #endregion

        #region Conversión desde String - Estándar

        [Theory]
        [InlineData("true")]
        [InlineData("True")]
        [InlineData("TRUE")]
        [InlineData("false")]
        [InlineData("False")]
        [InlineData("FALSE")]
        public void Validate_StandardBooleanString_ReturnsValid(string value)
        {
            // Arrange
            var validator = new BooleanValidator();

            // Act
            var result = validator.Validate("Flag", value);

            // Assert
            result.IsValid.Should().BeTrue();
        }

        #endregion

        #region Conversión desde String - Variantes Numéricas

        [Theory]
        [InlineData("1")]
        [InlineData("0")]
        public void Validate_NumericStringVariant_ReturnsValid(string value)
        {
            // Arrange
            var validator = new BooleanValidator();

            // Act
            var result = validator.Validate("Flag", value);

            // Assert
            result.IsValid.Should().BeTrue();
        }

        #endregion

        #region Conversión desde String - Variantes Españolas

        [Theory]
        [InlineData("sí")]
        [InlineData("Sí")]
        [InlineData("SÍ")]
        [InlineData("si")]
        [InlineData("Si")]
        [InlineData("SI")]
        [InlineData("no")]
        [InlineData("No")]
        [InlineData("NO")]
        public void Validate_SpanishVariant_ReturnsValid(string value)
        {
            // Arrange
            var validator = new BooleanValidator();

            // Act
            var result = validator.Validate("Flag", value);

            // Assert
            result.IsValid.Should().BeTrue();
        }

        #endregion

        #region Conversión desde String - Variantes Inglesas

        [Theory]
        [InlineData("yes")]
        [InlineData("Yes")]
        [InlineData("YES")]
        [InlineData("no")]
        [InlineData("No")]
        [InlineData("NO")]
        public void Validate_EnglishVariant_ReturnsValid(string value)
        {
            // Arrange
            var validator = new BooleanValidator();

            // Act
            var result = validator.Validate("Flag", value);

            // Assert
            result.IsValid.Should().BeTrue();
        }

        #endregion

        #region Conversión desde String - Variantes Largas

        [Theory]
        [InlineData("verdadero")]
        [InlineData("Verdadero")]
        [InlineData("VERDADERO")]
        [InlineData("falso")]
        [InlineData("Falso")]
        [InlineData("FALSO")]
        public void Validate_LongVariant_ReturnsValid(string value)
        {
            // Arrange
            var validator = new BooleanValidator();

            // Act
            var result = validator.Validate("Flag", value);

            // Assert
            result.IsValid.Should().BeTrue();
        }

        #endregion

        #region Null y Valores Especiales

        [Fact]
        public void Validate_NullValue_ReturnsValid()
        {
            // Arrange
            var validator = new BooleanValidator();

            // Act
            var result = validator.Validate("Flag", null);

            // Assert
            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_EmptyString_ReturnsValid()
        {
            // Arrange
            var validator = new BooleanValidator();

            // Act
            var result = validator.Validate("Flag", "");

            // Assert
            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_WhitespaceOnly_ReturnsValid()
        {
            // Arrange
            var validator = new BooleanValidator();

            // Act
            var result = validator.Validate("Flag", "   ");

            // Assert
            result.IsValid.Should().BeTrue();
        }

        #endregion

        #region Valores Inválidos

        [Theory]
        [InlineData("maybe")]
        [InlineData("unknown")]
        [InlineData("perhaps")]
        [InlineData("talvez")]
        [InlineData("quizá")]
        [InlineData("2")]
        [InlineData("-1")]
        [InlineData("true false")]
        [InlineData("not a bool")]
        public void Validate_InvalidValue_ReturnsInvalid(string value)
        {
            // Arrange
            var validator = new BooleanValidator();

            // Act
            var result = validator.Validate("Flag", value);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Message.Should().Contain("no es un booleano válido");
        }

        #endregion

        #region ErrorMessages

        [Fact]
        public void Validate_InvalidValue_ErrorMessageIncludesValue()
        {
            // Arrange
            var validator = new BooleanValidator();

            // Act
            var result = validator.Validate("Flag", "invalid_value");

            // Assert
            result.IsValid.Should().BeFalse();
            result.Message.Should().Contain("invalid_value");
        }

        [Fact]
        public void Validate_InvalidValue_ErrorMessageIncludesValidOptions()
        {
            // Arrange
            var validator = new BooleanValidator();

            // Act
            var result = validator.Validate("Flag", "maybe");

            // Assert
            result.IsValid.Should().BeFalse();
            result.SuggestionString.Should().Contain("true");
            result.SuggestionString.Should().Contain("false");
        }

        #endregion

        #region Casos de Uso Reales - Flags Booleanos

        [Fact]
        public void Validate_VPOProperty_Flag()
        {
            // Arrange - VPO (Vivienda de Protección Oficial)
            var validator = new BooleanValidator();

            var validTrue1 = validator.Validate("EsVPO", true);
            var validTrue2 = validator.Validate("EsVPO", "true");
            var validTrue3 = validator.Validate("EsVPO", "sí");
            var validTrue4 = validator.Validate("EsVPO", "1");

            var validFalse1 = validator.Validate("EsVPO", false);
            var validFalse2 = validator.Validate("EsVPO", "false");
            var validFalse3 = validator.Validate("EsVPO", "no");
            var validFalse4 = validator.Validate("EsVPO", "0");

            // Assert
            validTrue1.IsValid.Should().BeTrue();
            validTrue2.IsValid.Should().BeTrue();
            validTrue3.IsValid.Should().BeTrue();
            validTrue4.IsValid.Should().BeTrue();

            validFalse1.IsValid.Should().BeTrue();
            validFalse2.IsValid.Should().BeTrue();
            validFalse3.IsValid.Should().BeTrue();
            validFalse4.IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_HasDiscrepancies_Flag()
        {
            // Arrange - Flag para indicar si hay discrepancias
            var validator = new BooleanValidator();

            var values = new object[] { true, "true", "sí", "1", false, "false", "no", "0" };

            // Act & Assert
            foreach (var value in values)
            {
                validator.Validate("ConDiscrepancias", value).IsValid.Should().BeTrue();
            }
        }

        [Fact]
        public void Validate_IsUpdated_Status()
        {
            // Arrange - Indica si el documento está actualizado
            var validator = new BooleanValidator();

            // Act & Assert
            validator.Validate("Actualizado", true).IsValid.Should().BeTrue();
            validator.Validate("Actualizado", "verdadero").IsValid.Should().BeTrue();
            validator.Validate("Actualizado", false).IsValid.Should().BeTrue();
            validator.Validate("Actualizado", "falso").IsValid.Should().BeTrue();
            validator.Validate("Actualizado", "pending").IsValid.Should().BeFalse();
        }

        #endregion

        #region Casos de Uso Reales - From Forms

        [Fact]
        public void Validate_FormCheckbox_VariousInputs()
        {
            // Arrange - Datos que vienen de un formulario
            var validator = new BooleanValidator();

            // HTML checkbox puede enviar various valid valores
            var validInputs = new object[] { true, "1", "true", "yes", "sí", "verdadero", "0", "false", "no", "NO", "falso" };

            // Act & Assert
            foreach (var input in validInputs)
            {
                var result = validator.Validate("Aceptado", input);
                result.IsValid.Should().BeTrue(because: $"Input '{input}' should be a valid boolean value");
            }
        }

        #endregion

        #region Casos de Uso Reales - DocumentIA Tipologías

        [Fact]
        public void Validate_DocumentStatusFlags_NoteSimple()
        {
            // Arrange - Flags típicos en Nota Simple
            var validator = new BooleanValidator();

            var flags = new Dictionary<string, object[]>
            {
                { "CargasPendientes", new object[] { true, "sí", "1", false, "no", "0" } },
                { "ConFincaComercial", new object[] { true, "true", false, "false" } },
                { "ConHipoteca", new object[] { "verdadero", "falso", "sí", "no" } },
                { "ConCargas", new object[] { 1, 0, "yes", "no" } }
            };

            // Act & Assert
            foreach (var flag in flags)
            {
                foreach (var value in flag.Value)
                {
                    validator.Validate(flag.Key, value).IsValid.Should().BeTrue();
                }
            }
        }

        [Fact]
        public void Validate_PropertyCharacteristics_Flags()
        {
            // Arrange - Características de propiedades
            var validator = new BooleanValidator();

            var properties = new Dictionary<string, object>
            {
                { "EsViviendaPrincipal", true },
                { "TieneCalefaccion", "sí" },
                { "TieneAsensor", "no" },
                { "EsNuevaConstruction", "verdadero" },
                { "TienePiscina", "1" },
                { "TienePatio", "0" }
            };

            // Act & Assert
            foreach (var prop in properties)
            {
                validator.Validate(prop.Key, prop.Value).IsValid.Should().BeTrue();
            }
        }

        #endregion

        #region Tipos de Datos

        [Fact]
        public void Validate_IntegerValue_ReturnsValid()
        {
            // Arrange
            var validator = new BooleanValidator();

            // Act
            var result1 = validator.Validate("Flag", 1);
            var result0 = validator.Validate("Flag", 0);

            // Assert
            result1.IsValid.Should().BeTrue();
            result0.IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_IntegerOtherThanZeroOne_ReturnsInvalid()
        {
            // Arrange
            var validator = new BooleanValidator();

            // Act
            var result2 = validator.Validate("Flag", 2);
            var resultNeg1 = validator.Validate("Flag", -1);

            // Assert
            result2.IsValid.Should().BeFalse();
            resultNeg1.IsValid.Should().BeFalse();
        }

        [Fact]
        public void Validate_WithLeadingTrailingSpaces()
        {
            // Arrange
            var validator = new BooleanValidator();

            // Act - Las variantes españolas se trimean internamente
            var result1 = validator.Validate("Flag", " sí ");
            var result2 = validator.Validate("Flag", "  verdadero  ");
            var result3 = validator.Validate("Flag", "   no   ");

            // Assert
            result1.IsValid.Should().BeTrue();
            result2.IsValid.Should().BeTrue();
            result3.IsValid.Should().BeTrue();
        }

        #endregion

        #region Combinaciones Reales de Documnets

        [Fact]
        public void Validate_RealDocument_AllBooleanFields()
        {
            // Arrange - Documento completo con campos booleanos
            var validator = new BooleanValidator();

            var documentData = new Dictionary<string, object>
            {
                { "EsVPO", true },
                { "TieneCargas", "sí" },
                { "CaugasPendientes", "verdadero" },
                { "ConFincaComercial", "1" },
                { "ActualizacionCatastral", false },
                { "InmueblesComprendidos", "no" },
                { "ProyectoSuperior", "0" },
                { "TieneHipoteca", "falso" }
            };

            // Act & Assert
            foreach (var field in documentData)
            {
                validator.Validate(field.Key, field.Value).IsValid.Should().BeTrue();
            }
        }

        #endregion
    }
}
