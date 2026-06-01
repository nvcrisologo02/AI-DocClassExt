// src/backend/DocumentIA.Tests.Unit/Validation/DateFormatValidatorTests.cs
using System;
using DocumentIA.Core.Validation.Rules;
using FluentAssertions;
using Xunit;


#nullable disable

namespace DocumentIA.Tests.Unit.Validation
{
    public class DateFormatValidatorTests
    {
        [Theory]
        [InlineData("31/12/2025")]
        [InlineData("2025-12-31")]
        [InlineData("31-12-2025")]
        [InlineData("15/11/24")]
        [InlineData("04 de abril de 2016")]
        public void Validate_ValidDateFormats_ReturnsValid(string dateString)
        {
            var validator = new DateFormatValidator();

            var result = validator.Validate("FechaTasacion", dateString);

            result.IsValid.Should().BeTrue();
        }

        [Theory]
        [InlineData("32/12/2025")]
        [InlineData("31/13/2025")]
        [InlineData("2025/31/12")]
        [InlineData("31-DIC-2025")]
        public void Validate_InvalidDateFormats_ReturnsInvalid(string dateString)
        {
            var validator = new DateFormatValidator();

            var result = validator.Validate("FechaTasacion", dateString);

            result.IsValid.Should().BeFalse();
            result.Message.Should().Contain("no valido");
        }

        [Fact]
        public void Validate_FutureDate_WhenNotAllowed_ReturnsInvalid()
        {
            var validator = new DateFormatValidator(allowFutureDates: false);
            var futureDate = DateTime.Now.AddDays(10).ToString("dd/MM/yyyy");

            var result = validator.Validate("FechaTasacion", futureDate);

            result.IsValid.Should().BeFalse();
            result.Message.Should().Contain("futura");
        }

        [Fact]
        public void Validate_PastDate_WhenNotAllowed_ReturnsInvalid()
        {
            var validator = new DateFormatValidator(allowPastDates: false);
            var pastDate = DateTime.Now.AddDays(-10).ToString("dd/MM/yyyy");

            var result = validator.Validate("FechaTasacion", pastDate);

            result.IsValid.Should().BeFalse();
            result.Message.Should().Contain("pasada");
        }

        [Fact]
        public void Validate_CustomFormats_AcceptsOnlySpecified()
        {
            var validator = new DateFormatValidator(
                acceptedFormats: new[] { "yyyy-MM-dd" });

            var resultValid = validator.Validate("Fecha", "2025-12-31");
            var resultInvalid = validator.Validate("Fecha", "31/12/2025");

            resultValid.IsValid.Should().BeTrue();
            resultInvalid.IsValid.Should().BeFalse();
        }

        [Fact]
        public void Validate_SpanishTextualDate_WithUppercase_ReturnsValid()
        {
            var validator = new DateFormatValidator();

            var result = validator.Validate("Fecha", "04 DE ABRIL DE 2016");

            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_NullOrEmpty_ReturnsValid()
        {
            var validator = new DateFormatValidator();

            var resultNull = validator.Validate("Fecha", null);
            var resultEmpty = validator.Validate("Fecha", "");

            resultNull.IsValid.Should().BeTrue();
            resultEmpty.IsValid.Should().BeTrue();
        }
    }
}
