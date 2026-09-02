using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace DocumentIA.Core.Services;

/// <summary>
/// Compresion/descompresion de markdown para persistencia compacta en BD
/// (columna Documentos.NormalizacionMarkdownCompressed).
/// Algoritmo: UTF8 -> GZip (CompressionLevel.Optimal) -> Base64.
/// </summary>
public static class MarkdownCompression
{
    /// <summary>
    /// Comprime un texto a GZip(UTF8(texto)) sin Base64. Devuelve null si el valor es nulo o vacio.
    /// Es la forma de persistencia actual (columna varbinary, AB#100169): la variante Base64 se
    /// conserva porque se sigue escribiendo en paralelo mientras la vuelta atras deba ser posible,
    /// y porque es la unica forma del historico sin migrar.
    /// </summary>
    public static byte[]? Compress(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var rawBytes = Encoding.UTF8.GetBytes(value);
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            gzip.Write(rawBytes, 0, rawBytes.Length);
        }

        return output.ToArray();
    }

    /// <summary>
    /// Descomprime un valor generado por <see cref="Compress"/>. Tolerante a errores: devuelve
    /// null si el contenido es nulo, vacio o no es GZip valido, sin lanzar excepciones.
    /// </summary>
    public static string? Decompress(byte[]? value)
    {
        if (value is null || value.Length == 0)
        {
            return null;
        }

        try
        {
            using var input = new MemoryStream(value);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            gzip.CopyTo(output);
            return Encoding.UTF8.GetString(output.ToArray());
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Comprime un texto a Base64(GZip(UTF8(texto))). Devuelve null si el valor es nulo o vacio.
    /// </summary>
    public static string? CompressToBase64(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var rawBytes = Encoding.UTF8.GetBytes(value);
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            gzip.Write(rawBytes, 0, rawBytes.Length);
        }

        output.Position = 0;
        return Convert.ToBase64String(output.ToArray());
    }

    /// <summary>
    /// Descomprime un valor generado por <see cref="CompressToBase64"/>. Tolerante a errores:
    /// devuelve null si el valor es nulo/vacio o si el contenido Base64/GZip no es valido,
    /// sin lanzar excepciones.
    /// </summary>
    public static string? DecompressFromBase64(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            var compressedBytes = Convert.FromBase64String(value);
            using var input = new MemoryStream(compressedBytes);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            gzip.CopyTo(output);
            return Encoding.UTF8.GetString(output.ToArray());
        }
        catch (Exception)
        {
            return null;
        }
    }
}
