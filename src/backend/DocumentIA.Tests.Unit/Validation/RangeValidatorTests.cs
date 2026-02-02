// src/backend/DocumentIA.Tests.Unit/Validation/RangeValidatorTests.cs
using DocumentIA.Core.Validation.Rules;
using FluentAssertions;
using Xunit;

#nullable disable

namespace DocumentIA.Tests.Unit.Validation
{
    public class RangeValidatorTests
    {
        [Fact]
        public void Validate_ValueWithinRange_ReturnsValid()
        {
            var validator = new RangeValidator(min: 1000, max: 2000000);

            var result = validator.Validate("ValorTasado", 50000);

            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_ValueBelowMin_ReturnsInvalid()
        {
            var validator = new RangeValidator(min: 1000, max: 2000000);

            var result = validator.Validate("ValorTasado", 500);

            result.IsValid.Should().BeFalse();
            result.Message.Should().Contain("menor que el minimo");
        }

        [Fact]
        public void Validate_ValueAboveMax_ReturnsInvalid()
        {
            var validator = new RangeValidator(min: 1000, max: 2000000);

            var result = validator.Validate("ValorTasado", 3000000);

            result.IsValid.Should().BeFalse();
            result.Message.Should().Contain("mayor que el maximo");
        }

        [Fact]
        public void Validate_ValueAtMinBoundary_ReturnsValid()
        {
            var validator = new RangeValidator(min: 1000, max: 2000000);

            var result = validator.Validate("ValorTasado", 1000);

            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_ValueAtMaxBoundary_ReturnsValid()
        {
            var validator = new RangeValidator(min: 1000, max: 2000000);

            var result = validator.Validate("ValorTasado", 2000000);

            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_NonNumericValue_ReturnsInvalid()
        {
            var validator = new RangeValidator(min: 1000, max: 2000000);

            var result = validator.Validate("ValorTasado", "not-a-number");

            result.IsValid.Should().BeFalse();
            result.Message.Should().Contain("no es numerico");
        }

        [Fact]
        public void Validate_NullValue_ReturnsValid()
        {
            var validator = new RangeValidator(min: 1000, max: 2000000);

            var result = validator.Validate("ValorTasado", null);

            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_OnlyMinSet_ValidatesMin()
        {
            var validator = new RangeValidator(min: 1000);

            var resultValid = validator.Validate("Valor", 5000);
            var resultInvalid = validator.Validate("Valor", 500);

            resultValid.IsValid.Should().BeTrue();
            resultInvalid.IsValid.Should().BeFalse();
        }

        [Fact]
        public void Validate_OnlyMaxSet_ValidatesMax()
        {
            var validator = new RangeValidator(max: 10000);

            var resultValid = validator.Validate("Valor", 5000);
            var resultInvalid = validator.Validate("Valor", 15000);

            resultValid.IsValid.Should().BeTrue();
            resultInvalid.IsValid.Should().BeFalse();
        }
    }
}
