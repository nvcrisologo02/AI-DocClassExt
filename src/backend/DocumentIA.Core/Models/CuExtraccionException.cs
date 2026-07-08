namespace DocumentIA.Core.Models;

/// <summary>
/// Fallo de extracción Azure Content Understanding que conserva el modelKey intentado,
/// para que el fallback GPT pueda registrar qué modelo CU falló (trazabilidad del balanceo).
/// </summary>
public class CuExtraccionException : Exception
{
    /// <summary>ModelKey de extracción CU que se estaba usando cuando se produjo el fallo.</summary>
    public string ModelKey { get; }

    /// <summary>Tipo de razón estable para FallbackRazon (ej. "TimeoutException", "CircuitRejected").</summary>
    public string RazonTipo { get; }

    public CuExtraccionException(string modelKey, string razonTipo, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        ModelKey = modelKey;
        RazonTipo = razonTipo;
    }
}
