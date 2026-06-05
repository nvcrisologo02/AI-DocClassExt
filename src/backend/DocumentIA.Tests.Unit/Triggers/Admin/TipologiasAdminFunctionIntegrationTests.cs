using System.Reflection;
using DocumentIA.Core.Configuration;
using DocumentIA.Core.Mappers;
using DocumentIA.Data.Context;
using DocumentIA.Data.Entities;
using DocumentIA.Data.Repositories;
using DocumentIA.Functions.Triggers.Admin;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace DocumentIA.Tests.Unit.Triggers.Admin;

/// <summary>
/// Covers additional validation paths in TipologiasAdminFunction:
/// version format, config/payload coherence, model references, and confidence config ordering.
/// Complements TipologiasAdminFunctionValidationTests with integration-level scenarios.
/// </summary>
public class TipologiasAdminFunctionIntegrationTests
{
    // ─── Version format ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("1")]
    [InlineData("1.2.3.4")]
    [InlineData("v1.0")]
    public async Task ValidateTipologiaRequestAsync_WhenVersionFormatInvalid_ReturnsError(string version)
    {
        await using var db = CreateDbContext();
        var function = CreateFunction(db);
        var method = GetValidateTipologiaRequestAsyncMethod();

        var payload = CreatePayload("nota-simple", "Nota simple", version);
        var config = MakeMinimalConfig("nota-simple", version);

        var task = (Task<string?>)method.Invoke(function, new object?[] { payload, config, null })!;
        var result = await task;

        result.Should().Be("Version debe usar formato tipo 1.0 o 1.0.0.");
    }

    // ─── Payload/config coherence ─────────────────────────────────────────────

    [Fact]
    public async Task ValidateTipologiaRequestAsync_WhenTipologiaIdDoesNotMatchCodigo_ReturnsError()
    {
        await using var db = CreateDbContext();
        var function = CreateFunction(db);
        var method = GetValidateTipologiaRequestAsyncMethod();

        var payload = CreatePayload("nota-simple", "Nota simple", "1.0.0");
        var config = MakeMinimalConfig("otro-codigo", "1.0.0"); // mismatch

        var task = (Task<string?>)method.Invoke(function, new object?[] { payload, config, null })!;
        var result = await task;

        result.Should().Contain("tipologiaId");
    }

    [Fact]
    public async Task ValidateTipologiaRequestAsync_WhenConfigVersionDoesNotMatchPayload_ReturnsError()
    {
        await using var db = CreateDbContext();
        var function = CreateFunction(db);
        var method = GetValidateTipologiaRequestAsyncMethod();

        var payload = CreatePayload("nota-simple", "Nota simple", "2.0.0");
        var config = MakeMinimalConfig("nota-simple", "1.0.0"); // version mismatch

        var task = (Task<string?>)method.Invoke(function, new object?[] { payload, config, null })!;
        var result = await task;

        result.Should().Contain("version");
    }

    // ─── Legacy code with dot ────────────────────────────────────────────────

    [Fact]
    public async Task ValidateTipologiaRequestAsync_WhenCodigoHasDot_IsAccepted()
    {
        await using var db = CreateDbContext();
        var function = CreateFunction(db);
        var method = GetValidateTipologiaRequestAsyncMethod();

        var payload = CreatePayload("nota.simple", "Nota simple", "1.0.0");
        var config = MakeMinimalConfig("nota.simple", "1.0.0");

        var task = (Task<string?>)method.Invoke(function, new object?[] { payload, config, null })!;
        var result = await task;

        result.Should().BeNull();
    }

    // ─── Model references ────────────────────────────────────────────────────

    [Fact]
    public async Task ValidateTipologiaRequestAsync_WhenExtractionEnabledAndModelKeyEmpty_ReturnsError()
    {
        await using var db = CreateDbContext();
        var function = CreateFunction(db);
        var method = GetValidateTipologiaRequestAsyncMethod();

        var payload = CreatePayload("mi-tipo", "Mi tipo", "1.0.0");
        var config = MakeMinimalConfig("mi-tipo", "1.0.0");
        config.Extraction = new TipologiaExtractionConfig
        {
            Enabled = true,
            Provider = "ContentUnderstanding",
            ModelKey = string.Empty // empty → error
        };

        var task = (Task<string?>)method.Invoke(function, new object?[] { payload, config, null })!;
        var result = await task;

        result.Should().Contain("modelKey");
    }

