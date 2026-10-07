namespace DocumentIA.Core.Models;

/// <summary>
/// Respuesta no satisfactoria de Document Intelligence Layout. Conserva el codigo HTTP para que
/// el resolutor de markdown pueda decir si el fallo es transitorio (429, 5xx) o no (AB#100880).
/// Hereda de InvalidOperationException para no romper a quien ya capturaba ese tipo.
/// </summary>
public class LayoutRequestException : InvalidOperationException
{
    /// <summary>Codigo HTTP devuelto por Document Intelligence.</summary>
    public int CodigoHttp { get; }

    public LayoutRequestException(int codigoHttp, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        CodigoHttp = codigoHttp;
    }
}
