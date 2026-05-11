// src/backend/DocumentIA.Tests.Unit/Validation/RequiredFieldValidatorTests.cs
using DocumentIA.Core.Validation.Rules;
using FluentAssertions;
using Xunit;

#nullable disable

namespace DocumentIA.Tests.Unit.Validation
{
    public class RequiredFieldValidatorTests
    {
        private readonly RequiredFieldValidator _validator;

        public RequiredFieldValidatorTests()
        {
            _validator = new RequiredFieldValidator();
        }

        [Fact]
        public void Validate_WithValue_ReturnsValid()
        {
            var result = _validator.Validate("Campo", "valor");

            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_NullValue_ReturnsInvalid()
        {
            var result = _validator.Validate("Campo", null);

            result.IsValid.Should().BeFalse();
            result.Message.Should().Contain("obligatorio");
        }

        [Fact]
        public void Validate_EmptyString_ReturnsInvalid()
        {
            var result = _validator.Validate("Campo", "");

            result.IsValid.Should().BeFalse();
        }

        [Fact]
        public void Validate_WhitespaceString_ReturnsInvalid()
        {
            var result = _validator.Validate("Campo", "   ");

            result.IsValid.Should().BeFalse();
        }

        [Fact]
        public void Validate_NumericValue_ReturnsValid()
        {
            var result = _validator.Validate("Campo", 123);

            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_BooleanValue_ReturnsValid()
        {
            var result = _validator.Validate("Campo", false);

            result.IsValid.Should().BeTrue();
        }
    }
}
