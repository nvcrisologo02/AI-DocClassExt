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

    // Aliases por defecto para búsqueda por dirección como criterio adicional
    public List<string> DireccionCompleta { get; set; } = ["Localizacion", "Direccion", "DireccionCompleta", "Domicilio"];
    public List<string> DireccionNombreVia { get; set; } = ["NombreVia", "Via", "Calle", "Direccion", "DireccionVia"];
    public List<string> DireccionNumero { get; set; } = ["Numero", "NumeroVia", "NumVia", "NumeroCalle"];
    public List<string> DireccionMunicipio { get; set; } = ["Municipio", "Localidad", "Poblacion", "Ciudad"];
    public List<string> DireccionCodigoPostal { get; set; } = ["CodigoPostal", "CP", "CodPostal", "CodPostal"];

}
