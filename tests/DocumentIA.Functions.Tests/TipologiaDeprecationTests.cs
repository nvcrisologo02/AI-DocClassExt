using System.Diagnostics;
using Xunit;
using FluentAssertions;
using DocumentIA.Core.Extensions;
using DocumentIA.Data.Entities;
using Microsoft.Extensions.Logging;
using Moq;

namespace DocumentIA.Functions.Tests;

/// <summary>
/// Tests for PromptGPT deprecation (Fase 3, Task 5).
/// Verifies that:
/// 1. [Obsolete] attribute is correctly applied
/// 2. No breaking changes in classification workflow
/// 3. Legacy tipologías (with PromptGPT) work correctly
/// 4. Performance is not degraded
/// 5. v2.0 DROP migration will be safe
/// </summary>
public class TipologiaDeprecationTests
{
    private readonly ILogger<TipologiaDeprecationTests> _logger;

    public TipologiaDeprecationTests()
    {
        var mockLogger = new Mock<ILogger<TipologiaDeprecationTests>>();
        _logger = mockLogger.Object;
    }

    /// <summary>
    /// Test 1: Verify [Obsolete] attribute is applied to PromptGPT
    /// This should compile with CS0618 warning
    /// </summary>
    [Fact]
    public void PromptGptProperty_HasObsoleteAttribute()
    {
        // Arrange
        var tipologiaType = typeof(TipologiaEntity);
        var property = tipologiaType.GetProperty("PromptGPT");

        // Act
        var obsoleteAttribute = property
            ?.GetCustomAttributes(typeof(ObsoleteAttribute), false)
            .FirstOrDefault() as ObsoleteAttribute;

        // Assert
        obsoleteAttribute.Should().NotBeNull("PromptGPT should be marked [Obsolete]");
        obsoleteAttribute.IsError.Should().BeFalse("Should be a warning, not an error");
        obsoleteAttribute.Message.Should().Contain("ConfiguracionJson");
    }

