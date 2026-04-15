namespace DocumentIA.AssetResolver.Models;

/// <summary>
/// Configuración de aliases para mapear nombres de campos extraídos
/// a las columnas de búsqueda en DM_POSICION_AAII_TB.
/// Sección "FieldAliases" en appsettings.
/// </summary>
public class FieldAliasesConfig
{
    public List<string> Idufir { get; set; } = ["IDUFIR", "CRU", "CodigoRegistroUnico"];
    public List<string> ReferenciaCatastral { get; set; } = ["ReferenciaCatastral", "RefCatastral", "Catastral"];
}
