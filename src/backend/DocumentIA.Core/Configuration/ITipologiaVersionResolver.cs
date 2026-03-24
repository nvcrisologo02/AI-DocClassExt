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
    bool SkipGDCUpload = false);