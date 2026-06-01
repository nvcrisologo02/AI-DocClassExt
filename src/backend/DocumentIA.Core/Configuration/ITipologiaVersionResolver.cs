namespace DocumentIA.Core.Configuration;

public interface ITipologiaVersionResolver
{
    ResolvedTipologia Resolve(string input);
    IReadOnlyCollection<string> GetVersions(string tipologiaId);
}

public sealed record ResolvedTipologia(
    string RequestedValue,
    string TipologiaId,
    string Version,
    string TechnicalKey,
    bool IsDefault,
    bool SkipGDCUpload = false,
    bool PromptEnabled = false,
    bool ExtractionEnabled = true,
    ConfidenceConfig? ConfidenceConfig = null,
    string ExtractionProvider = "",
    bool AssetResolverEnabled = false,
    List<string>? AssetResolverCamposSolicitados = null,
    string AssetResolverModoCombinacionCriterios = "OR",
    List<string>? AssetResolverMapeoIdufir = null,
    List<string>? AssetResolverMapeoReferenciaCatastral = null,
    bool AssetResolverBusquedaIdufirHabilitada = true,
    bool AssetResolverBusquedaReferenciaCatastralHabilitada = true,
    bool AssetResolverBusquedaDireccionHabilitada = false,
    List<string>? AssetResolverMapeoDireccionCompleta = null,
    List<string>? AssetResolverMapeoDireccionNombreVia = null,
    List<string>? AssetResolverMapeoDireccionNumero = null,
    List<string>? AssetResolverMapeoDireccionMunicipio = null,
    List<string>? AssetResolverMapeoDireccionCodigoPostal = null,
    double AssetResolverUmbralScoreDireccion = 0.75,
    // Tipology metadata for output contract enrichment
    string TipologiaNombre = "",
    string TipologiaMGDCMatricula = "",
    string GdcTipoDocumento = "",
    string GdcSubtipoDocumento = "",
    string GdcSerie = "",
    string Tdn1 = "",
    string Tdn2 = "",
    string GptDescripcion = "",
    bool PromptHasDefinition = false,
    int MaxPaginasDocumento = 0);