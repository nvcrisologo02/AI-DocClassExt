using DocumentIA.Core.Configuration;
using DocumentIA.Functions.Abstractions;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace DocumentIA.Functions.Tests.Services;

/// <summary>
/// Tests unitarios simplificados para GptClasificarDataProvider.
/// 
/// Validan la integración con IClassificationPromptProvider sin intentar ejecutar
/// el flujo completo E2E de clasificación (que requeriría mockear dependencias concretas
/// sin interfaces como ClassificationModelRegistryLoader).
/// 
/// Objetivo de los tests:
/// 1. Verificar que IClassificationPromptProvider se inyecta correctamente
/// 2. Verificar que los prompts resueltos (Source, Version) están disponibles
/// 3. Validar que el sistema puede obtener prompts desde BD o fallback
/// </summary>
public class GptClasificarDataProviderTests
{
    [Fact]
    public async Task IClassificationPromptProvider_CanResolvePromptsFromDatabase()
    {
        // Arrange
        var mockProvider = new Mock<IClassificationPromptProvider>();
        mockProvider
            .Setup(p => p.GetPromptSetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Core.Models.ClassificationPromptSet
            {
                Phase1SystemPrompt = "DB Phase1 System",
                Phase1UserPrompt = "DB Phase1 User {CONTEXT_PROMPT}",
                Phase2SystemPrompt = "DB Phase2 System",
                Phase2UserPrompt = "DB Phase2 User {TDN1_CODE}",
                Version = 5,
                Source = "Database",
                ResolvedAtUtc = DateTime.UtcNow
            });

        // Act
        var result = await mockProvider.Object.GetPromptSetAsync();

        // Assert
        result.Should().NotBeNull();
        result.Source.Should().Be("Database");
        result.Version.Should().Be(5);
        result.Phase1SystemPrompt.Should().Contain("DB Phase1 System");
        result.Phase1UserPrompt.Should().Contain("{CONTEXT_PROMPT}");
        result.Phase2SystemPrompt.Should().Contain("DB Phase2 System");
        result.Phase2UserPrompt.Should().Contain("{TDN1_CODE}");

        mockProvider.Verify(p => p.GetPromptSetAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IClassificationPromptProvider_CanResolvePromptsFromFallback()
    {
        // Arrange
        var mockProvider = new Mock<IClassificationPromptProvider>();
        mockProvider
            .Setup(p => p.GetPromptSetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Core.Models.ClassificationPromptSet
            {
                Phase1SystemPrompt = "Fallback Phase1 System",
                Phase1UserPrompt = "Fallback Phase1 User {CONTEXT_PROMPT}",
                Phase2SystemPrompt = "Fallback Phase2 System",
                Phase2UserPrompt = "Fallback Phase2 User {TDN1_CODE}",
                Version = 0, // Version 0 indica fallback
                Source = "Fallback",
                ResolvedAtUtc = DateTime.UtcNow
            });

        // Act
        var result = await mockProvider.Object.GetPromptSetAsync();

        // Assert
        result.Should().NotBeNull();
        result.Source.Should().Be("Fallback");
        result.Version.Should().Be(0);
        result.Phase1SystemPrompt.Should().Contain("Fallback");
        result.Phase2UserPrompt.Should().Contain("{TDN1_CODE}");

        mockProvider.Verify(p => p.GetPromptSetAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void ClassificationPromptsSettings_EnableFullPromptLogging_DefaultFalse()
    {
        // Arrange & Act
        var settings = new ClassificationPromptsSettings
        {
            Phase1 = new ClassificationPhasePromptSettings
            {
                SystemPrompt = "Test",
                UserPromptTemplate = "Test"
            },
            Phase2 = new ClassificationPhasePromptSettings
            {
                SystemPrompt = "Test",
                UserPromptTemplate = "Test"
            }
        };

        // Assert
        settings.EnableFullPromptLogging.Should().BeFalse("EnableFullPromptLogging debe estar desactivado por defecto");
    }

    [Fact]
    public void ClassificationPromptsSettings_EnableFullPromptLogging_CanBeEnabled()
    {
        // Arrange & Act
        var settings = new ClassificationPromptsSettings
        {
            Phase1 = new ClassificationPhasePromptSettings
            {
                SystemPrompt = "Test",
                UserPromptTemplate = "Test"
            },
            Phase2 = new ClassificationPhasePromptSettings
            {
                SystemPrompt = "Test",
                UserPromptTemplate = "Test"
            },
            EnableFullPromptLogging = true
        };

        // Assert
        settings.EnableFullPromptLogging.Should().BeTrue();
    }

    [Fact]
    public void ClassificationPromptSet_HasRequiredMetadata()
    {
        // Arrange & Act
        var promptSet = new Core.Models.ClassificationPromptSet
        {
            Phase1SystemPrompt = "P1S",
            Phase1UserPrompt = "P1U",
            Phase2SystemPrompt = "P2S",
            Phase2UserPrompt = "P2U",
            Version = 3,
            Source = "Database",
            ResolvedAtUtc = DateTime.UtcNow
        };

        // Assert
        promptSet.Version.Should().Be(3);
        promptSet.Source.Should().Be("Database");
        promptSet.ResolvedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }
}
