using DocumentIA.Core.Validation.Rules;
using FluentAssertions;
using Xunit;

namespace DocumentIA.Tests.Unit.Validation
{
    public class LengthValidatorTests
    {
        /// <summary>
        /// Tests para validador de longitud de strings
        /// Cubre: minLength, maxLength, null, vacío, límites exactos
        /// </summary>
        /// 
        #region Constructor y Configuración Básica

        [Fact]
        public void Constructor_WithoutRestrictions_Succeeds()
        {
            // Arrange & Act
            var validator = new LengthValidator();

            // Assert
            validator.RuleName.Should().Be("LengthValidator");
        }

        [Fact]
        public void Constructor_WithMinLength_Succeeds()
        {
            // Arrange & Act
            var validator = new LengthValidator(minLength: 5);

            // Assert
            validator.Should().NotBeNull();
        }

        [Fact]
        public void Constructor_WithMaxLength_Succeeds()
        {
            // Arrange & Act
            var validator = new LengthValidator(maxLength: 100);

            // Assert
            validator.Should().NotBeNull();
        }

        [Fact]
        public void Constructor_WithBothLengths_Succeeds()
        {
            // Arrange & Act
            var validator = new LengthValidator(minLength: 5, maxLength: 100);

            // Assert
            validator.Should().NotBeNull();
        }

        #endregion

        #region Null y Valores Vacíos