    [Fact]
    public async Task ValidateTipologiaRequestAsync_WhenExtractionModelKeyNotInDb_ReturnsError()
    {
        await using var db = CreateDbContext();
        var function = CreateFunction(db);
        var method = GetValidateTipologiaRequestAsyncMethod();

        var payload = CreatePayload("mi-tipo", "Mi tipo", "1.0.0");
        var config = MakeMinimalConfig("mi-tipo", "1.0.0");
        config.Extraction = new TipologiaExtractionConfig
        {
            Enabled = true,
            Provider = "ContentUnderstanding",
            ModelKey = "modelo-inexistente"
        };

        var task = (Task<string?>)method.Invoke(function, new object?[] { payload, config, null })!;
        var result = await task;

        result.Should().Contain("modelo-inexistente");
    }

    [Fact]
    public async Task ValidateTipologiaRequestAsync_WhenExtractionModelActiveInDb_ReturnsNull()
    {
        await using var db = CreateDbContext();
        db.ModeloConfigs.Add(new ModeloConfigEntity
        {
            Key = "cu-mi-tipo",
            Tipo = TipoModelo.Extraccion,
            Provider = "ContentUnderstanding",
            ConfiguracionJson = "{}",
            Activo = true
        });
        await db.SaveChangesAsync();

        var function = CreateFunction(db);
        var method = GetValidateTipologiaRequestAsyncMethod();

        var payload = CreatePayload("mi-tipo", "Mi tipo", "1.0.0");
        var config = MakeMinimalConfig("mi-tipo", "1.0.0");
        config.Extraction = new TipologiaExtractionConfig
        {
            Enabled = true,
            Provider = "ContentUnderstanding",
            ModelKey = "cu-mi-tipo"
        };

        var task = (Task<string?>)method.Invoke(function, new object?[] { payload, config, null })!;
        var result = await task;

        result.Should().BeNull();
    }

    [Fact]
    public async Task ValidateTipologiaRequestAsync_WhenPromptModelKeyNotInDb_ReturnsError()
    {
        await using var db = CreateDbContext();
        var function = CreateFunction(db);
        var method = GetValidateTipologiaRequestAsyncMethod();

        var payload = CreatePayload("mi-tipo", "Mi tipo", "1.0.0");
        var config = MakeMinimalConfig("mi-tipo", "1.0.0");
        config.PromptConfig = new PromptConfig
        {
            Enabled = true,
            ModelKey = "gpt-inexistente",
            SystemPrompt = "Prompt",
            UserPromptTemplate = "Template"
        };

        var task = (Task<string?>)method.Invoke(function, new object?[] { payload, config, null })!;
        var result = await task;

        result.Should().Contain("gpt-inexistente");
    }

    // ─── ConfidenceConfig ordering ────────────────────────────────────────────

    [Fact]
    public void ValidateBusinessRules_WhenUmbralRevisionGreaterThanUmbralOK_ReturnsError()
    {
        var method = GetValidateBusinessRulesMethod();
        var config = MakeMinimalConfig("mi-tipo", "1.0.0");
        config.ConfidenceConfig = new ConfidenceConfig
        {
            ClasifUmbralFallback = 0.5,
            UmbralOK = 0.7,
            UmbralRevision = 0.9 // greater than UmbralOK → error
        };

        var result = method.Invoke(null, new object?[] { config }) as string;

        result.Should().Contain("umbralRevision");
    }

    [Fact]
    public void ValidateBusinessRules_WhenAllUmbralValuesValid_ReturnsNull()
    {
        var method = GetValidateBusinessRulesMethod();
        var config = MakeMinimalConfig("mi-tipo", "1.0.0");
        config.ConfidenceConfig = new ConfidenceConfig
        {
            ClasifUmbralFallback = 0.85,
            UmbralOK = 0.8,
            UmbralRevision = 0.5
        };

        var result = method.Invoke(null, new object?[] { config }) as string;

        result.Should().BeNull();
    }

