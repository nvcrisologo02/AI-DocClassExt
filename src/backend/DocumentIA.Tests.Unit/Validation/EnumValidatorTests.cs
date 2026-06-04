using DocumentIA.Core.Validation.Rules;
using FluentAssertions;
using Xunit;

#nullable disable

namespace DocumentIA.Tests.Unit.Validation
{
    public class EnumValidatorTests
    {
        /// <summary>
        /// Tests para validador de enumeraciones
        /// Cubre: valores válidos, inválidos, case-sensitivity, null, vacío
        /// </summary>
        /// 
        #region Constructor y Configuración

        [Fact]
        public void Constructor_WithValidList_Succeeds()
        {
            // Arrange & Act
            var values = new List<string> { "A", "B", "C" };
            var validator = new EnumValidator(values);

            // Assert
            validator.RuleName.Should().Be("EnumValidator");
        }

        [Fact]
        public void Constructor_WithNullList_ThrowsException()
        {
            // Arrange & Act
            var action = () => new EnumValidator(null);

            // Assert
            action.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void Constructor_WithEmptyList_Succeeds()
        {
            // Arrange & Act
            var validator = new EnumValidator(new List<string>());

            // Assert
            validator.Should().NotBeNull();
        }

        [Fact]
        public void Constructor_WithCaseSensitive_False_Succeeds()
        {
            // Arrange & Act
            var validator = new EnumValidator(
                new List<string> { "Option1", "Option2" },
                caseSensitive: false);

            // Assert
            validator.Should().NotBeNull();
        }

        [Fact]
        public void Constructor_WithCaseSensitive_True_Succeeds()
        {
            // Arrange & Act
            var validator = new EnumValidator(
                new List<string> { "Option1", "Option2" },
                caseSensitive: true);

            // Assert
            validator.Should().NotBeNull();
        }

        #endregion

        #region Validación Básica (Case-Insensitive)

        [Theory]
        [InlineData("DN_301")]
        [InlineData("DN_302")]
        [InlineData("D_100")]
        public void Validate_ValidValue_CaseInsensitive_ReturnsValid(string value)
        {
            // Arrange
            var validator = new EnumValidator(
                new List<string> { "DN_301", "DN_302", "D_100" },
                caseSensitive: false);

            // Act
            var result = validator.Validate("TipoDocumento", value);

            // Assert
            result.IsValid.Should().BeTrue();
        }

        [Theory]
        [InlineData("dn_301")]
        [InlineData("DN_301")]
        [InlineData("Dn_301")]
        [InlineData("dN_301")]
        public void Validate_DifferentCase_CaseInsensitive_ReturnsValid(string value)
        {
            // Arrange - Sin sensibilidad a mayúsculas
            var validator = new EnumValidator(
                new List<string> { "DN_301", "DN_302" },
                caseSensitive: false);

            // Act
            var result = validator.Validate("TipoDocumento", value);

            // Assert
            result.IsValid.Should().BeTrue();
        }

        [Theory]
        [InlineData("INVALID")]
        [InlineData("UNKNOWN")]
        [InlineData("OTHER")]
        public void Validate_InvalidValue_CaseInsensitive_ReturnsInvalid(string value)
        {
            // Arrange
            var validator = new EnumValidator(
                new List<string> { "DN_301", "DN_302", "D_100" },
                caseSensitive: false);

            // Act
            var result = validator.Validate("TipoDocumento", value);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Message.Should().Contain("no es válido");
        }

        #endregion

        #region Validación Case-Sensitive

        [Theory]
        [InlineData("Option1")]
        [InlineData("Option2")]
        [InlineData("Option3")]
        public void Validate_ValidValue_CaseSensitive_ReturnsValid(string value)
        {
            // Arrange
            var validator = new EnumValidator(
                new List<string> { "Option1", "Option2", "Option3" },
                caseSensitive: true);

            // Act
            var result = validator.Validate("Field", value);

            // Assert
            result.IsValid.Should().BeTrue();
        }

        [Theory]
        [InlineData("option1")]    // Minúsculas
        [InlineData("OPTION1")]    // Mayúsculas
        [InlineData("oPtIoN1")]    // Mixto
        public void Validate_WrongCase_CaseSensitive_ReturnsInvalid(string value)
        {
            // Arrange
            var validator = new EnumValidator(
                new List<string> { "Option1", "Option2" },
                caseSensitive: true);

            // Act
            var result = validator.Validate("Field", value);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Message.Should().Contain("no es válido");
        }

        #endregion

        #region Null y Valores Especiales

        [Fact]
        public void Validate_NullValue_ReturnsValid()
        {
            // Arrange
            var validator = new EnumValidator(new List<string> { "A", "B" });

            // Act
            var result = validator.Validate("Field", null);

            // Assert
            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_EmptyString_ReturnsValid()
        {
            // Arrange
            var validator = new EnumValidator(new List<string> { "A", "B" });

            // Act
            var result = validator.Validate("Field", "");

            // Assert
            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_WhitespaceOnly_ReturnsValid()
        {
            // Arrange
            var validator = new EnumValidator(new List<string> { "A", "B" });

            // Act
            var result = validator.Validate("Field", "   ");

            // Assert
            result.IsValid.Should().BeTrue();
        }

        #endregion

        #region Mensajes de Error

        [Fact]
        public void Validate_InvalidValue_ErrorMessageIncludesValidOptions()
        {
            // Arrange
            var validator = new EnumValidator(
                new List<string> { "RED", "GREEN", "BLUE" });

            // Act
            var result = validator.Validate("Color", "YELLOW");

            // Assert
            result.IsValid.Should().BeFalse();
            result.SuggestionString.Should().Contain("RED");
            result.SuggestionString.Should().Contain("GREEN");
            result.SuggestionString.Should().Contain("BLUE");
        }

        [Fact]
        public void Validate_ErrorMessage_ShowsInvalidValue()
        {
            // Arrange
            var validator = new EnumValidator(new List<string> { "A", "B" });

            // Act
            var result = validator.Validate("Field", "INVALID");

            // Assert
            result.IsValid.Should().BeFalse();
            result.Message.Should().Contain("INVALID");
        }

        #endregion

        #region Casos de Uso Reales - Tipologías de Documentos

        [Theory]
        [InlineData("notasimple")]
        [InlineData("tasacion")]
        [InlineData("cedula_habitabilidad")]
        public void Validate_DocumentTypology_CaseInsensitive(string tipologia)
        {
            // Arrange
            var validator = new EnumValidator(
                new List<string> { "notasimple", "tasacion", "cedula_habitabilidad" },
                caseSensitive: false);

            // Act
            var result = validator.Validate("Tipologia", tipologia);

            // Assert
            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_DocumentType_WithVersions()
        {
            // Arrange
            var validator = new EnumValidator(
                new List<string> { "DN_301", "DN_302", "D_100", "D_101" },
                caseSensitive: false);

            var validTypes = new[] { "DN_301", "dn_302", "D_100", "d_101" };

            // Act & Assert
            foreach (var type in validTypes)
            {
                validator.Validate("TipoDocumento", type).IsValid.Should().BeTrue();
            }
        }

        #endregion

        #region Casos de Uso Reales - Estados

        [Theory]
        [InlineData("PENDIENTE")]
        [InlineData("PROCESANDO")]
        [InlineData("COMPLETADO")]
        [InlineData("ERROR")]
        public void Validate_ProcessStatus_ReturnsValid(string status)
        {
            // Arrange
            var validator = new EnumValidator(
                new List<string> { "PENDIENTE", "PROCESANDO", "COMPLETADO", "ERROR" },
                caseSensitive: true);

            // Act
            var result = validator.Validate("Estado", status);

            // Assert
            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_StatusTransitions_ValidState()
        {
            // Arrange
            var validator = new EnumValidator(
                new List<string> { 
                    "BORRADOR", 
                    "ENVIADO", 
                    "CONFIRMADO", 
                    "COMPLETADO", 
                    "RECHAZADO" 
                },
                caseSensitive: false);

            var states = new[] { "borrador", "ENVIADO", "Confirmado" };

            // Act & Assert
            foreach (var state in states)
            {
                validator.Validate("EstadoDocumento", state).IsValid.Should().BeTrue();
            }
        }

        #endregion

        #region Casos de Uso Reales - Tipos de Usuarios

        [Fact]
        public void Validate_UserRole_CaseSensitive()
        {
            // Arrange
            var validator = new EnumValidator(
                new List<string> { "Admin", "Editor", "Viewer", "Guest" },
                caseSensitive: true);

            // Act
            var validRole = validator.Validate("Role", "Admin");
            var invalidRole1 = validator.Validate("Role", "invalid-role");
            var invalidRole2 = validator.Validate("Role", "ADMIN");

            // Assert
            validRole.IsValid.Should().BeTrue();
            invalidRole1.IsValid.Should().BeFalse();
            invalidRole2.IsValid.Should().BeFalse();
        }

        #endregion

        #region Casos de Uso Reales - Clasificación de Propiedades

        [Fact]
        public void Validate_PropertyType_RealEstate()
        {
            // Arrange
            var propertyTypes = new List<string>
            {
                "VIVIENDA",
                "COMERCIAL",
                "INDUSTRIAL",
                "GARAJE",
                "TERRENO",
                "MIXTO"
            };

            var validator = new EnumValidator(propertyTypes, caseSensitive: false);

            var validTypes = new[] { "vivienda", "COMERCIAL", "Garaje" };
            var invalidTypes = new[] { "APARTAMENTO", "CHALET", "SOLAR" };

            // Act & Assert
            foreach (var type in validTypes)
            {
                validator.Validate("TipoPropiedad", type).IsValid.Should().BeTrue();
            }

            foreach (var type in invalidTypes)
            {
                validator.Validate("TipoPropiedad", type).IsValid.Should().BeFalse();
            }
        }

        [Fact]
        public void Validate_PropertyUsage_Residential()
        {
            // Arrange
            var validator = new EnumValidator(
                new List<string> { "RESIDENCIAL", "NO_RESIDENCIAL" },
                caseSensitive: false);

            // Act & Assert
            validator.Validate("UsoPropiedad", "residencial").IsValid.Should().BeTrue();
            validator.Validate("UsoPropiedad", "RESIDENCIAL").IsValid.Should().BeTrue();
            validator.Validate("UsoPropiedad", "no_residencial").IsValid.Should().BeTrue();
            validator.Validate("UsoPropiedad", "AGRARIO").IsValid.Should().BeFalse();
        }

        [Fact]
        public void Validate_AcentosYMayusculas_CaseInsensitive_ReturnsValid()
        {
            // Arrange
            var validator = new EnumValidator(
                new List<string> { "Adjudicacion", "Cesion", "Division Horizontal" },
                caseSensitive: false);

            // Act & Assert
            validator.Validate("TituloAdquisicion", "Adjudicación").IsValid.Should().BeTrue();
            validator.Validate("TituloAdquisicion", "CESIÓN").IsValid.Should().BeTrue();
            validator.Validate("TituloAdquisicion", "división horizontal").IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_AcentosYMayusculas_CaseSensitive_ReturnsInvalid()
        {
            // Arrange
            var validator = new EnumValidator(
                new List<string> { "Adjudicacion" },
                caseSensitive: true);

            // Act
            var result = validator.Validate("TituloAdquisicion", "Adjudicación");

            // Assert
            result.IsValid.Should().BeFalse();
        }

        #endregion

        #region Listas Numéricas como Strings

        [Fact]
        public void Validate_NumericValues_AsStrings()
        {
            // Arrange
            var validator = new EnumValidator(
                new List<string> { "1", "2", "3", "4", "5" },
                caseSensitive: false);

            // Act & Assert
            validator.Validate("Nivel", "1").IsValid.Should().BeTrue();
            validator.Validate("Nivel", "5").IsValid.Should().BeTrue();
            validator.Validate("Nivel", "6").IsValid.Should().BeFalse();
        }

        [Fact]
        public void Validate_EmptyList_ReturnsInvalid()
        {
            // Arrange
            var validator = new EnumValidator(new List<string>());

            // Act
            var result = validator.Validate("Field", "ANY_VALUE");

            // Assert
            result.IsValid.Should().BeFalse();
        }

        #endregion

        #region Tipos de Datos

        [Fact]
        public void Validate_IntegerValue_ConvertedToString()
        {
            // Arrange
            var validator = new EnumValidator(new List<string> { "1", "2", "3" });

            // Act
            var result = validator.Validate("Numero", 1);

            // Assert
            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_BooleanValue_ConvertedToString()
        {
            // Arrange
            var validator = new EnumValidator(
                new List<string> { "true", "false" },
                caseSensitive: false);

            // Act
            var result = validator.Validate("Flag", true);

            // Assert
            result.IsValid.Should().BeTrue();
        }

        #endregion
    }
}
