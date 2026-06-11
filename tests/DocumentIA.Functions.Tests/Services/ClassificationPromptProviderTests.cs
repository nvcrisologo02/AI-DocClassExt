using Xunit;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DocumentIA.Core.Configuration;
using DocumentIA.Core.Models;
using DocumentIA.Data.Entities;
using DocumentIA.Data.Repositories;
using DocumentIA.Functions.Services;
using DocumentIA.Functions.Abstractions;

namespace DocumentIA.Functions.Tests.Services;

/// <summary>
/// Tests for ClassificationPromptProvider.
/// Validates BD → cache → fallback resolution with 120s TTL.
/// </summary>
public class ClassificationPromptProviderTests
{
    private readonly IMemoryCache _cache; // Real MemoryCache (extension methods can't be mocked)
    private readonly Mock<IServiceScopeFactory> _mockScopeFactory;
    private readonly Mock<IOptions<ClassificationPromptsSettings>> _mockOptions;
    private readonly Mock<ILogger<ClassificationPromptProvider>> _mockLogger;
    private readonly ClassificationPromptsSettings _fallbackSettings;

    public ClassificationPromptProviderTests()
    {
        _cache = new MemoryCache(new MemoryCacheOptions()); // Use real cache
        _mockScopeFactory = new Mock<IServiceScopeFactory>();
        _mockOptions = new Mock<IOptions<ClassificationPromptsSettings>>();
        _mockLogger = new Mock<ILogger<ClassificationPromptProvider>>();

        _fallbackSettings = new ClassificationPromptsSettings
        {
            Phase1 = new ClassificationPhasePromptSettings
            {
                SystemPrompt = "Fallback Phase1 System",
                UserPromptTemplate = "Fallback Phase1 User"
            },
            Phase2 = new ClassificationPhasePromptSettings
            {
                SystemPrompt = "Fallback Phase2 System",
                UserPromptTemplate = "Fallback Phase2 User"
            }
        };

        _mockOptions.Setup(o => o.Value).Returns(_fallbackSettings);
    }

