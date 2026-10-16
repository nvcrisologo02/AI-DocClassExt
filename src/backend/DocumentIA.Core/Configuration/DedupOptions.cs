namespace DocumentIA.Core.Configuration;

/// <summary>
/// Reutilizacion por duplicado (AB#100930). Seccion "Dedup"; en Azure, app settings
/// Dedup__ModoEstricto, Dedup__MaxCandidatas, Dedup__MaxDerivacionAlVuelo.
/// </summary>
public sealed class DedupOptions
{
    public const string Seccion = "Dedup";

    /// <summary>
    /// True (defecto): la historica se selecciona por clave de intencion y, sin historica
    /// compatible, el documento se procesa. False: regla anterior (modo y nivel con fallback a
    /// la ultima con contrato) y DUPLICADO vacio sin historica. La clave se calcula y persiste
    /// en ambos modos. Se retira en la release siguiente a la validacion en PRO.
    /// </summary>
    public bool ModoEstricto { get; set; } = true;

    /// <summary>Candidatas con la misma clave que se examinan antes de rendirse.</summary>
    public int MaxCandidatas { get; set; } = 5;

    /// <summary>Filas antiguas sin clave que se derivan al vuelo por documento y peticion.</summary>
    public int MaxDerivacionAlVuelo { get; set; } = 50;
}
