using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Xunit;
using DocumentIA.AssetResolver.Data;
using DocumentIA.AssetResolver.Data.Entities;
using DocumentIA.AssetResolver.Models;
using DocumentIA.AssetResolver.Services;
using DocumentIA.AssetResolver.Controllers;

namespace DocumentIA.AssetResolver.Tests;

public class AssetResolverServiceTests
{
    [Fact]
    public void Ping_ReturnsOk()
    {
        var controller = new PingController();
        var result = controller.Ping();

        Assert.IsType<OkObjectResult>(result);
    }

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
        Assert.Contains("IND_STATUS", campos.Keys, StringComparer.OrdinalIgnoreCase);
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

    [Fact]
    public async Task BuscarActivos_DireccionCompletaDesdeLocalizacion_ResuelveActivo()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateInMemoryDb(dbName, d =>
        {
            d.DmPosicionAAII.Add(new DmPosicionAAII
            {
                IdActivoSareb = 4m,
                FchCierreDt = new DateTime(2026, 2, 1),
                DesNombreVia = "CALLE MAYOR",
                NumVia = "1",
                DesMunicp = "MADRID",
                FchAlta = new DateTime(2020, 1, 1),
                DesServicer = "S4",
                FchCierre = new DateTime(2026, 2, 1)
            });
            d.SaveChanges();
        });

        var options = Options.Create(new FieldAliasesConfig());
        var service = new AssetResolverService(db, options, NullLogger<AssetResolverService>.Instance);

        var request = new AssetResolverController.GetAAIIInfoRequest
        {
            CorrelationId = "c4",
            ExtractedData = new Dictionary<string, string?>
            {
                ["Localizacion"] = "CALLE MAYOR 1, MADRID"
            },
            BusquedaDireccionHabilitada = true,
            MapeoDireccionCompleta = new List<string> { "Localizacion" }
        };

        var response = await service.BuscarActivosAsync(request);

        Assert.True(response.Found);
        Assert.Equal(1, response.Count);
        Assert.Contains("Direccion", response.CriterioUtilizado ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("CALLE MAYOR", response.CriteriosUsados?.Direccion?.NombreVia, ignoreCase: true);
        Assert.Equal("1", response.CriteriosUsados?.Direccion?.Numero, ignoreCase: true);
        Assert.Equal("MADRID", response.CriteriosUsados?.Direccion?.Municipio, ignoreCase: true);
    }

    [Fact]
    public async Task BuscarActivos_ModoAnd_ExigeCoincidenciaEnTodosLosCriteriosResueltos()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateInMemoryDb(dbName, d =>
        {
            d.DmPosicionAAII.AddRange(
                new DmPosicionAAII
                {
                    IdActivoSareb = 5m,
                    FchCierreDt = new DateTime(2026, 3, 1),
                    IdIdufir = "ID5",
                    DesNombreVia = "CALLE ALCALA",
                    NumVia = "10",
                    DesMunicp = "MADRID",
                    FchAlta = new DateTime(2020, 1, 1),
                    DesServicer = "S5",
                    FchCierre = new DateTime(2026, 3, 1)
                },
                new DmPosicionAAII
                {
                    IdActivoSareb = 6m,
                    FchCierreDt = new DateTime(2026, 3, 2),
                    IdIdufir = "ID6",
                    DesNombreVia = "CALLE MAYOR",
                    NumVia = "1",
                    DesMunicp = "MADRID",
                    FchAlta = new DateTime(2020, 1, 1),
                    DesServicer = "S6",
                    FchCierre = new DateTime(2026, 3, 2)
                });
            d.SaveChanges();
        });

        var options = Options.Create(new FieldAliasesConfig());
        var service = new AssetResolverService(db, options, NullLogger<AssetResolverService>.Instance);

        var request = new AssetResolverController.GetAAIIInfoRequest
        {
            CorrelationId = "c5",
            ExtractedData = new Dictionary<string, string?>
            {
                ["IDUFIR_CRU"] = "ID5",
                ["Localizacion"] = "CALLE MAYOR 1, MADRID"
            },
            MapeoIdufir = new List<string> { "IDUFIR_CRU" },
            BusquedaDireccionHabilitada = true,
            ModoCombinacionCriterios = "AND",
            MapeoDireccionCompleta = new List<string> { "Localizacion" }
        };

