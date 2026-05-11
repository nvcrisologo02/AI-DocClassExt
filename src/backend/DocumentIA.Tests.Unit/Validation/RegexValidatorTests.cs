using DocumentIA.Core.Validation.Rules;
using FluentAssertions;
using Xunit;

#nullable disable

namespace DocumentIA.Tests.Unit.Validation
{
    public class RegexValidatorTests
    {
        /// <summary>
        /// Tests para validador de expresiones regulares
        /// Cubre: patrones válidos/inválidos, null, vacío, caracteres especiales
        /// </summary>
        /// 
        #region Constructor y Patrones Válidos

        [Fact]
        public void Constructor_WithValidPattern_Succeeds()
        {
            // Arrange & Act
            var validator = new RegexValidator(@"^\d{5}$");

            // Assert
            validator.RuleName.Should().Be("RegexValidator");
        }

        [Fact]
        public void Constructor_WithNullPattern_ThrowsException()
        {
            // Arrange & Act
            var action = () => new RegexValidator(null);

            // Assert
            action.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void Constructor_WithWhitespacePattern_ThrowsException()
        {
            // Arrange & Act
            var action = () => new RegexValidator("   ");

            // Assert
            action.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void Constructor_WithInvalidRegex_ThrowsException()
        {
            // Arrange & Act - Patrón regex inválido
            var action = () => new RegexValidator("[invalid");

            // Assert
            action.Should().Throw<ArgumentException>().WithMessage("*inválido*");
        }

        #endregion

        #region Validación de Códigos Postales

        [Theory]
        [InlineData("28013")]
        [InlineData("41007")]
        [InlineData("08001")]
        [InlineData("29015")]
        public void Validate_ValidPostalCode_ReturnsValid(string postalCode)
        {
            // Arrange - Patrón para código postal español (5 dígitos)
            var validator = new RegexValidator(@"^\d{5}$");

            // Act
            var result = validator.Validate("CodigoPostal", postalCode);

            // Assert
            result.IsValid.Should().BeTrue();
        }

        [Theory]
        [InlineData("280")]        // Muy corto
        [InlineData("280130")]     // Muy largo
        [InlineData("2801A")]      // Contiene letra
        public void Validate_InvalidPostalCode_ReturnsInvalid(string invalidCode)
        {
            // Arrange
            var validator = new RegexValidator(@"^\d{5}$");

            // Act
            var result = validator.Validate("CodigoPostal", invalidCode);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Message.Should().Contain("no cumple con el formato");
        }

        #endregion

        #region Validación de Referencias Catastrales

        [Theory]
        [InlineData("7949211YJ2874N0025RA")]    // Formato correcto: 7+2+4+1+4+2
        [InlineData("1234567AB1234S0001ZX")]
        [InlineData("0000000AA0000A0000XX")]
        public void Validate_ValidCatastralReference_ReturnsValid(string catastralRef)
        {
            // Arrange - Patrón catastral: 7 dígitos + 2 letras + 4 dígitos + 1 letra + 4 dígitos + 2 letras
            var validator = new RegexValidator(@"^[0-9]{7}[A-Z]{2}[0-9]{4}[A-Z]{1}[0-9]{4}[A-Z]{2}$");

            // Act
            var result = validator.Validate("ReferenciaCatastral", catastralRef);

            // Assert
            result.IsValid.Should().BeTrue();
        }

        [Theory]
        [InlineData("7949211YJ2874N002RA")]     // Faltan dígitos al final
        [InlineData("7949211YJ2874N0025R")]     // Solo 1 letra al final
        [InlineData("794921YJ2874N0025RA")]     // Solo 6 dígitos al inicio
        [InlineData("7949211yj2874n0025ra")]    // Letras minúsculas (caso sensible)
        public void Validate_InvalidCatastralReference_ReturnsInvalid(string invalidRef)
        {
            // Arrange
            var validator = new RegexValidator(@"^[0-9]{7}[A-Z]{2}[0-9]{4}[A-Z]{1}[0-9]{4}[A-Z]{2}$");

            // Act
            var result = validator.Validate("ReferenciaCatastral", invalidRef);

            // Assert
            result.IsValid.Should().BeFalse();
        }

        #endregion

        #region Validación de NIFs/NIEs

        [Theory]
        [InlineData("12345678Z")]   // NIF con control
        [InlineData("87654321A")]
        [InlineData("X1234567L")]   // NIE
        public void Validate_ValidNifNie_ReturnsValid(string nifNie)
        {
            // Arrange - Patrón NIF/NIE: 8 caracteres + 1 letra
            var validator = new RegexValidator(@"^[0-9X][0-9]{7}[A-Z]$");

            // Act
            var result = validator.Validate("NIF", nifNie);

            // Assert
            result.IsValid.Should().BeTrue();
        }

        [Theory]
        [InlineData("12345678")]    // Sin letra control
        [InlineData("123456789")]   // 9 dígitos
        [InlineData("1234567Z")]    // Solo 7 dígitos
        public void Validate_InvalidNifNie_ReturnsInvalid(string invalidNif)
        {
            // Arrange
            var validator = new RegexValidator(@"^[0-9X][0-9]{7}[A-Z]$");

            // Act
            var result = validator.Validate("NIF", invalidNif);

            // Assert
            result.IsValid.Should().BeFalse();
        }

        #endregion

        #region Validación de Emails

        [Theory]
        [InlineData("usuario@example.com")]
        [InlineData("nombre.apellido@empresa.es")]
        [InlineData("user+tag@domain.co.uk")]
        public void Validate_ValidEmail_ReturnsValid(string email)
        {
            // Arrange - Patrón email simplificado
            var validator = new RegexValidator(@"^[^\s@]+@[^\s@]+\.[^\s@]+$");

            // Act
            var result = validator.Validate("Email", email);

            // Assert
            result.IsValid.Should().BeTrue();
        }

        [Theory]
        [InlineData("usuario@")]         // Dominio faltante
        [InlineData("usuario.com")]      // Falta @ y dominio
        [InlineData("@example.com")]     // Sin usuario
        public void Validate_InvalidEmail_ReturnsInvalid(string invalidEmail)
        {
            // Arrange
            var validator = new RegexValidator(@"^[^\s@]+@[^\s@]+\.[^\s@]+$");

            // Act
            var result = validator.Validate("Email", invalidEmail);

            // Assert
            result.IsValid.Should().BeFalse();
        }

        #endregion

        #region Null y Valores Especiales

        [Fact]
        public void Validate_NullValue_ReturnsValid()
        {
            // Arrange
            var validator = new RegexValidator(@"^\d{5}$");

            // Act
            var result = validator.Validate("CodigoPostal", null);

            // Assert
            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_EmptyString_ReturnsValid()
        {
            // Arrange
            var validator = new RegexValidator(@"^\d{5}$");

            // Act
            var result = validator.Validate("CodigoPostal", "");

            // Assert
            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_WhitespaceOnly_ReturnsValid()
        {
            // Arrange
            var validator = new RegexValidator(@"^\d{5}$");

            // Act
            var result = validator.Validate("CodigoPostal", "   ");

            // Assert
            result.IsValid.Should().BeTrue();
        }

        #endregion

        #region Patrones Complejos

        [Theory]
        [InlineData("https://www.example.com")]
        [InlineData("http://api.service.io")]
        [InlineData("https://localhost:8080")]
        public void Validate_ValidUrl_ReturnsValid(string url)
        {
            // Arrange - Patrón URL simplificado
            var validator = new RegexValidator(@"^https?://[^\s/$.?#].[^\s]*$");

            // Act
            var result = validator.Validate("URL", url);

            // Assert
            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_ComplexPattern_WithAlternatives()
        {
            // Arrange - Patrón que acepta formato con o sin guiones
            var validator = new RegexValidator(@"^\d{3}-?\d{3}-?\d{4}$");

            // Act
            var validWithHyphens = validator.Validate("Phone", "555-123-4567");
            var validWithoutHyphens = validator.Validate("Phone", "5551234567");
            var invalid = validator.Validate("Phone", "55-123-4567");

            // Assert
            validWithHyphens.IsValid.Should().BeTrue();
            validWithoutHyphens.IsValid.Should().BeTrue();
            invalid.IsValid.Should().BeFalse();
        }

        #endregion

        #region Casos de Uso Reales

        [Fact]
        public void Validate_DocumentNumber_CaseSensitivity()
        {
            // Arrange - Algunos patrones son case-sensitive
            var validator = new RegexValidator(@"^[A-Z]{2}\d{6}$");

            var validUpperCase = validator.Validate("Doc", "AB123456");
            var invalidLowerCase = validator.Validate("Doc", "ab123456");

            // Assert
            validUpperCase.IsValid.Should().BeTrue();
            invalidLowerCase.IsValid.Should().BeFalse();
        }

        [Fact]
        public void Validate_ErrorMessageIncludesPattern()
        {
            // Arrange
            var pattern = @"^\d{5}$";
            var validator = new RegexValidator(pattern);

            // Act
            var result = validator.Validate("CodigoPostal", "ABC");

            // Assert
            result.IsValid.Should().BeFalse();
            result.SuggestionString.Should().Contain(pattern);
        }

        #endregion
    }
}
