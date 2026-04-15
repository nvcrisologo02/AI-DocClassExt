using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using DocumentIA.AssetResolver.Data;
using DocumentIA.AssetResolver.Data.Entities;
using DocumentIA.AssetResolver.Models;
using DocumentIA.AssetResolver.Services;
using DocumentIA.AssetResolver.Controllers;

namespace DocumentIA.AssetResolver.Tests;

public class AssetResolverServiceTests
{
    private static AssetResolverDbContext CreateInMemoryDb(string dbName, Action<AssetResolverDbContext>? seed = null)
    {
        var options = new DbContextOptionsBuilder<AssetResolverDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var db = new AssetResolverDbContext(options);
        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();
        seed?.Invoke(db);
        return db;
    }

    [Fact]
    public async Task BuscarActivos_BothEmpty_UsesAliasesAndReturnsObligatoryFields()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateInMemoryDb(dbName, d =>
        {
            d.DmPosicionAAII.Add(new DmPosicionAAII
            {
                IdActivoSareb = 1m,
                FchCierreDt = new DateTime(2026, 1, 1),
                IdIdufir = "ID1",
                IdRefCatast = "REF1",
                FchAlta = new DateTime(2020, 1, 1),
                FchBaja = new DateTime(2021, 1, 1),
                DesServicer = "ServicerX",
                FchCierre = new DateTime(2026, 1, 1)
            });
            d.SaveChanges();
        });

        var aliases = new FieldAliasesConfig
        {
            Idufir = new List<string> { "MI_IDUFIR_ALIAS" },
            ReferenciaCatastral = new List<string> { "MI_REFCAT_ALIAS" }
        };
        var options = Options.Create(aliases);
        var service = new AssetResolverService(db, options, NullLogger<AssetResolverService>.Instance);

        var request = new AssetResolverController.GetAAIIInfoRequest
        {
            CorrelationId = "c1",
            ExtractedData = new Dictionary<string, string?> { ["MI_IDUFIR_ALIAS"] = "ID1" },
            RequestedFields = new List<string> { "ID_ACTIVO_SAREB" }
        };

        var response = await service.BuscarActivosAsync(request);

        Assert.True(response.Found);
        Assert.Equal(1, response.Count);
        var campos = response.Activos[0].CamposSolicitados;
        Assert.Contains("FCH_ALTA", campos.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("FCH_BAJA", campos.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("DES_SERVICER", campos.Keys, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BuscarActivos_Indicated_MapeoIdufir_SearchOnlyByIdufir()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateInMemoryDb(dbName, d =>
        {
            d.DmPosicionAAII.Add(new DmPosicionAAII
            {
                IdActivoSareb = 2m,
                FchCierreDt = DateTime.UtcNow,
                IdIdufir = "ID2",
                IdRefCatast = "REFX",
                FchAlta = DateTime.UtcNow.AddYears(-1),
                FchBaja = null,
                DesServicer = "S2",
                FchCierre = DateTime.UtcNow
            });
            d.SaveChanges();
        });

        var aliases = new FieldAliasesConfig
        {
            Idufir = new List<string> { "IGNORED" },
            ReferenciaCatastral = new List<string> { "IGNORED2" }
        };
        var options = Options.Create(aliases);
        var service = new AssetResolverService(db, options, NullLogger<AssetResolverService>.Instance);

        var request = new AssetResolverController.GetAAIIInfoRequest
        {
            CorrelationId = "c2",
            ExtractedData = new Dictionary<string, string?> { ["SOME_FIELD"] = "ID2", ["SOME_REFCAT"] = "REFX" },
            MapeoIdufir = new List<string> { "SOME_FIELD" },
            RequestedFields = new List<string> { "ID_ACTIVO_SAREB" }
        };

        var response = await service.BuscarActivosAsync(request);

        Assert.True(response.Found);
        Assert.Equal(1, response.Count);
        Assert.Equal("ID2", response.CriteriosUsados?.Idufir, ignoreCase: true);
        Assert.Null(response.CriteriosUsados?.ReferenciaCatastral);
    }

    [Fact]
    public async Task BuscarActivos_RequestLimitedFields_StillIncludesObligatory()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateInMemoryDb(dbName, d =>
        {
            d.DmPosicionAAII.Add(new DmPosicionAAII
            {
                IdActivoSareb = 3m,
                FchCierreDt = new DateTime(2025, 5, 1),
                IdIdufir = "ID3",
                FchAlta = new DateTime(2020, 5, 1),
                FchBaja = null,
                DesServicer = "S3",
                FchCierre = new DateTime(2025, 5, 1)
            });
            d.SaveChanges();
        });

        var aliases = new FieldAliasesConfig
        {
            Idufir = new List<string> { "IDUFIR" },
            ReferenciaCatastral = new List<string> { "REFCAT" }
        };
        var options = Options.Create(aliases);
        var service = new AssetResolverService(db, options, NullLogger<AssetResolverService>.Instance);

        var request = new AssetResolverController.GetAAIIInfoRequest
        {
            CorrelationId = "c3",
            ExtractedData = new Dictionary<string, string?> { ["IDUFIR"] = "ID3" },
            RequestedFields = new List<string> { "ID_ACTIVO_SAREB" }
        };

        var response = await service.BuscarActivosAsync(request);

        Assert.True(response.Found);
        Assert.Equal(1, response.Count);
        var campos = response.Activos[0].CamposSolicitados;
        Assert.Contains("FCH_ALTA", campos.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("FCH_BAJA", campos.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("DES_SERVICER", campos.Keys, StringComparer.OrdinalIgnoreCase);
    }
}
