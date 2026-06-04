using Xunit;
using FluentAssertions;
using DocumentIA.Core.Extensions;
using DocumentIA.Data.Entities;
using Microsoft.Extensions.Logging;
using Moq;

namespace DocumentIA.Functions.Tests;

/// <summary>
/// Tests for TipologiaEntity extension methods that safely access ConfiguracionJson.
/// Validates that deprecated PromptGPT field is never used.
/// </summary>
public class TipologiaEntityExtensionsTests
{
    private readonly ILogger<TipologiaEntityExtensionsTests> _logger;

    public TipologiaEntityExtensionsTests()
    {
        var mockLogger = new Mock<ILogger<TipologiaEntityExtensionsTests>>();
        _logger = mockLogger.Object;
    }

    [Fact]
    public void GetValidationConfig_ReturnsValidObject_WhenJsonExists()
    {
        // Arrange
        var tipologia = TestFixtures.CreateValidTipologia();

        // Act
        var config = tipologia.GetValidationConfig(_logger);

        // Assert
        config.Should().NotBeNull();
    }

    [Fact]
    public void GetValidationConfig_ReturnsEmptyConfig_WhenJsonIsNull()
    {
        // Arrange
        var tipologia = new TipologiaEntity { ConfiguracionJson = null };

        // Act
        var config = tipologia.GetValidationConfig(_logger);

        // Assert
        config.Should().NotBeNull();
    }

    [Fact]
    public void GetValidationConfig_ReturnsEmptyConfig_WhenJsonIsEmpty()
    {
        // Arrange
        var tipologia = new TipologiaEntity { ConfiguracionJson = string.Empty };

        // Act
        var config = tipologia.GetValidationConfig(_logger);

        // Assert
        config.Should().NotBeNull();
    }

    [Fact]
    public void GetSystemPrompt_ExtractsPrompt_FromConfigJson()
    {
        // Arrange
        var tipologia = TestFixtures.CreateValidTipologia();

        // Act
        var prompt = tipologia.GetSystemPrompt();

        // Assert
        prompt.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GetSystemPrompt_ReturnsEmpty_WhenJsonIsNull()
    {
        // Arrange
        var tipologia = new TipologiaEntity { ConfiguracionJson = null };

        // Act
        var prompt = tipologia.GetSystemPrompt();

        // Assert
        prompt.Should().Be(string.Empty);
    }

    [Fact]
    public void GetUserPromptTemplate_ExtractsTemplate_FromConfigJson()
    {
        // Arrange
        var tipologia = TestFixtures.CreateValidTipologia();

        // Act
        var template = tipologia.GetUserPromptTemplate();

        // Assert
        template.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GetUserPromptTemplate_ReturnsEmpty_WhenJsonIsNull()
    {
        // Arrange
        var tipologia = new TipologiaEntity { ConfiguracionJson = null };

        // Act
        var template = tipologia.GetUserPromptTemplate();

        // Assert
        template.Should().Be(string.Empty);
    }

    [Fact]
    public void GetTdn1_ExtractsTdn1_FromConfigJson()
    {
        // Arrange
        var tipologia = TestFixtures.CreateValidTipologia();

        // Act
        var tdn1 = tipologia.GetTdn1();

        // Assert
        tdn1.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GetTdn2_ExtractsTdn2_FromConfigJson()
    {
        // Arrange
        var tipologia = TestFixtures.CreateValidTipologia();

        // Act
        var tdn2 = tipologia.GetTdn2();

        // Assert
        tdn2.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Extensions_NeverAccess_DeprecatedPromptGptField()
    {
        // Arrange
        var tipologia = TestFixtures.CreateValidTipologia();
        tipologia.PromptGPT = "THIS_SHOULD_NOT_BE_USED";

        // Act
        var systemPrompt = tipologia.GetSystemPrompt();
        var userTemplate = tipologia.GetUserPromptTemplate();

        // Assert - all should come from JSON, not deprecated field
        systemPrompt.Should().NotContain("THIS_SHOULD_NOT_BE_USED");
        userTemplate.Should().NotContain("THIS_SHOULD_NOT_BE_USED");
    }
}
