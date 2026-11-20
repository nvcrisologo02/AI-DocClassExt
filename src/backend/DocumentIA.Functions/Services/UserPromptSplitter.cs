namespace DocumentIA.Functions.Services;

/// <summary>
/// Parte una plantilla de mensaje de usuario en el tramo estable (todo lo anterior al primer
/// <c>{DOCUMENT_TEXT}</c>: instrucciones, contexto y catálogo) y el resto (documento y lo que va detrás),
/// para que la caché de prompt pueda poner un punto de corte al final del tramo estable (AB#101428).
/// </summary>
internal static class UserPromptSplitter
{
    /// <summary>Marcador de la plantilla donde va el texto del documento.</summary>
    public const string MarcadorDocumento = "{DOCUMENT_TEXT}";

    /// <summary>
    /// Corta la plantilla en el primer <see cref="MarcadorDocumento"/> y aplica las sustituciones, en el orden
    /// dado, a cada mitad por separado. La concatenación de ambas mitades es igual a aplicar la misma cadena de
    /// <c>Replace</c> a la plantilla entera, salvo si un valor sustituido contiene a su vez un marcador: entonces
    /// ese marcador ya no se sustituye en la otra mitad (caso patológico aceptado). Sin marcador, todo es tramo
    /// estable y el resto queda vacío.
    /// </summary>
    public static (string Estable, string Resto) Partir(
        string plantilla,
        IReadOnlyList<KeyValuePair<string, string>> sustituciones)
    {
        var corte = plantilla.IndexOf(MarcadorDocumento, StringComparison.Ordinal);
        if (corte < 0)
        {
            return (Sustituir(plantilla, sustituciones), string.Empty);
        }

        return (Sustituir(plantilla[..corte], sustituciones), Sustituir(plantilla[corte..], sustituciones));
    }

    private static string Sustituir(string texto, IReadOnlyList<KeyValuePair<string, string>> sustituciones)
    {
        foreach (var (marcador, valor) in sustituciones)
        {
            texto = texto.Replace(marcador, valor);
        }

        return texto;
    }
}