        var response = await service.BuscarActivosAsync(request);

        Assert.False(response.Found);
        Assert.Equal(0, response.Count);
        Assert.Equal("AND", response.CriteriosUsados?.ModoCombinacionCriterios);
    }

    [Fact]
    public async Task BuscarActivos_ConOrigenAacc_DevuelveResultadosSeparadosPorTipo()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateInMemoryDb(dbName, d =>
        {
            d.DmPosicionAAII.Add(new DmPosicionAAII
            {
                IdActivoSareb = 100m,
                FchCierreDt = new DateTime(2026, 1, 1),
                IdIdufir = "ID-MIX",
                DesServicer = "S-AAII",
                IndStatus = "A",
                FchCierre = new DateTime(2026, 1, 1)
            });

            d.DmPosicionAACC.Add(new DmPosicionAACC
            {
                IdActivoSareb = 200m,
                FchCierreDt = new DateTime(2026, 1, 1),
                IdIdufir = "ID-MIX",
                DesServicer = "S-AACC",
                IndStatus = "A",
                FchCierre = new DateTime(2026, 1, 1)
            });

            d.SaveChanges();
        });

        var options = Options.Create(new FieldAliasesConfig());
        var service = new AssetResolverService(db, options, NullLogger<AssetResolverService>.Instance);

        var request = new AssetResolverController.GetAAIIInfoRequest
        {
            CorrelationId = "c6",
            MapeoIdufir = new List<string> { "IDUFIR" },
            ExtractedData = new Dictionary<string, string?> { ["IDUFIR"] = "ID-MIX" },
            RequestedFields = new List<string> { "ID_ACTIVO_SAREB" },
            AAII_Search = true,
            AACC_Search = true
        };

        var response = await service.BuscarActivosAsync(request);

        Assert.True(response.Found);
        Assert.Equal(2, response.Count);
        Assert.Equal(1, response.CountAAII);
        Assert.Equal(1, response.CountAACC);
        Assert.Single(response.ActivosAAII);
        Assert.Single(response.ActivosAACC);
    }

    [Fact]
    public async Task BuscarActivos_OrigenSoloAacc_FiltraYNoConsultaAaii()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateInMemoryDb(dbName, d =>
        {
            d.DmPosicionAAII.Add(new DmPosicionAAII
            {
                IdActivoSareb = 300m,
                FchCierreDt = new DateTime(2026, 1, 1),
                IdIdufir = "ID-SOLO-AAII",
                DesServicer = "S-AAII",
                IndStatus = "A",
                FchCierre = new DateTime(2026, 1, 1)
            });

            d.DmPosicionAACC.Add(new DmPosicionAACC
            {
                IdActivoSareb = 400m,
                FchCierreDt = new DateTime(2026, 1, 1),
                IdIdufir = "ID-SOLO-AACC",
                DesServicer = "S-AACC",
                IndStatus = "A",
                FchCierre = new DateTime(2026, 1, 1)
            });

            d.SaveChanges();
        });

        var options = Options.Create(new FieldAliasesConfig());
        var service = new AssetResolverService(db, options, NullLogger<AssetResolverService>.Instance);

        var request = new AssetResolverController.GetAAIIInfoRequest
        {
            CorrelationId = "c7",
            MapeoIdufir = new List<string> { "IDUFIR" },
            ExtractedData = new Dictionary<string, string?> { ["IDUFIR"] = "ID-SOLO-AACC" },
            RequestedFields = new List<string> { "ID_ACTIVO_SAREB" },
            AAII_Search = false,
            AACC_Search = true
        };

        var response = await service.BuscarActivosAsync(request);

        Assert.True(response.Found);
        Assert.Equal(1, response.Count);
        Assert.Equal(0, response.CountAAII);
        Assert.Equal(1, response.CountAACC);
        Assert.Empty(response.ActivosAAII);
        Assert.Single(response.ActivosAACC);
    }
}
