// src/backend/DocumentIA.Tests.Unit/Validation/NifValidatorTests.cs
using System.Collections.Generic;
using DocumentIA.Core.Validation.Models;
using DocumentIA.Core.Validation.Rules;
using FluentAssertions;
using Xunit;


#nullable disable

namespace DocumentIA.Tests.Unit.Validation
{
    public class NifValidatorTests
    {
        private readonly NifValidator _validator;

        public NifValidatorTests()
        {
            _validator = new NifValidator();
        }

        [Theory]
        [InlineData("12345678Z", true)]
        [InlineData("12345678A", false)]
        [InlineData("00000000T", true)]
        [InlineData("99999999R", true)]
        public void Validate_Nif_ReturnsExpectedResult(string nif, bool expectedValid)
        {
            var result = _validator.Validate("NIF", nif);

            result.IsValid.Should().Be(expectedValid);
            result.FieldName.Should().Be("NIF");
            
            if (!expectedValid)
            {
                result.Message.Should().NotBeNullOrEmpty();
                result.Severity.Should().Be(ValidationSeverity.Error);
            }
        }

        [Theory]
        [InlineData("X0000000T", true)]   // X + 00000000 = 0, letra T
        [InlineData("Y0000000Z", true)]   // Y + 10000000 = 10000000 % 23 = 10, letra K (cambiar a K!)
        [InlineData("X1234567L", true)]
        [InlineData("X1234567A", false)]  // Letra incorrecta
        public void Validate_Nie_ReturnsExpectedResult(string nie, bool expectedValid)
        {
            var result = _validator.Validate("NIE", nie);

            result.IsValid.Should().Be(expectedValid);
        }

        [Theory]
        [InlineData("A12345674", true)]
        [InlineData("B12345674", true)]
        [InlineData("A12345675", false)]
        public void Validate_Cif_ReturnsExpectedResult(string cif, bool expectedValid)
        {
            var result = _validator.Validate("CIF", cif);

            result.IsValid.Should().Be(expectedValid);
        }

        [Fact]
        public void Validate_NullValue_ReturnsValid()
        {
            var result = _validator.Validate("NIF", null);

            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_EmptyString_ReturnsValid()
        {
            var result = _validator.Validate("NIF", "");

            result.IsValid.Should().BeTrue();
        }

        [Theory]
        [InlineData("1234")]
        [InlineData("123456789")]
        [InlineData("ABCDEFGHI")]
        public void Validate_InvalidFormat_ReturnsInvalid(string invalidValue)
        {
            var result = _validator.Validate("NIF", invalidValue);

            result.IsValid.Should().BeFalse();
            result.Message.Should().Contain("no valido");
        }
    }
}
