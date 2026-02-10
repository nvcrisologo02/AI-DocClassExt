// src/backend/DocumentIA.Tests.Unit/Validation/CatastralReferenceValidatorTests.cs
using DocumentIA.Core.Validation.Rules;
using FluentAssertions;
using Xunit;

#nullable disable

namespace DocumentIA.Tests.Unit.Validation
{
    public class CatastralReferenceValidatorTests
    {
        private readonly CatastralReferenceValidator _validator;

        public CatastralReferenceValidatorTests()
        {
            _validator = new CatastralReferenceValidator();
        }

        [Fact]
        public void Validate_ValidFormat_ReturnsValid()
        {
            var validRef = "1234567AB1234S0001ZX";

            var result = _validator.Validate("RefCatastral", validRef);

            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_TooShort_ReturnsInvalid()
        {
            var shortRef = "1234567AB1234";

            var result = _validator.Validate("RefCatastral", shortRef);

            result.IsValid.Should().BeFalse();
            result.Message.Should().Contain("20 caracteres");
        }

        [Fact]
        public void Validate_TooLong_ReturnsInvalid()
        {
            var longRef = "1234567AB1234S0001ZXABC";

            var result = _validator.Validate("RefCatastral", longRef);

            result.IsValid.Should().BeFalse();
        }

        [Fact]
        public void Validate_InvalidFormat_ReturnsInvalid()
        {
            var invalidRef = "ABCD567AB1234S0001ZX";

            var result = _validator.Validate("RefCatastral", invalidRef);

            result.IsValid.Should().BeFalse();
            result.Message.Should().Contain("formato");
        }

        [Fact]
        public void Validate_NullValue_ReturnsValid()
        {
            var result = _validator.Validate("RefCatastral", null);

            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_EmptyString_ReturnsValid()
        {
            var result = _validator.Validate("RefCatastral", "");

            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_WithSpaces_RemovesSpacesAndValidates()
        {
            var refWithSpaces = "1234567 AB 1234 S 0001 ZX";

            var result = _validator.Validate("RefCatastral", refWithSpaces);

            result.IsValid.Should().BeTrue();
        }
    }
}
