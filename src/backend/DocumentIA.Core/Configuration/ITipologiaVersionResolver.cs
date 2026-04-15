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
    List<string>? AssetResolverMapeoIdufir = null,
    List<string>? AssetResolverMapeoReferenciaCatastral = null);