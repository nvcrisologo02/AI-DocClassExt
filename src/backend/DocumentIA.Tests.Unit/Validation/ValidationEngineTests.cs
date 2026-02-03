using System.Collections.Generic;
using DocumentIA.Core.Validation;
using DocumentIA.Core.Validation.Models;
using DocumentIA.Core.Validation.Rules;
using FluentAssertions;
using Xunit;


#nullable disable

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
    }
}