    private ClassificationPromptProvider CreateProvider()
    {
        return new ClassificationPromptProvider(
            _cache,
            _mockScopeFactory.Object,
            _mockOptions.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public async Task GetPromptSetAsync_CacheHit_ReturnsFromCacheWithoutDbCall()
    {
        // Arrange
        var cachedSet = new ClassificationPromptSet
        {
            Phase1SystemPrompt = "Cached P1 System",
            Phase1UserPrompt = "Cached P1 User",
            Phase2SystemPrompt = "Cached P2 System",
            Phase2UserPrompt = "Cached P2 User",
            Version = 5,
            Source = "Database",
            ResolvedAtUtc = DateTime.UtcNow.AddMinutes(-1)
        };

        // Pre-populate cache with real MemoryCache
        _cache.Set("classification_prompts_all", cachedSet, TimeSpan.FromSeconds(120));

        var provider = CreateProvider();

        // Act
        var result = await provider.GetPromptSetAsync();

        // Assert
        result.Should().NotBeNull();
        result.Phase1SystemPrompt.Should().Be("Cached P1 System");
        result.Version.Should().Be(5);
        result.Source.Should().Be("Database");

        // Verify no scope factory call (no DB access)
        _mockScopeFactory.Verify(sf => sf.CreateScope(), Times.Never);
    }

    [Fact]
    public async Task GetPromptSetAsync_CacheMiss_DatabaseSuccess_ReturnsFromDbAndCaches()
    {
        // Arrange
        var mockRepository = new Mock<IPromptTemplateRepository>();
        var mockScope = new Mock<IServiceScope>();
        var mockServiceProvider = new Mock<IServiceProvider>();

        var phase1System = new PromptTemplateEntity 
        { 
            Id = 1, 
            PromptKey = "classification.phase1.system", 
            Version = 10, 
            Content = "DB P1 System", 
            IsActive = true 
        };
        var phase1User = new PromptTemplateEntity 
        { 
            Id = 2, 
            PromptKey = "classification.phase1.user", 
            Version = 10, 
            Content = "DB P1 User", 
            IsActive = true 
        };
        var phase2System = new PromptTemplateEntity 
        { 
            Id = 3, 
            PromptKey = "classification.phase2.system", 
            Version = 10, 
            Content = "DB P2 System", 
            IsActive = true 
        };
        var phase2User = new PromptTemplateEntity 
        { 
            Id = 4, 
            PromptKey = "classification.phase2.user", 
            Version = 10, 
            Content = "DB P2 User", 
            IsActive = true 
        };

        // Mock GetActivePromptAsync for each key
        mockRepository.Setup(r => r.GetActivePromptAsync("classification.phase1.system", It.IsAny<CancellationToken>()))
            .ReturnsAsync(phase1System);
        mockRepository.Setup(r => r.GetActivePromptAsync("classification.phase1.user", It.IsAny<CancellationToken>()))
            .ReturnsAsync(phase1User);
        mockRepository.Setup(r => r.GetActivePromptAsync("classification.phase2.system", It.IsAny<CancellationToken>()))
            .ReturnsAsync(phase2System);
        mockRepository.Setup(r => r.GetActivePromptAsync("classification.phase2.user", It.IsAny<CancellationToken>()))
            .ReturnsAsync(phase2User);

        mockServiceProvider.Setup(sp => sp.GetService(typeof(IPromptTemplateRepository)))
            .Returns(mockRepository.Object);

        mockScope.Setup(s => s.ServiceProvider).Returns(mockServiceProvider.Object);
        _mockScopeFactory.Setup(sf => sf.CreateScope()).Returns(mockScope.Object);

        var provider = CreateProvider();

        // Act
        var result = await provider.GetPromptSetAsync();

        // Assert
        result.Should().NotBeNull();
        result.Phase1SystemPrompt.Should().Be("DB P1 System");
        result.Phase1UserPrompt.Should().Be("DB P1 User");
        result.Phase2SystemPrompt.Should().Be("DB P2 System");
        result.Phase2UserPrompt.Should().Be("DB P2 User");
        result.Version.Should().Be(10);
        result.Source.Should().Be("Database");

        // Verify result is now cached
        var cachedResult = _cache.Get<ClassificationPromptSet>("classification_prompts_all");
        cachedResult.Should().NotBeNull();
        cachedResult!.Version.Should().Be(10);
    }

    [Fact]
    public async Task GetPromptSetAsync_DatabaseIncompleteSet_FallsBackToAppsettings()
    {
        // Arrange
        var mockRepository = new Mock<IPromptTemplateRepository>();
        var mockScope = new Mock<IServiceScope>();
        var mockServiceProvider = new Mock<IServiceProvider>();

        // Only 2 prompts returned (incomplete)
        mockRepository.Setup(r => r.GetActivePromptAsync("classification.phase1.system", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PromptTemplateEntity 
            { 
                Id = 1, 
                PromptKey = "classification.phase1.system", 
                Version = 10, 
                Content = "DB P1 System", 
                IsActive = true 
            });
        mockRepository.Setup(r => r.GetActivePromptAsync("classification.phase1.user", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PromptTemplateEntity 
            { 
                Id = 2, 
                PromptKey = "classification.phase1.user", 
                Version = 10, 
                Content = "DB P1 User", 
                IsActive = true 
            });
        mockRepository.Setup(r => r.GetActivePromptAsync("classification.phase2.system", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PromptTemplateEntity?)null);  // Missing!
        mockRepository.Setup(r => r.GetActivePromptAsync("classification.phase2.user", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PromptTemplateEntity?)null);  // Missing!

        mockServiceProvider.Setup(sp => sp.GetService(typeof(IPromptTemplateRepository)))
            .Returns(mockRepository.Object);

        mockScope.Setup(s => s.ServiceProvider).Returns(mockServiceProvider.Object);
        _mockScopeFactory.Setup(sf => sf.CreateScope()).Returns(mockScope.Object);

        var provider = CreateProvider();

        // Act
        var result = await provider.GetPromptSetAsync();

        // Assert
        result.Should().NotBeNull();
        result.Phase1SystemPrompt.Should().Be("Fallback Phase1 System");
        result.Phase1UserPrompt.Should().Be("Fallback Phase1 User");
        result.Phase2SystemPrompt.Should().Be("Fallback Phase2 System");
        result.Phase2UserPrompt.Should().Be("Fallback Phase2 User");
        result.Version.Should().Be(0);
        result.Source.Should().Be("Fallback");
    }

    [Fact]
    public async Task GetPromptSetAsync_DatabaseException_FallsBackToAppsettings()
    {
        // Arrange
        var mockRepository = new Mock<IPromptTemplateRepository>();
        var mockScope = new Mock<IServiceScope>();
        var mockServiceProvider = new Mock<IServiceProvider>();

        mockRepository.Setup(r => r.GetActivePromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database connection failed"));

        mockServiceProvider.Setup(sp => sp.GetService(typeof(IPromptTemplateRepository)))
            .Returns(mockRepository.Object);

        mockScope.Setup(s => s.ServiceProvider).Returns(mockServiceProvider.Object);
        _mockScopeFactory.Setup(sf => sf.CreateScope()).Returns(mockScope.Object);

        var provider = CreateProvider();

        // Act
        var result = await provider.GetPromptSetAsync();

        // Assert
        result.Should().NotBeNull();
        result.Phase1SystemPrompt.Should().Be("Fallback Phase1 System");
        result.Version.Should().Be(0);
        result.Source.Should().Be("Fallback");

        // Verify LogError was called (database failure)
        _mockLogger.Verify(
            logger => logger.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Database query FAILED")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetPromptSetAsync_VersionMismatch_LogsWarningButReturnsData()
    {
        // Arrange
        var mockRepository = new Mock<IPromptTemplateRepository>();
        var mockScope = new Mock<IServiceScope>();
        var mockServiceProvider = new Mock<IServiceProvider>();

        // Prompts with mismatched versions
        mockRepository.Setup(r => r.GetActivePromptAsync("classification.phase1.system", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PromptTemplateEntity 
            { 
                Id = 1, 
                PromptKey = "classification.phase1.system", 
                Version = 10, 
                Content = "DB P1 System", 
                IsActive = true 
            });
        mockRepository.Setup(r => r.GetActivePromptAsync("classification.phase1.user", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PromptTemplateEntity 
            { 
                Id = 2, 
                PromptKey = "classification.phase1.user", 
                Version = 10, 
                Content = "DB P1 User", 
                IsActive = true 
            });
        mockRepository.Setup(r => r.GetActivePromptAsync("classification.phase2.system", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PromptTemplateEntity 
            { 
                Id = 3, 
                PromptKey = "classification.phase2.system", 
                Version = 10, 
                Content = "DB P2 System", 
                IsActive = true 
            });
        mockRepository.Setup(r => r.GetActivePromptAsync("classification.phase2.user", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PromptTemplateEntity 
            { 
                Id = 4, 
                PromptKey = "classification.phase2.user", 
                Version = 11, // Different version!
                Content = "DB P2 User", 
                IsActive = true 
            });

        mockServiceProvider.Setup(sp => sp.GetService(typeof(IPromptTemplateRepository)))
            .Returns(mockRepository.Object);

        mockScope.Setup(s => s.ServiceProvider).Returns(mockServiceProvider.Object);
        _mockScopeFactory.Setup(sf => sf.CreateScope()).Returns(mockScope.Object);

        var provider = CreateProvider();

        // Act
        var result = await provider.GetPromptSetAsync();

        // Assert - Despite version mismatch, should return DB data (not fallback)
        result.Should().NotBeNull();
        result.Phase1SystemPrompt.Should().Be("DB P1 System");
        result.Phase1UserPrompt.Should().Be("DB P1 User");
        result.Phase2SystemPrompt.Should().Be("DB P2 System");
        result.Phase2UserPrompt.Should().Be("DB P2 User");
        result.Version.Should().Be(10); // Version from first prompt
        result.Source.Should().Be("Database");

        // Verify LogWarning was called for version mismatch
        _mockLogger.Verify(
            logger => logger.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("VERSION MISMATCH")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetPromptSetAsync_ConsistentVersions_LogsConfirmation()
    {
        // Arrange
        var mockRepository = new Mock<IPromptTemplateRepository>();
        var mockScope = new Mock<IServiceScope>();
        var mockServiceProvider = new Mock<IServiceProvider>();

        mockRepository.Setup(r => r.GetActivePromptAsync("classification.phase1.system", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PromptTemplateEntity 
            { 
                Id = 1, 
                PromptKey = "classification.phase1.system", 
                Version = 15, 
                Content = "DB P1 System", 
                IsActive = true 
            });
        mockRepository.Setup(r => r.GetActivePromptAsync("classification.phase1.user", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PromptTemplateEntity 
            { 
                Id = 2, 
                PromptKey = "classification.phase1.user", 
                Version = 15, 
                Content = "DB P1 User", 
                IsActive = true 
            });
        mockRepository.Setup(r => r.GetActivePromptAsync("classification.phase2.system", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PromptTemplateEntity 
            { 
                Id = 3, 
                PromptKey = "classification.phase2.system", 
                Version = 15, 
                Content = "DB P2 System", 
                IsActive = true 
            });
        mockRepository.Setup(r => r.GetActivePromptAsync("classification.phase2.user", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PromptTemplateEntity 
            { 
                Id = 4, 
                PromptKey = "classification.phase2.user", 
                Version = 15, 
                Content = "DB P2 User", 
                IsActive = true 
            });

        mockServiceProvider.Setup(sp => sp.GetService(typeof(IPromptTemplateRepository)))
            .Returns(mockRepository.Object);

        mockScope.Setup(s => s.ServiceProvider).Returns(mockServiceProvider.Object);
        _mockScopeFactory.Setup(sf => sf.CreateScope()).Returns(mockScope.Object);

        var provider = CreateProvider();

        // Act
        var result = await provider.GetPromptSetAsync();

        // Assert
        result.Should().NotBeNull();
        result.Phase1SystemPrompt.Should().Be("DB P1 System");
        result.Version.Should().Be(15);
        result.Source.Should().Be("Database");

        // Verify LogInformation was called for consistent version confirmation
        _mockLogger.Verify(
            logger => logger.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("All 4 prompts loaded from database with consistent version")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetPromptSetAsync_FallbackCachesWithVersion0()
    {
        // Arrange
        var mockRepository = new Mock<IPromptTemplateRepository>();
        var mockScope = new Mock<IServiceScope>();
        var mockServiceProvider = new Mock<IServiceProvider>();

        // Empty result from DB (all nulls)
        mockRepository.Setup(r => r.GetActivePromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PromptTemplateEntity?)null);

        mockServiceProvider.Setup(sp => sp.GetService(typeof(IPromptTemplateRepository)))
            .Returns(mockRepository.Object);

        mockScope.Setup(s => s.ServiceProvider).Returns(mockServiceProvider.Object);
        _mockScopeFactory.Setup(sf => sf.CreateScope()).Returns(mockScope.Object);

        var provider = CreateProvider();

        // Act
        var result = await provider.GetPromptSetAsync();

        // Assert
        result.Should().NotBeNull();
        result.Version.Should().Be(0);
        result.Source.Should().Be("Fallback");

        // Verify fallback is cached
        var cachedResult = _cache.Get<ClassificationPromptSet>("classification_prompts_all");
        cachedResult.Should().NotBeNull();
        cachedResult!.Version.Should().Be(0);
        cachedResult.Source.Should().Be("Fallback");
    }
}
