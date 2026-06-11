using Xunit;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;
using System.Text;
using System.Text.Json;
using DocumentIA.Data.Context;
using DocumentIA.Data.Entities;
using DocumentIA.Core.Models;
using DocumentIA.Functions.Triggers.Admin;

namespace DocumentIA.Functions.Tests.Triggers.Admin;

/// <summary>
/// Tests for PromptsAdminFunction - Admin API CRUD operations on PromptTemplates.
/// </summary>
public class PromptsAdminFunctionTests : IDisposable
{
    private readonly DocumentIADbContext _dbContext;
    private readonly Mock<ILogger<PromptsAdminFunction>> _mockLogger;
    private readonly PromptsAdminFunction _function;

    public PromptsAdminFunctionTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<DocumentIADbContext>()
            .UseInMemoryDatabase(databaseName: $"PromptsAdminTests_{Guid.NewGuid()}")
            .Options;

        _dbContext = new DocumentIADbContext(options);
        _mockLogger = new Mock<ILogger<PromptsAdminFunction>>();
        _function = new PromptsAdminFunction(_dbContext, _mockLogger.Object);
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    // ==================== Helper Methods ====================

    private HttpRequestData CreateMockHttpRequest(string method, string? body = null)
    {
        var mockFunctionContext = new Mock<FunctionContext>();
        var mockRequest = new Mock<HttpRequestData>(mockFunctionContext.Object);

        mockRequest.Setup(r => r.Method).Returns(method);

        if (body != null)
        {
            var stream = new MemoryStream(Encoding.UTF8.GetBytes(body));
            mockRequest.Setup(r => r.Body).Returns(stream);
        }

        mockRequest.Setup(r => r.CreateResponse()).Returns(() =>
        {
            var responseStream = new MemoryStream();
            var mockResponse = new Mock<HttpResponseData>(mockFunctionContext.Object);
            
            mockResponse.Setup(r => r.Body).Returns(responseStream);
            mockResponse.SetupProperty(r => r.StatusCode);
            mockResponse.Setup(r => r.Headers).Returns(new Microsoft.Azure.Functions.Worker.Http.HttpHeadersCollection());

            // Mock WriteAsJsonAsync to manually serialize to the Body stream
            mockResponse.Setup(r => r.WriteAsJsonAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .Returns<object, CancellationToken>((obj, ct) =>
                {
                    var json = JsonSerializer.Serialize(obj, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                    var bytes = Encoding.UTF8.GetBytes(json);
                    responseStream.Write(bytes, 0, bytes.Length);
                    responseStream.Position = 0;
                    return new ValueTask(Task.CompletedTask);
                });

            return mockResponse.Object;
        });

        return mockRequest.Object;
    }

    private async Task<(HttpStatusCode status, T? data)> ExecuteAndDeserialize<T>(Func<Task<HttpResponseData>> action)
    {
        var response = await action();
        var status = response.StatusCode;

        response.Body.Position = 0;
        using var reader = new StreamReader(response.Body);
        var json = await reader.ReadToEndAsync();

        var data = string.IsNullOrEmpty(json) ? default : JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return (status, data);
    }

    // ==================== Tests: GET Admin_GetPromptTemplates ====================

    [Fact]
    public async Task Admin_GetPromptTemplates_EmptyDatabase_ReturnsEmptyList()
    {
        // Arrange
        var request = CreateMockHttpRequest("GET");

        // Act
        var (status, data) = await ExecuteAndDeserialize<List<PromptTemplateListItemDto>>(
            () => _function.GetPromptTemplates(request));

        // Assert
        status.Should().Be(HttpStatusCode.OK);
        data.Should().NotBeNull();
        data!.Should().BeEmpty();
    }

    [Fact]
    public async Task Admin_GetPromptTemplates_WithData_ReturnsOrderedList()
    {
        // Arrange
        _dbContext.PromptTemplates.AddRange(
            new PromptTemplateEntity
            {
                PromptKey = "classification.phase1.system",
                Version = 1,
                Content = "System prompt v1 content",
                IsActive = false,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = "user1"
            },
            new PromptTemplateEntity
            {
                PromptKey = "classification.phase1.system",
                Version = 2,
                Content = "System prompt v2 content - active",
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = "user2"
            },
            new PromptTemplateEntity
            {
                PromptKey = "classification.phase1.user",
                Version = 1,
                Content = "User prompt v1 content",
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = "user3"
            }
        );
        await _dbContext.SaveChangesAsync();

        var request = CreateMockHttpRequest("GET");

        // Act
        var (status, data) = await ExecuteAndDeserialize<List<PromptTemplateListItemDto>>(
            () => _function.GetPromptTemplates(request));

        // Assert
        status.Should().Be(HttpStatusCode.OK);
        data.Should().NotBeNull();
        data!.Should().HaveCount(3);

        // Should be ordered by PromptKey, Version DESC
        data[0].PromptKey.Should().Be("classification.phase1.system");
        data[0].Version.Should().Be(2);
        data[1].PromptKey.Should().Be("classification.phase1.system");
        data[1].Version.Should().Be(1);
        data[2].PromptKey.Should().Be("classification.phase1.user");
        data[2].Version.Should().Be(1);
    }

    // ==================== Tests: GET Admin_GetPromptTemplateById ====================

    [Fact]
    public async Task Admin_GetPromptTemplateById_ExistingId_ReturnsPrompt()
    {
        // Arrange
        var entity = new PromptTemplateEntity
        {
            PromptKey = "classification.phase2.system",
            Version = 1,
            Content = "Phase2 system prompt content",
            Description = "Test description",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "admin"
        };
        _dbContext.PromptTemplates.Add(entity);
        await _dbContext.SaveChangesAsync();

        var request = CreateMockHttpRequest("GET");

        // Act
        var (status, data) = await ExecuteAndDeserialize<PromptTemplateDto>(
            () => _function.GetPromptTemplateById(request, entity.Id));

        // Assert
        status.Should().Be(HttpStatusCode.OK);
        data.Should().NotBeNull();
        data!.Id.Should().Be(entity.Id);
        data.PromptKey.Should().Be("classification.phase2.system");
        data.Version.Should().Be(1);
        data.IsActive.Should().BeTrue();
        data.Description.Should().Be("Test description");
    }

    [Fact]
    public async Task Admin_GetPromptTemplateById_NonExistingId_ReturnsNotFound()
    {
        // Arrange
        var request = CreateMockHttpRequest("GET");

        // Act
        var response = await _function.GetPromptTemplateById(request, 99999);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ==================== Tests: POST Admin_CreatePromptTemplate ====================

    [Fact]
    public async Task Admin_CreatePromptTemplate_ValidPayload_CreatesNewPrompt()
    {
        // Arrange
        var payload = new CreatePromptTemplateRequest
        {
            PromptKey = "classification.phase1.system",
            Content = "Valid {CONTEXT_PROMPT} content with {DOCUMENT_TEXT} placeholder",
            Description = "Test prompt",
            CreatedBy = "testuser"
        };
        var request = CreateMockHttpRequest("POST", JsonSerializer.Serialize(payload));

        // Act
        var (status, data) = await ExecuteAndDeserialize<PromptTemplateDto>(
            () => _function.CreatePromptTemplate(request));

        // Assert
        status.Should().Be(HttpStatusCode.Created);
        data.Should().NotBeNull();
        data!.PromptKey.Should().Be("classification.phase1.system");
        data.Version.Should().Be(1); // First version
        data.IsActive.Should().BeFalse(); // Always created as draft
        data.Description.Should().Be("Test prompt");
        data.CreatedBy.Should().Be("testuser");

        // Verify in database
        var dbEntity = await _dbContext.PromptTemplates.FirstOrDefaultAsync(p => p.Id == data.Id);
        dbEntity.Should().NotBeNull();
    }

    [Fact]
    public async Task Admin_CreatePromptTemplate_AutoVersioning_IncrementsProperly()
    {
        // Arrange - existing version 2
        _dbContext.PromptTemplates.Add(new PromptTemplateEntity
        {
            PromptKey = "classification.phase2.user",
            Version = 2,
            Content = "Existing {TDN1_CODE} content {DOCUMENT_TEXT}",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "admin"
        });
        await _dbContext.SaveChangesAsync();

        var payload = new CreatePromptTemplateRequest
        {
            PromptKey = "classification.phase2.user",
            Content = "New version {TDN1_CODE} content {DOCUMENT_TEXT}",
            Description = "New draft",
            CreatedBy = "user2"
        };
        var request = CreateMockHttpRequest("POST", JsonSerializer.Serialize(payload));

        // Act
        var (status, data) = await ExecuteAndDeserialize<PromptTemplateDto>(
            () => _function.CreatePromptTemplate(request));

        // Assert
        status.Should().Be(HttpStatusCode.Created);
        data!.Version.Should().Be(3); // MAX(2) + 1
    }

    [Fact]
    public async Task Admin_CreatePromptTemplate_ContentTooShort_ReturnsBadRequest()
    {
        // Arrange
        var payload = new CreatePromptTemplateRequest
        {
            PromptKey = "classification.phase1.system",
            Content = "Short", // Less than 10 chars
            CreatedBy = "testuser"
        };
        var request = CreateMockHttpRequest("POST", JsonSerializer.Serialize(payload));

        // Act
        var (status, error) = await ExecuteAndDeserialize<ValidationErrorResponse>(
            () => _function.CreatePromptTemplate(request));

        // Assert
        status.Should().Be(HttpStatusCode.BadRequest);
        error.Should().NotBeNull();
        error!.Errors.Should().NotBeNull();
        var allErrors = string.Join(" ", error!.Errors!.Values.SelectMany(v => v));
        allErrors.Should().Contain("10");
        allErrors.Should().Contain("16000");
    }

    [Fact]
    public async Task Admin_CreatePromptTemplate_MissingRequiredPlaceholder_ReturnsBadRequest()
    {
        // Arrange - Phase1 requires CONTEXT_PROMPT and DOCUMENT_TEXT
        var payload = new CreatePromptTemplateRequest
        {
            PromptKey = "classification.phase1.system",
            Content = "Content without required placeholders just plain text here",
            CreatedBy = "testuser"
        };
        var request = CreateMockHttpRequest("POST", JsonSerializer.Serialize(payload));

        // Act
        var (status, error) = await ExecuteAndDeserialize<ValidationErrorResponse>(
            () => _function.CreatePromptTemplate(request));

        // Assert
        status.Should().Be(HttpStatusCode.BadRequest);
        error.Should().NotBeNull();
        error!.Errors.Should().NotBeNull();
        var allErrors = string.Join(" ", error!.Errors!.Values.SelectMany(v => v));
        (allErrors.Contains("CONTEXT_PROMPT") || allErrors.Contains("DOCUMENT_TEXT")).Should().BeTrue();
    }

    [Fact]
    public async Task Admin_CreatePromptTemplate_InvalidPlaceholder_ReturnsBadRequest()
    {
        // Arrange
        var payload = new CreatePromptTemplateRequest
        {
            PromptKey = "classification.phase1.system",
            Content = "Valid {CONTEXT_PROMPT} and {DOCUMENT_TEXT} but also {INVALID_PLACEHOLDER}",
            CreatedBy = "testuser"
        };
        var request = CreateMockHttpRequest("POST", JsonSerializer.Serialize(payload));

        // Act
        var (status, error) = await ExecuteAndDeserialize<ValidationErrorResponse>(
            () => _function.CreatePromptTemplate(request));

        // Assert
        status.Should().Be(HttpStatusCode.BadRequest);
        error.Should().NotBeNull();
        error!.Errors.Should().NotBeNull();
        var allErrors = string.Join(" ", error!.Errors!.Values.SelectMany(v => v));
        allErrors.Should().Contain("INVALID_PLACEHOLDER");
    }

    // ==================== Tests: PUT Admin_UpdatePromptTemplate ====================

    [Fact]
    public async Task Admin_UpdatePromptTemplate_ValidUpdate_UpdatesDraft()
    {
        // Arrange
        var entity = new PromptTemplateEntity
        {
            PromptKey = "classification.phase1.system",
            Version = 1,
            Content = "Old {CONTEXT_PROMPT} content {DOCUMENT_TEXT}",
            IsActive = false, // Draft
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "admin"
        };
        _dbContext.PromptTemplates.Add(entity);
        await _dbContext.SaveChangesAsync();

        var payload = new UpdatePromptTemplateRequest
        {
            Content = "Updated {CONTEXT_PROMPT} content {DOCUMENT_TEXT}",
            Description = "Updated description",
            UpdatedBy = "editor"
        };
        var request = CreateMockHttpRequest("PUT", JsonSerializer.Serialize(payload));

        // Act
        var (status, data) = await ExecuteAndDeserialize<PromptTemplateDto>(
            () => _function.UpdatePromptTemplate(request, entity.Id));

        // Assert
        status.Should().Be(HttpStatusCode.OK);
        data.Should().NotBeNull();
        data!.Content.Should().Be("Updated {CONTEXT_PROMPT} content {DOCUMENT_TEXT}");
        data.Description.Should().Be("Updated description");
        data.UpdatedBy.Should().Be("editor");
        data.UpdatedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Admin_UpdatePromptTemplate_ActivePrompt_ReturnsForbidden()
    {
        // Arrange
        var entity = new PromptTemplateEntity
        {
            PromptKey = "classification.phase1.system",
            Version = 1,
            Content = "Active {CONTEXT_PROMPT} content {DOCUMENT_TEXT}",
            IsActive = true, // Active
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "admin"
        };
        _dbContext.PromptTemplates.Add(entity);
        await _dbContext.SaveChangesAsync();

        var payload = new UpdatePromptTemplateRequest
        {
            Content = "Trying to update {CONTEXT_PROMPT} active {DOCUMENT_TEXT}",
            UpdatedBy = "hacker"
        };
        var request = CreateMockHttpRequest("PUT", JsonSerializer.Serialize(payload));

        // Act
        var response = await _function.UpdatePromptTemplate(request, entity.Id);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ==================== Tests: PUT Admin_ActivatePromptVersion ====================

    [Fact]
    public async Task Admin_ActivatePromptVersion_ValidActivation_DeactivatesPreviousAndActivatesNew()
    {
        // Arrange
        var oldActive = new PromptTemplateEntity
        {
            PromptKey = "classification.phase1.system",
            Version = 1,
            Content = "Old {CONTEXT_PROMPT} active {DOCUMENT_TEXT}",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-1),
            CreatedBy = "admin"
        };
        var newDraft = new PromptTemplateEntity
        {
            PromptKey = "classification.phase1.system",
            Version = 2,
            Content = "New {CONTEXT_PROMPT} draft {DOCUMENT_TEXT}",
            IsActive = false,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "admin"
        };
        _dbContext.PromptTemplates.AddRange(oldActive, newDraft);
        await _dbContext.SaveChangesAsync();

        var payload = new ActivatePromptVersionRequest
        {
            Id = newDraft.Id,
            PublishedBy = "publisher"
        };
        var request = CreateMockHttpRequest("PUT", JsonSerializer.Serialize(payload));

        // Act
        var (status, data) = await ExecuteAndDeserialize<PromptTemplateDto>(
            () => _function.ActivatePromptVersion(request, newDraft.Id));

        // Assert
        status.Should().Be(HttpStatusCode.OK);
        data.Should().NotBeNull();
        data!.IsActive.Should().BeTrue();
        data.PublishedBy.Should().Be("publisher");
        data.PublishedAtUtc.Should().NotBeNull();

        // Verify old active is now deactivated
        await _dbContext.Entry(oldActive).ReloadAsync();
        oldActive.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Admin_ActivatePromptVersion_NonExistingId_ReturnsNotFound()
    {
        // Arrange
        var payload = new ActivatePromptVersionRequest
        {
            Id = 99999,
            PublishedBy = "publisher"
        };
        var request = CreateMockHttpRequest("PUT", JsonSerializer.Serialize(payload));

        // Act
        var response = await _function.ActivatePromptVersion(request, 99999);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ==================== Tests: POST Admin_RollbackPromptVersion ====================

    [Fact]
    public async Task Admin_RollbackPromptVersion_ValidRollback_ActivatesTargetVersion()
    {
        // Arrange
        var v1 = new PromptTemplateEntity
        {
            PromptKey = "classification.phase2.user",
            Version = 1,
            Content = "Version 1 {TDN1_CODE} content {DOCUMENT_TEXT}",
            IsActive = false,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-2),
            CreatedBy = "admin"
        };
        var v2Active = new PromptTemplateEntity
        {
            PromptKey = "classification.phase2.user",
            Version = 2,
            Content = "Version 2 {TDN1_CODE} active {DOCUMENT_TEXT}",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-1),
            CreatedBy = "admin",
            PublishedAtUtc = DateTime.UtcNow.AddDays(-1),
            PublishedBy = "publisher1"
        };
        _dbContext.PromptTemplates.AddRange(v1, v2Active);
        await _dbContext.SaveChangesAsync();

        var payload = new RollbackPromptVersionRequest
        {
            PromptKey = "classification.phase2.user",
            TargetVersion = 1,
            PublishedBy = "rollback_admin"
        };
        var request = CreateMockHttpRequest("POST", JsonSerializer.Serialize(payload));

        // Act
        var (status, data) = await ExecuteAndDeserialize<PromptTemplateDto>(
            () => _function.RollbackPromptVersion(request));

        // Assert
        status.Should().Be(HttpStatusCode.OK);
        data.Should().NotBeNull();
        data!.Version.Should().Be(1);
        data.IsActive.Should().BeTrue();
        data.PublishedBy.Should().Be("rollback_admin");

        // Verify v2 is deactivated
        await _dbContext.Entry(v2Active).ReloadAsync();
        v2Active.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Admin_RollbackPromptVersion_TargetNotFound_ReturnsNotFound()
    {
        // Arrange
        var payload = new RollbackPromptVersionRequest
        {
            PromptKey = "classification.phase1.system",
            TargetVersion = 999,
            PublishedBy = "admin"
        };
        var request = CreateMockHttpRequest("POST", JsonSerializer.Serialize(payload));

        // Act
        var response = await _function.RollbackPromptVersion(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ==================== Tests: DELETE Admin_DeletePromptTemplate ====================

    [Fact]
    public async Task Admin_DeletePromptTemplate_DraftPrompt_DeletesSuccessfully()
    {
        // Arrange
        var draftEntity = new PromptTemplateEntity
        {
            PromptKey = "classification.phase1.user",
            Version = 1,
            Content = "Draft {CONTEXT_PROMPT} content {DOCUMENT_TEXT}",
            IsActive = false,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "admin"
        };
        _dbContext.PromptTemplates.Add(draftEntity);
        await _dbContext.SaveChangesAsync();

        var request = CreateMockHttpRequest("DELETE");

        // Act
        var response = await _function.DeletePromptTemplate(request, draftEntity.Id);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify deleted from database
        var deleted = await _dbContext.PromptTemplates.FindAsync(draftEntity.Id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task Admin_DeletePromptTemplate_ActivePrompt_ReturnsForbidden()
    {
        // Arrange
        var activeEntity = new PromptTemplateEntity
        {
            PromptKey = "classification.phase1.user",
            Version = 1,
            Content = "Active {CONTEXT_PROMPT} content {DOCUMENT_TEXT}",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "admin"
        };
        _dbContext.PromptTemplates.Add(activeEntity);
        await _dbContext.SaveChangesAsync();

        var request = CreateMockHttpRequest("DELETE");

        // Act
        var response = await _function.DeletePromptTemplate(request, activeEntity.Id);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Verify NOT deleted from database
        var stillExists = await _dbContext.PromptTemplates.FindAsync(activeEntity.Id);
        stillExists.Should().NotBeNull();
    }

    [Fact]
    public async Task Admin_DeletePromptTemplate_NonExistingId_ReturnsNotFound()
    {
        // Arrange
        var request = CreateMockHttpRequest("DELETE");

        // Act
        var response = await _function.DeletePromptTemplate(request, 99999);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ==================== Tests: GET Admin_GetPromptTemplatesByKey ====================

    [Fact]
    public async Task Admin_GetPromptTemplatesByKey_MultipleVersions_ReturnsOrderedByVersion()
    {
        // Arrange
        _dbContext.PromptTemplates.AddRange(
            new PromptTemplateEntity
            {
                PromptKey = "classification.phase2.system",
                Version = 3,
                Content = "v3 {TDN1_CODE} {DOCUMENT_TEXT}",
                IsActive = false,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = "admin"
            },
            new PromptTemplateEntity
            {
                PromptKey = "classification.phase2.system",
                Version = 1,
                Content = "v1 {TDN1_CODE} {DOCUMENT_TEXT}",
                IsActive = false,
                CreatedAtUtc = DateTime.UtcNow.AddDays(-2),
                CreatedBy = "admin"
            },
            new PromptTemplateEntity
            {
                PromptKey = "classification.phase2.system",
                Version = 2,
                Content = "v2 {TDN1_CODE} {DOCUMENT_TEXT}",
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow.AddDays(-1),
                CreatedBy = "admin"
            }
        );
        await _dbContext.SaveChangesAsync();

        var request = CreateMockHttpRequest("GET");

        // Act
        var (status, data) = await ExecuteAndDeserialize<List<PromptTemplateDto>>(
            () => _function.GetPromptTemplatesByKey(request, "classification.phase2.system"));

        // Assert
        status.Should().Be(HttpStatusCode.OK);
        data.Should().NotBeNull();
        data!.Should().HaveCount(3);
        data[0].Version.Should().Be(3); // DESC order
        data[1].Version.Should().Be(2);
        data[2].Version.Should().Be(1);
    }

    [Fact]
    public async Task Admin_GetPromptTemplatesByKey_NonExistingKey_ReturnsEmptyList()
    {
        // Arrange
        var request = CreateMockHttpRequest("GET");

        // Act
        var (status, data) = await ExecuteAndDeserialize<List<PromptTemplateDto>>(
            () => _function.GetPromptTemplatesByKey(request, "nonexisting.key"));

        // Assert
        status.Should().Be(HttpStatusCode.OK);
        data.Should().NotBeNull();
        data!.Should().BeEmpty();
    }
}
