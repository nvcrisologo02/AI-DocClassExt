using UglyToad.PdfPig;
using UglyToad.PdfPig.Writer;
using Microsoft.Extensions.Logging;

namespace DocumentIA.Functions.Services;

public class PdfRecorteService
{
    private readonly ILogger<PdfRecorteService> _logger;

    public PdfRecorteService(ILogger<PdfRecorteService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Recorta un PDF a sus primeras <paramref name="maxPaginas"/> páginas para la clasificación.
    /// Trabaja con bytes de entrada y salida y abre el documento una sola vez (AB#100814).
    /// Si no hay recorte (no es PDF, está vacío o no excede el máximo) devuelve PdfRecortado null.
    /// </summary>
    public PdfRecorteResultado RecortarParaClasificacion(byte[] documento, int maxPaginas)
    {
        var normalizedMaxPaginas = Math.Max(1, maxPaginas);

        if (documento is null || documento.Length == 0)
        {
            _logger.LogWarning(
                "PDF recorte omitido para clasificación: documento vacío. MaxPaginas={MaxPaginas}",
                normalizedMaxPaginas);

            return PdfRecorteResultado.SinRecorte(totalPaginas: 0, charsTextoNativo: 0, paginasIncluidas: 0);
        }

        // Documentos no-PDF (XLSX/PPTX/DOCX empiezan por "PK", imagenes por sus propios magic
        // bytes): el recorte por paginas solo aplica a PDF. Se devuelve "sin recorte" en lugar de
        // dejar que PdfPig lance y el llamador pierda el documento (AB#100045).
        if (!EsPdf(documento))
        {
            _logger.LogInformation(
                "Recorte omitido: el documento no es PDF (cabecera no coincide con %PDF). Se usará el documento completo sin recortar.");

            return PdfRecorteResultado.SinRecorte(totalPaginas: 0, charsTextoNativo: 0, paginasIncluidas: 0);
        }

        using var pdf = PdfDocument.Open(documento);

        var totalPaginas = pdf.NumberOfPages;
        var paginasInspeccionar = Math.Min(totalPaginas, normalizedMaxPaginas + 2);
        var charsTextoNativo = 0;

        for (var pageNumber = 1; pageNumber <= paginasInspeccionar; pageNumber++)
        {
            charsTextoNativo += pdf.GetPage(pageNumber).GetWords().Sum(w => w.Text.Length);
        }

        if (totalPaginas <= normalizedMaxPaginas)
        {
            _logger.LogInformation(
                "PDF sin recorte para clasificación: {TotalPaginas} páginas <= {MaxPaginas}",
                totalPaginas,
                normalizedMaxPaginas);

            return PdfRecorteResultado.SinRecorte(totalPaginas, charsTextoNativo, paginasIncluidas: totalPaginas);
        }

        var builder = new PdfDocumentBuilder();
        for (var pageNumber = 1; pageNumber <= normalizedMaxPaginas; pageNumber++)
        {
            builder.AddPage(pdf, pageNumber);
        }

        var pdfRecortado = builder.Build();

        _logger.LogInformation(
            "PDF recortado para clasificación: {TotalPaginas} -> {PaginasRecortadas} páginas | {BytesOriginal} -> {BytesRecortado} bytes | charsTextoNativo={CharsTextoNativo}",
            totalPaginas,
            normalizedMaxPaginas,
            documento.Length,
            pdfRecortado.Length,
            charsTextoNativo);

        return new PdfRecorteResultado
        {
            PdfRecortado = pdfRecortado,
            TotalPaginas = totalPaginas,
            CharsTextoNativo = charsTextoNativo,
            PaginasIncluidas = normalizedMaxPaginas,
            RecorteAplicado = true
        };
    }

    /// <summary>
    /// Un PDF empieza por "%PDF" (0x25 0x50 0x44 0x46), opcionalmente precedido de BOM/espacios
    /// que PdfPig tolera buscando la cabecera en los primeros bytes. Se inspecciona esa misma
    /// ventana inicial en lugar de exigir la posición 0.
    /// </summary>
    private static bool EsPdf(byte[] bytes)
    {
        ReadOnlySpan<byte> cabecera = "%PDF"u8;
        var limite = Math.Min(bytes.Length, 1024) - cabecera.Length;

        for (var i = 0; i <= limite; i++)
        {
            if (bytes.AsSpan(i, cabecera.Length).SequenceEqual(cabecera))
            {
                return true;
            }
        }

        return false;
    }
}

public class PdfRecorteResultado
{
    /// <summary>Bytes del PDF recortado. Null cuando no se aplicó recorte.</summary>
    public byte[]? PdfRecortado { get; set; }
    public int TotalPaginas { get; set; }
    public int CharsTextoNativo { get; set; }
    public int PaginasIncluidas { get; set; }
    public bool RecorteAplicado { get; set; }

    public static PdfRecorteResultado SinRecorte(int totalPaginas, int charsTextoNativo, int paginasIncluidas) => new()
    {
        PdfRecortado = null,
        TotalPaginas = totalPaginas,
        CharsTextoNativo = charsTextoNativo,
        PaginasIncluidas = paginasIncluidas,
        RecorteAplicado = false
    };
}
