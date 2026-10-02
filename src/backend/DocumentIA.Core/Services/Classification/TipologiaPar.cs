namespace DocumentIA.Core.Services.Classification;

/// <summary>Tipologia publicada con su par TDN1/TDN2 (AB#100779).</summary>
public sealed record TipologiaPar(string Codigo, string Tdn1, string Tdn2)
{
    /// <summary>Clave del par sin distinguir mayusculas: "TDN1|TDN2".</summary>
    public static string Clave(string? tdn1, string? tdn2) =>
        $"{(tdn1 ?? string.Empty).Trim().ToUpperInvariant()}|{(tdn2 ?? string.Empty).Trim().ToUpperInvariant()}";

    public string ClavePar => Clave(Tdn1, Tdn2);
}
