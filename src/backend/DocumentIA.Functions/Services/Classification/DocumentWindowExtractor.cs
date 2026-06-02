using System;
using System.Collections.Generic;
using System.Linq;
using DocumentIA.Core.Models;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;

namespace DocumentIA.Functions.Services.Classification
{
    /// <summary>
    /// Extrae ventanas de páginas/texto del documento para la clasificación jerárquica.
    /// Soporta: límite de páginas configurables, normalización de texto, y desduplicación.
    /// </summary>
    public class DocumentWindowExtractor
    {
        private readonly ILogger<DocumentWindowExtractor> _logger;

        public DocumentWindowExtractor(ILogger<DocumentWindowExtractor> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Extrae una ventana de contexto desde DatosNormalizados para clasificación.
        /// Utiliza los primeros N caracteres del Markdown/Texto o devuelve texto disponible.
        /// </summary>
        public DocumentClassificationWindow ExtractWindow(
            ClasificacionInput input,
            int maxCharacters = 32000,
            int pagesToInspect = 3)
        {
            var window = new DocumentClassificationWindow
            {
                DocumentName = input.Entrada.Documento.Name,
                PagesToInspect = pagesToInspect,
                ExtractedText = ExtractTextContent(input.DatosNormalizados, maxCharacters)
                    ?? ExtractPdfTextContent(input.Entrada.Documento.Content.Base64, maxCharacters, pagesToInspect),
                Tipologia = input.Entrada.Instrucciones?.Classification?.Model ?? "unknown",
                TotalPaginas = input.TotalPaginas,
                CharsTextoNativo = input.CharsTextoNativo,
                Timestamp = DateTime.UtcNow
            };

            _logger.LogInformation(
                "Ventana extraída para {Documento}: {TextLength} chars, {PagesToInspect} páginas",
                window.DocumentName,
                window.ExtractedText?.Length ?? 0,
                window.PagesToInspect);

            return window;
        }

        private string? ExtractTextContent(Dictionary<string, object> datosNormalizados, int maxChars)
        {
            var claves = new[] { "Markdown", "markdown", "Texto", "texto", "ContentText", "contentText" };

            foreach (var clave in claves)
            {
                if (!datosNormalizados.TryGetValue(clave, out var raw) || raw is null)
                    continue;

                string? texto = null;
                if (raw is string s && !string.IsNullOrWhiteSpace(s))
                {
                    texto = s;
                }
                else if (raw is System.Text.Json.JsonElement json && json.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    var value = json.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        texto = value;
                    }
                }

                if (!string.IsNullOrWhiteSpace(texto))
                {
                    return texto.Length > maxChars ? texto.Substring(0, maxChars) : texto;
                }
            }

            return null;
        }

        private string? ExtractPdfTextContent(string? documentoBase64, int maxChars, int pagesToInspect)
        {
            if (string.IsNullOrWhiteSpace(documentoBase64))
            {
                return null;
            }

            try
            {
                var pdfBytes = Convert.FromBase64String(documentoBase64);
                using var document = PdfDocument.Open(pdfBytes);

                var pages = Math.Max(1, pagesToInspect);
                var text = string.Concat(
                    Enumerable.Range(1, Math.Min(document.NumberOfPages, pages))
                        .Select(pageNumber => document.GetPage(pageNumber).Text ?? string.Empty));

                if (string.IsNullOrWhiteSpace(text))
                {
                    return null;
                }

                return text.Length > maxChars ? text.Substring(0, maxChars) : text;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "No se pudo extraer texto nativo del PDF para clasificación.");
                return null;
            }
        }
    }

    /// <summary>
    /// Representa la ventana de contexto extraída para clasificación.
    /// </summary>
    public class DocumentClassificationWindow
    {
        public string? DocumentName { get; set; }
        public string? ExtractedText { get; set; }
        public string? Tipologia { get; set; }
        public int TotalPaginas { get; set; }
        public int CharsTextoNativo { get; set; }
        public int PagesToInspect { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
