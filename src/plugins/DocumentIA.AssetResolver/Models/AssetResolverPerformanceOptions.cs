namespace DocumentIA.AssetResolver.Models;

public class AssetResolverPerformanceOptions
{
    // EF/SQL command timeout for all DB queries from this service.
    public int SqlCommandTimeoutSeconds { get; set; } = 15;

    // Upper bound for rows fetched in exact-match queries (IDUFIR/RefCatastral).
    public int MaxRowsPerExactSearch { get; set; } = 500;

    // Upper bound for candidate set in fuzzy address scoring.
    public int MaxCandidatesDireccion { get; set; } = 2000;

    // Upper bound for candidate set in typed-address filters.
    public int MaxCandidatesDireccionTipificada { get; set; } = 2000;
}