        [Fact]
        public void Validate_NullValue_ReturnsValid()
        {
            // Arrange - Null siempre es válido
            var validator = new LengthValidator(minLength: 5, maxLength: 100);

            // Act
            var result = validator.Validate("Campo", null);

            // Assert
            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_EmptyString_WithoutMinLengthRequirement_ReturnsValid()
        {
            // Arrange
            var validator = new LengthValidator(maxLength: 100);

            // Act
            var result = validator.Validate("Campo", "");

            // Assert
            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_EmptyString_WithMinLengthGreaterThanZero_ReturnsInvalid()
        {
            // Arrange
            var validator = new LengthValidator(minLength: 1);

            // Act
            var result = validator.Validate("Campo", "");

            // Assert
            result.IsValid.Should().BeFalse();
            result.Message.Should().Contain("está vacío");
        }

        [Fact]
        public void Validate_WhitespaceOnly_CountsAsLength()
        {
            // Arrange
            var validator = new LengthValidator(minLength: 3);

            // Act
            var result = validator.Validate("Campo", "   ");  // 3 espacios

            // Assert
            result.IsValid.Should().BeTrue();
        }

        #endregion

        #region Validación Mínima

        [Theory]
        [InlineData("Hola", 4)]
        [InlineData("Hola mundo", 10)]
        [InlineData("A", 1)]
        public void Validate_ExactMinLength_ReturnsValid(string value, int minLength)
        {
            // Arrange
            var validator = new LengthValidator(minLength: minLength);

            // Act
            var result = validator.Validate("Campo", value);

            // Assert
            result.IsValid.Should().BeTrue();
        }

        [Theory]
        [InlineData("Hola", 5)]      // Necesita al menos 5
        [InlineData("Hi", 3)]        // Necesita al menos 3
        [InlineData("X", 2)]         // Necesita al menos 2
        public void Validate_BelowMinLength_ReturnsInvalid(string value, int minLength)
        {
            // Arrange
            var validator = new LengthValidator(minLength: minLength);

            // Act
            var result = validator.Validate("Campo", value);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Message.Should().Contain($"menor que el mínimo");
        }

        [Fact]
        public void Validate_MinLengthError_IncludesActualAndRequired()
        {
            // Arrange
            var validator = new LengthValidator(minLength: 10);

            // Act
            var result = validator.Validate("Campo", "Corto");

            // Assert
            result.IsValid.Should().BeFalse();
            result.Message.Should().Contain("5");  // longitud actual
            result.Message.Should().Contain("10"); // mínimo requerido
        }

        #endregion

        #region Validación Máxima

        [Theory]
        [InlineData("Hola", 4)]
        [InlineData("Hola mundo", 10)]
        [InlineData("A corta descripción", 50)]
        public void Validate_ExactMaxLength_ReturnsValid(string value, int maxLength)
        {
            // Arrange
            var validator = new LengthValidator(maxLength: maxLength);

            // Act
            var result = validator.Validate("Campo", value);

            // Assert
            result.IsValid.Should().BeTrue();
        }

        [Theory]
        [InlineData("Hola mundo muy largo", 10)]
        [InlineData("Este es un mensaje muy largo que excede el límite", 20)]
        public void Validate_ExceedsMaxLength_ReturnsInvalid(string value, int maxLength)
        {
            // Arrange
            var validator = new LengthValidator(maxLength: maxLength);

            // Act
            var result = validator.Validate("Campo", value);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Message.Should().Contain($"excede el máximo");
        }

        [Fact]
        public void Validate_MaxLengthError_IncludesActualAndMaximum()
        {
            // Arrange
            var validator = new LengthValidator(maxLength: 5);

            // Act
            var result = validator.Validate("Campo", "Demasiado largo");

            // Assert
            result.IsValid.Should().BeFalse();
            result.Message.Should().Contain("15"); // longitud actual
            result.Message.Should().Contain("5");  // máximo permitido
        }

        #endregion

        #region Validación Rango (Min + Max)

        [Theory]
        [InlineData("Hola", 3, 10)]      // 4 caracteres - válido
        [InlineData("Texto", 3, 10)]     // 5 caracteres - válido
        [InlineData("Información", 3, 20)]  // 11 caracteres - válido
        public void Validate_WithinRange_ReturnsValid(string value, int minLength, int maxLength)
        {
            // Arrange
            var validator = new LengthValidator(minLength: minLength, maxLength: maxLength);

            // Act
            var result = validator.Validate("Campo", value);

            // Assert
            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_BelowRangeMinimum_ReturnsInvalid()
        {
            // Arrange
            var validator = new LengthValidator(minLength: 10, maxLength: 50);

            // Act
            var result = validator.Validate("Campo", "Corto");  // 5 caracteres

            // Assert
            result.IsValid.Should().BeFalse();
        }

        [Fact]
        public void Validate_AboveRangeMaximum_ReturnsInvalid()
        {
            // Arrange
            var validator = new LengthValidator(minLength: 10, maxLength: 50);

            // Act
            var result = validator.Validate("Campo", "Este es un texto que definitivamente excede el máximo permitido de 50 caracteres");

            // Assert
            result.IsValid.Should().BeFalse();
        }

        [Fact]
        public void Validate_BoundaryMinimum_Inclusive()
        {
            // Arrange
            var validator = new LengthValidator(minLength: 5);

            // Act
            var result = validator.Validate("Campo", "Cinco");  // Exactamente 5

            // Assert
            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_BoundaryMaximum_Inclusive()
        {
            // Arrange
            var validator = new LengthValidator(maxLength: 5);

            // Act
            var result = validator.Validate("Campo", "Cinco");  // Exactamente 5

            // Assert
            result.IsValid.Should().BeTrue();
        }

        #endregion

        #region Casos de Uso Reales - Documentos

        [Fact]
        public void Validate_DocumentTitle_TypeicalLength()
        {
            // Arrange - Típicamente títulos de 10-200 caracteres
            var validator = new LengthValidator(minLength: 10, maxLength: 200);

            var validTitle = "Tasación de Propiedad Inmueble en Madrid";
            var invalidShort = "Tasación";
            var invalidLong = new string('A', 250);

            // Act & Assert
            validator.Validate("Titulo", validTitle).IsValid.Should().BeTrue();
            validator.Validate("Titulo", invalidShort).IsValid.Should().BeFalse();
            validator.Validate("Titulo", invalidLong).IsValid.Should().BeFalse();
        }

        [Fact]
        public void Validate_AddressField_RequiredMinLength()
        {
            // Arrange - Direcciones típicas de 20-160 caracteres
            var validator = new LengthValidator(minLength: 20, maxLength: 160);

            var validAddress = "Calle Mayor 15, 28013 Madrid";
            var invalidAddress = "C/ Mayor 1";

            // Act & Assert
            validator.Validate("Direccion", validAddress).IsValid.Should().BeTrue();
            validator.Validate("Direccion", invalidAddress).IsValid.Should().BeFalse();
        }

        [Fact]
        public void Validate_Notes_MaxLengthConstraint()
        {
            // Arrange - Notas con límite de 500 caracteres
            var validator = new LengthValidator(maxLength: 500);

            var shortNotes = "Observación breve";
            var longNotes = new string('A', 600);

            // Act & Assert
            validator.Validate("Notas", shortNotes).IsValid.Should().BeTrue();
            validator.Validate("Notas", longNotes).IsValid.Should().BeFalse();
        }

        [Fact]
        public void Validate_NifField_ExactLength()
        {
            // Arrange - NIF debe ser exactamente 9 caracteres
            var validator = new LengthValidator(minLength: 9, maxLength: 9);

            var validNif = "12345678Z";
            var invalidShort = "1234567Z";
            var invalidLong = "123456789Z";

            // Act & Assert
            validator.Validate("NIF", validNif).IsValid.Should().BeTrue();
            validator.Validate("NIF", invalidShort).IsValid.Should().BeFalse();
            validator.Validate("NIF", invalidLong).IsValid.Should().BeFalse();
        }

        #endregion

        #region Tipos de Datos

        [Fact]
        public void Validate_IntegerConvertedToString()
        {
            // Arrange
            var validator = new LengthValidator(minLength: 4, maxLength: 5);

            // Act
            var result = validator.Validate("Numero", 12345);

            // Assert
            result.IsValid.Should().BeTrue();  // "12345" tiene 5 caracteres
        }

        [Fact]
        public void Validate_WithoutAnyRestriction_AcceptsAll()
        {
            // Arrange
            var validator = new LengthValidator();  // Sin min ni max

            // Act & Assert
            validator.Validate("Campo", "").IsValid.Should().BeTrue();
            validator.Validate("Campo", "A").IsValid.Should().BeTrue();
            validator.Validate("Campo", new string('X', 10000)).IsValid.Should().BeTrue();
        }

        #endregion

        #region Casos Especiales con Caracteres

        [Fact]
        public void Validate_UnicodeCharacters_CountCorrectly()
        {
            // Arrange
            var validator = new LengthValidator(minLength: 3, maxLength: 20);

            // Act  
            var result = validator.Validate("Campo", "Café");

            // Assert
            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_EmojisAndSpecialChars()
        {
            // Arrange
            var validator = new LengthValidator(minLength: 3);

            // Act
            var result = validator.Validate("Campo", "Test 😀");

            // Assert
            result.IsValid.Should().BeTrue();
        }

        #endregion
    }
}
