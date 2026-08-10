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

    public PdfRecorteResultado RecortarParaClasificacion(string documentoBase64, int maxPaginas)
    {
        var normalizedMaxPaginas = Math.Max(1, maxPaginas);

        if (string.IsNullOrWhiteSpace(documentoBase64))
        {
            _logger.LogWarning(
                "PDF recorte omitido para clasificación: documentoBase64 vacío o nulo. MaxPaginas={MaxPaginas}",
                normalizedMaxPaginas);

            return new PdfRecorteResultado
            {
                DocumentoBase64Recortado = string.Empty,
                TotalPaginas = 0,
                CharsTextoNativo = 0,
                PaginasIncluidas = 0,
                RecorteAplicado = false
            };
        }

        var pdfBytes = Convert.FromBase64String(documentoBase64);

        // Documentos no-PDF (XLSX/PPTX/DOCX empiezan por "PK", imagenes por sus propios magic
        // bytes): el recorte por paginas solo aplica a PDF. Se devuelve el documento completo tal
        // cual, en lugar de dejar que PdfPig lance y el llamador pierda el contenido (AB#100045).
        if (!EsPdf(pdfBytes))
        {
            _logger.LogInformation(
                "Recorte omitido: el documento no es PDF (cabecera no coincide con %PDF). Se devuelve el documento completo sin recortar.");

            return new PdfRecorteResultado
            {
                DocumentoBase64Recortado = documentoBase64,
                TotalPaginas = 0,
                CharsTextoNativo = 0,
                PaginasIncluidas = 0,
                RecorteAplicado = false
            };
        }

        int totalPaginas;
        int charsTextoNativo = 0;

        using (var document = PdfDocument.Open(pdfBytes))
        {
            totalPaginas = document.NumberOfPages;
            var paginasInspeccionar = Math.Min(totalPaginas, normalizedMaxPaginas + 2);

            for (var pageNumber = 1; pageNumber <= paginasInspeccionar; pageNumber++)
            {
                charsTextoNativo += document.GetPage(pageNumber).GetWords().Sum(w => w.Text.Length);
            }
        }

        if (totalPaginas <= normalizedMaxPaginas)
        {
            _logger.LogInformation(
                "PDF sin recorte para clasificación: {TotalPaginas} páginas <= {MaxPaginas}",
                totalPaginas,
                normalizedMaxPaginas);

            return new PdfRecorteResultado
            {
                DocumentoBase64Recortado = documentoBase64,
                TotalPaginas = totalPaginas,
                CharsTextoNativo = charsTextoNativo,
                PaginasIncluidas = totalPaginas,
                RecorteAplicado = false
            };
        }

        byte[] pdfRecortado;
        using (var document = PdfDocument.Open(pdfBytes))
        {
            var builder = new PdfDocumentBuilder();
            for (var pageNumber = 1; pageNumber <= normalizedMaxPaginas; pageNumber++)
            {
                builder.AddPage(document, pageNumber);
            }

            pdfRecortado = builder.Build();
        }

        _logger.LogInformation(
            "PDF recortado para clasificación: {TotalPaginas} -> {PaginasRecortadas} páginas | {BytesOriginal} -> {BytesRecortado} bytes | charsTextoNativo={CharsTextoNativo}",
            totalPaginas,
            normalizedMaxPaginas,
            pdfBytes.Length,
            pdfRecortado.Length,
            charsTextoNativo);

        return new PdfRecorteResultado
        {
            DocumentoBase64Recortado = Convert.ToBase64String(pdfRecortado),
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
    public string DocumentoBase64Recortado { get; set; } = string.Empty;
    public int TotalPaginas { get; set; }
    public int CharsTextoNativo { get; set; }
    public int PaginasIncluidas { get; set; }
    public bool RecorteAplicado { get; set; }
}