    // ─── FieldMappings coherence ──────────────────────────────────────────────

    [Fact]
    public void ValidateBusinessRules_WhenFieldMappingTargetsExistingField_ReturnsNull()
    {
        var method = GetValidateBusinessRulesMethod();
        var config = MakeMinimalConfig("mi-tipo", "1.0.0");
        config.Extraction = new TipologiaExtractionConfig
        {
            Enabled = true,
            Provider = "ContentUnderstanding",
            ModelKey = "cu-test",
            FieldMappings = new List<ExtractionFieldMappingConfig>
            {
                new() { TargetField = "Titular", SourcePath = "document.titular" }
            }
        };
        config.Fields = new List<FieldValidationConfig>
        {
            new() { Name = "Titular", Type = "string" }
        };

        var result = method.Invoke(null, new object?[] { config }) as string;

        result.Should().BeNull();
    }

    // ─── GDC conditional requirements ────────────────────────────────────────

    [Fact]
    public void ValidateBusinessRules_WhenGdcRequiredAndSeriePresent_ReturnsNull()
    {
        var method = GetValidateBusinessRulesMethod();
        var config = MakeMinimalConfig("mi-tipo", "1.0.0");
        config.SkipGDCUpload = false;
        config.GdcTipoDocumento = "NOTS";
        config.GdcSerie = "AI09";

        var result = method.Invoke(null, new object?[] { config }) as string;

        result.Should().BeNull();
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static TipologiaValidationConfig MakeMinimalConfig(string tipologiaId, string version)
    {
        return new TipologiaValidationConfig
        {
            TipologiaId = tipologiaId,
            TipologiaNombre = "Test tipologia",
            Version = version,
            SkipGDCUpload = true,
            Extraction = new TipologiaExtractionConfig { Enabled = false }
        };
    }

    private static object CreatePayload(string codigo, string nombre, string version)
    {
        var payloadType = typeof(TipologiasAdminFunction)
            .GetNestedType("TipologiaUpsertRequest", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("No se encontró TipologiaUpsertRequest.");

        var payload = Activator.CreateInstance(payloadType)
            ?? throw new InvalidOperationException("No se pudo crear TipologiaUpsertRequest.");

        payloadType.GetProperty("Codigo")!.SetValue(payload, codigo);
        payloadType.GetProperty("Nombre")!.SetValue(payload, nombre);
        payloadType.GetProperty("Version")!.SetValue(payload, version);
        payloadType.GetProperty("ConfiguracionJson")!.SetValue(payload, "{}");
        payloadType.GetProperty("Usuario")!.SetValue(payload, "tests");
        return payload;
    }

    private static TipologiasAdminFunction CreateFunction(DocumentIADbContext dbContext)
    {
        return new TipologiasAdminFunction(
            dbContext,
            Mock.Of<ITipologiaRepository>(),
            Mock.Of<ITipologiaConfigAuditRepository>(),
            Mock.Of<ILogger<TipologiasAdminFunction>>(),
            new MemoryCache(new MemoryCacheOptions()),
            new TipologiaMapper(Mock.Of<ILogger<TipologiaMapper>>()));
    }

    private static DocumentIADbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<DocumentIADbContext>()
            .UseInMemoryDatabase($"tipologias-integration-tests-{Guid.NewGuid()}")
            .Options;
        return new DocumentIADbContext(options);
    }

    private static MethodInfo GetValidateTipologiaRequestAsyncMethod()
    {
        return typeof(TipologiasAdminFunction)
            .GetMethod("ValidateTipologiaRequestAsync", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("No se encontró ValidateTipologiaRequestAsync.");
    }

    private static MethodInfo GetValidateBusinessRulesMethod()
    {
        return typeof(TipologiasAdminFunction)
            .GetMethod("ValidateBusinessRules", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("No se encontró ValidateBusinessRules.");
    }
}
