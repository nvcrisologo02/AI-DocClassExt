using DocumentIA.Core.Configuration;
using DocumentIA.Data.Entities;

namespace DocumentIA.Functions.Tests;

/// <summary>
/// Shared test data factories for all test classes.
/// </summary>
public static class TestFixtures
{
    public static TipologiaEntity CreateValidTipologia(int id = 1)
    {
        return new TipologiaEntity
        {
            Id = id,
            Nombre = $"Test Tipologia {id}",
            Codigo = $"TST{id:000}",
            Estado = EstadoTipologia.Published,
            ConfiguracionJson = GetValidConfigJson(),
            FechaCreacion = DateTime.UtcNow,
            PromptGPT = null, // ✅ Deprecated field should be null
            Activa = true,
            UmbralClasificacion = 0.85,
            UmbralExtraccion = 0.80,
            Version = "1.0"
        };
    }

    public static TipologiaEntity CreateDraftTipologia(int id = 1)
    {
        return new TipologiaEntity
        {
            Id = id,
            Nombre = $"Draft Tipologia {id}",
            Codigo = $"DRF{id:000}",
            Estado = EstadoTipologia.Draft,
            ConfiguracionJson = GetValidConfigJson(),
            FechaCreacion = DateTime.UtcNow,
            PromptGPT = null,
            Activa = false,
            UmbralClasificacion = 0.85,
            UmbralExtraccion = 0.80,
            Version = "1.0"
        };
    }

    public static TipologiaEntity CreateRetiredTipologia(int id = 1)
    {
        return new TipologiaEntity
        {
            Id = id,
            Nombre = $"Retired Tipologia {id}",
            Codigo = $"RET{id:000}",
            Estado = EstadoTipologia.Retired,
            ConfiguracionJson = GetValidConfigJson(),
            FechaCreacion = DateTime.UtcNow,
            PromptGPT = null,
            Activa = false,
            UmbralClasificacion = 0.85,
            UmbralExtraccion = 0.80,
            Version = "1.0"
        };
    }

    public static string GetValidConfigJson()
    {
        return """
        {
            "fields": [
                {"name": "field1", "type": "string", "required": true},
                {"name": "field2", "type": "number"}
            ],
            "promptConfig": {
                "systemPrompt": "You are a document classifier",
                "userPromptTemplate": "Classify this document..."
            },
            "classification": {
                "tdn1": "TYPE_A",
                "tdn2": "SUBTYPE_B"
            }
        }
        """;
    }

    public static string GetMalformedConfigJson()
    {
        return """
        {
            "fields": [
                {"name": "field1", "type": "string
            ]
        """;
    }

    public static string GetMinimalConfigJson()
    {
        return """
        {
            "fields": [],
            "systemPrompt": "Minimal prompt",
            "userPromptTemplate": "Minimal template"
        }
        """;
    }
}
