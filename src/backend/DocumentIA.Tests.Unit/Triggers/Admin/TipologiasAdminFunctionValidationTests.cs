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

public class TipologiasAdminFunctionValidationTests
{
    [Fact]
    public void ValidateBusinessRules_WhenConfidenceConfigOutOfRange_ReturnsError()
    {
        var method = GetValidateBusinessRulesMethod();
        var config = new TipologiaValidationConfig
        {
            TipologiaId = "nota.simple",
            Version = "1.4",
            SkipGDCUpload = true,
            Extraction = new TipologiaExtractionConfig { Enabled = false },
            ConfidenceConfig = new ConfidenceConfig
            {
                ClasifUmbralFallback = 1.20
            }
        };

        var result = method.Invoke(null, new object?[] { config }) as string;

        result.Should().Be("Los umbrales y pesos de confidenceConfig deben estar entre 0 y 1.");
    }

    [Fact]
    public void ValidateBusinessRules_WhenFieldMappingTargetsUnknownField_ReturnsError()
    {
        var method = GetValidateBusinessRulesMethod();
        var config = new TipologiaValidationConfig
        {
            TipologiaId = "nota_simple",
            Version = "1.4",
            SkipGDCUpload = true,
            Extraction = new TipologiaExtractionConfig
            {
                Enabled = true,
                Provider = "ContentUnderstanding",
                ModelKey = "cu-nota-simple",
                FieldMappings = new List<ExtractionFieldMappingConfig>
                {
                    new() { TargetField = "CampoInexistente", SourcePath = "document.field" }
                }
            },
            Fields = new List<FieldValidationConfig>
            {
                new() { Name = "CampoExistente", Type = "string" }
            }
        };

        var result = method.Invoke(null, new object?[] { config }) as string;

        result.Should().Be("extraction.fieldMappings referencia un field inexistente: CampoInexistente.");
    }

    [Fact]
    public void ValidateBusinessRules_WhenGdcRequiredFieldsMissing_ReturnsError()
    {
        var method = GetValidateBusinessRulesMethod();
        var config = new TipologiaValidationConfig
        {
            TipologiaId = "nota.simple",
            Version = "1.4",
            SkipGDCUpload = false,
            Extraction = new TipologiaExtractionConfig { Enabled = false },
            GdcTipoDocumento = string.Empty,
            GdcSerie = string.Empty
        };

        var result = method.Invoke(null, new object?[] { config }) as string;

        result.Should().Be("gdcTipoDocumento es obligatorio cuando skipGDCUpload=false.");
    }

    [Fact]
    public async Task ValidateTipologiaRequestAsync_WhenExistingCodigoChanges_ReturnsImmutabilityError()
    {
        await using var dbContext = CreateDbContext();
        var function = CreateFunction(dbContext);
        var method = GetValidateTipologiaRequestAsyncMethod();

        var payload = CreatePayload("codigo-nuevo", "Nombre", "1.4");
        var config = new TipologiaValidationConfig
        {
            TipologiaId = "codigo-nuevo",
            TipologiaNombre = "Nombre",
            Version = "1.4",
            SkipGDCUpload = true,
            Extraction = new TipologiaExtractionConfig { Enabled = false }
        };
        var existing = new TipologiaEntity
        {
            Id = 12,
            Codigo = "codigo-original",
            Nombre = "Nombre",
            Version = "1.4"
        };

        var task = (Task<string?>)method.Invoke(function, new object?[] { payload, config, existing })!;
        var result = await task;

        result.Should().Be("Codigo no puede modificarse tras el primer guardado.");
    }

    [Theory]
    [InlineData("1.4")]
    [InlineData("1.4.0")]
    public async Task ValidateTipologiaRequestAsync_WhenVersionIsSupported_ReturnsNull(string version)
    {
        await using var dbContext = CreateDbContext();
        var function = CreateFunction(dbContext);
        var method = GetValidateTipologiaRequestAsyncMethod();

        dbContext.ModeloConfigs.Add(new ModeloConfigEntity
        {
            Key = "cu-nota-simple",
            Tipo = TipoModelo.Extraccion,
            Provider = "ContentUnderstanding",
            ConfiguracionJson = "{}",
            Activo = true
        });
        await dbContext.SaveChangesAsync();

        var payload = CreatePayload("nota_simple", "Nota simple", version);
        var config = new TipologiaValidationConfig
        {
            TipologiaId = "nota_simple",
            TipologiaNombre = "Nota simple",
            Version = version,
            SkipGDCUpload = true,
            Extraction = new TipologiaExtractionConfig
            {
                Enabled = true,
                Provider = "ContentUnderstanding",
                ModelKey = "cu-nota-simple"
            },
            Fields = new List<FieldValidationConfig>
            {
                new() { Name = "Titular", Type = "string" }
            }
        };

        var task = (Task<string?>)method.Invoke(function, new object?[] { payload, config, null })!;
        var result = await task;

        result.Should().BeNull();
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
            .UseInMemoryDatabase($"tipologias-validation-tests-{Guid.NewGuid()}")
            .Options;

        return new DocumentIADbContext(options);
    }

    private static MethodInfo GetValidateBusinessRulesMethod()
    {
        return typeof(TipologiasAdminFunction)
            .GetMethod("ValidateBusinessRules", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("No se encontró ValidateBusinessRules.");
    }

    private static MethodInfo GetValidateTipologiaRequestAsyncMethod()
    {
        return typeof(TipologiasAdminFunction)
            .GetMethod("ValidateTipologiaRequestAsync", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("No se encontró ValidateTipologiaRequestAsync.");
    }
}