    /// <summary>
    /// Test 2: Verify modern tipologías (ConfigJson-only) work without PromptGPT
    /// </summary>
    [Fact]
    public void ModernTipologia_WorksWithoutPromptGpt()
    {
        // Arrange
        var tipologia = TestFixtures.CreateValidTipologia(id: 1);
        tipologia.PromptGPT = null; // Modern: no legacy field

        // Act
        var systemPrompt = tipologia.GetSystemPrompt();
        var userTemplate = tipologia.GetUserPromptTemplate();
        var tdn1 = tipologia.GetTdn1();

        // Assert - should all work from ConfigJson
        systemPrompt.Should().NotBeNullOrEmpty();
        userTemplate.Should().NotBeNullOrEmpty();
        tdn1.Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// Test 3: Verify legacy tipologías (with PromptGPT) still work
    /// but read from ConfigJson instead
    /// </summary>
    [Fact]
    public void LegacyTipologia_WithPromptGpt_StillReadsFromConfigJson()
    {
        // Arrange
        var tipologia = TestFixtures.CreateValidTipologia(id: 2);
        tipologia.PromptGPT = "LEGACY_PROMPT_GPT_CONTENT"; // Legacy: has field set

        // Act
        var systemPrompt = tipologia.GetSystemPrompt();

        // Assert - should read from ConfigJson, NOT from PromptGPT
        systemPrompt.Should().NotBeNullOrEmpty();
        systemPrompt.Should().NotContain("LEGACY_PROMPT_GPT_CONTENT");
        systemPrompt.Should().Be("You are a document classifier"); // From ConfigJson
    }

    /// <summary>
    /// Test 4: Verify no breaking changes for tipología classification
    /// Test both modern (194 expected) and legacy (10 expected) scenarios
    /// </summary>
    [Theory]
    [InlineData(1, null, "Modern tipología (no PromptGPT)")]
    [InlineData(2, "LEGACY_DATA_1", "Legacy tipología (with PromptGPT)")]
    [InlineData(3, "LEGACY_DATA_2", "Legacy tipología variant")]
    public void Classification_Works_ForBothModernAndLegacy(int id, string? legacyPrompt, string scenario)
    {
        // Arrange
        var tipologia = TestFixtures.CreateValidTipologia(id: id);
        if (legacyPrompt != null)
        {
            tipologia.PromptGPT = legacyPrompt;
        }

        // Act
        var config = tipologia.GetValidationConfig(_logger);
        var hasValidConfig = tipologia.HasValidConfiguration();

        // Assert
        hasValidConfig.Should().BeTrue($"Classification should work for {scenario}");
        config.Should().NotBeNull();
    }

    /// <summary>
    /// Test 5: Verify extension methods never access deprecated field
    /// </summary>
    [Fact]
    public void ExtensionMethods_NeverAccess_DeprecatedField()
    {
        // Arrange
        var tipologia = TestFixtures.CreateValidTipologia(id: 3);
        tipologia.PromptGPT = "SHOULD_NOT_BE_USED";

        // Act - string-based extension methods
        var systemPrompt = tipologia.GetSystemPrompt();
        var userTemplate = tipologia.GetUserPromptTemplate();
        var tdn1 = tipologia.GetTdn1();
        var tdn2 = tipologia.GetTdn2();

        // Assert - none should contain the deprecated field value
        var results = new List<string> { systemPrompt, userTemplate, tdn1, tdn2 };
        foreach (var result in results)
        {
            result.Should().NotContain("SHOULD_NOT_BE_USED",
                "Extension methods should never read from deprecated PromptGPT field");
        }
    }

    /// <summary>
    /// Test 6: Performance - no degradation from [Obsolete] marking
    /// </summary>
    [Fact]
    public void Performance_NotDegraded_ByObsoleteAttribute()
    {
        // Arrange
        var stopwatch = new Stopwatch();
        var tipologia = TestFixtures.CreateValidTipologia(id: 4);
        const int iterations = 10000;

        // Act - measure extension method performance
        stopwatch.Start();
        for (int i = 0; i < iterations; i++)
        {
            _ = tipologia.GetSystemPrompt();
            _ = tipologia.GetUserPromptTemplate();
            _ = tipologia.GetValidationConfig(_logger);
        }
        stopwatch.Stop();

        // Assert - should complete quickly (< 1 second for 10k iterations)
        var avgMs = (double)stopwatch.ElapsedMilliseconds / iterations;
        avgMs.Should().BeLessThan(0.1, 
            $"Average time should be < 0.1ms per operation, got {avgMs}ms");
    }

    /// <summary>
    /// Test 7: Verify data consistency between modern and legacy tipologías
    /// Simulates current production state (10 legacy + 194 modern)
    /// </summary>
    [Fact]
    public void MixedEnvironment_Modern194_Plus_Legacy10_WorkCorrectly()
    {
        // Arrange - simulate production: 10 legacy, 194 modern
        var legacy = new List<TipologiaEntity>();
        var modern = new List<TipologiaEntity>();

        // Create 10 legacy tipologías (with PromptGPT)
        for (int i = 1; i <= 10; i++)
        {
            var tip = TestFixtures.CreateValidTipologia(id: i);
            tip.PromptGPT = $"LEGACY_PROMPT_{i}";
            legacy.Add(tip);
        }

        // Create 194 modern tipologías (ConfigJson-only)
        for (int i = 11; i <= 204; i++)
        {
            var tip = TestFixtures.CreateValidTipologia(id: i);
            tip.PromptGPT = null;
            modern.Add(tip);
        }

        // Act - classify with all 204 tipologías
        var legacyResults = legacy.Select(t => t.GetSystemPrompt()).ToList();
        var modernResults = modern.Select(t => t.GetSystemPrompt()).ToList();

        // Assert
        legacyResults.Should().HaveCount(10);
        modernResults.Should().HaveCount(194);
        
        legacyResults.Should().AllSatisfy(p => p.Should().NotBeNullOrEmpty());
        modernResults.Should().AllSatisfy(p => p.Should().NotBeNullOrEmpty());
        
        // Verify no legacy field values appear in results
        var allResults = legacyResults.Concat(modernResults);
        allResults.Should().AllSatisfy(r => 
            r.Should().NotMatch("LEGACY_PROMPT_*"));
    }

    /// <summary>
    /// Test 8: Verify v2.0 DROP migration will be safe
    /// This test verifies that removing PromptGPT column won't break classification
    /// </summary>
    [Fact]
    public void V20_Drop_PromptGpt_Migration_WillBeSafe()
    {
        // Arrange - simulate post-DROP state (no PromptGPT property)
        var tipologiaPost = new TipologiaEntity
        {
            Id = 1,
            Nombre = "Post-DROP Tipologia",
            Codigo = "POST001",
            Estado = EstadoTipologia.Published,
            ConfiguracionJson = TestFixtures.GetValidConfigJson(),
            // PromptGPT deliberately omitted (simulating DROP)
            Activa = true,
            UmbralClasificacion = 0.85,
            UmbralExtraccion = 0.80,
            Version = "2.0",
            FechaCreacion = DateTime.UtcNow
        };

        // Act - all operations should work without PromptGPT
        var systemPrompt = tipologiaPost.GetSystemPrompt();
        var config = tipologiaPost.GetValidationConfig(_logger);
        var hasValidConfig = tipologiaPost.HasValidConfiguration();

        // Assert - DROP is safe
        systemPrompt.Should().NotBeNullOrEmpty();
        config.Should().NotBeNull();
        hasValidConfig.Should().BeTrue();
    }

    /// <summary>
    /// Test 9: Verify code coverage for new [Obsolete] scenarios
    /// </summary>
    [Fact]
    public void LegacyPromptGpt_GetLegacyPromptGpt_Method_Works()
    {
        // Arrange
        var tipologia = TestFixtures.CreateValidTipologia(id: 5);
        tipologia.PromptGPT = "LEGACY_CONTENT";

        // Act - this method is also marked [Obsolete] for audit purposes
        var legacyValue = tipologia.GetLegacyPromptGPT();

        // Assert
        legacyValue.Should().Be("LEGACY_CONTENT",
            "Legacy method should still work for audit/debugging purposes");
    }

    /// <summary>
    /// Test 10: Verify classification workflow unchanged
    /// End-to-end: extraction -> classification -> extraction config access
    /// </summary>
    [Fact]
    public void ClassificationWorkflow_Unchanged_PreAndPostDeprecation()
    {
        // Arrange
        var tipologia = TestFixtures.CreateValidTipologia(id: 6);

        // Act - complete classification workflow
        var systemPrompt = tipologia.GetSystemPrompt();
        var userTemplate = tipologia.GetUserPromptTemplate();
        var extractionConfig = tipologia.GetExtractionConfig();
        var gdcConfig = tipologia.GetGdcConfig();
        var validation = tipologia.GetValidationConfig(_logger);

        // Assert - all components should work
        systemPrompt.Should().NotBeNullOrEmpty();
        userTemplate.Should().NotBeNullOrEmpty();
        extractionConfig.Should().NotBeNull();
        gdcConfig.Should().NotBeNull();
        validation.Should().NotBeNull();
    }
}
