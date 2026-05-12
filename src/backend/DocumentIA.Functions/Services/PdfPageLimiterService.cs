using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using System.IO;

namespace DocumentIA.Functions.Services;

public class PdfPageLimiterService
{
    public PdfPageLimitResult LimitForClassificationOnly(string documentoBase64, int maxPages)
    {
        if (string.IsNullOrWhiteSpace(documentoBase64))
        {
            throw new ArgumentException("Document base64 cannot be null or empty.", nameof(documentoBase64));
        }

        var pdfBytes = Convert.FromBase64String(documentoBase64);
        using var inputStream = new MemoryStream(pdfBytes, writable: false);
        using var source = PdfReader.Open(inputStream, PdfDocumentOpenMode.Import);
        var originalPages = source.PageCount;

        if (maxPages <= 0 || originalPages <= maxPages)
        {
            return new PdfPageLimitResult
            {
                Base64 = documentoBase64,
                OriginalPages = originalPages,
                UsedPages = originalPages,
                Applied = false
            };
        }

        using var output = new PdfDocument();
        for (var i = 0; i < maxPages; i++)
        {
            output.AddPage(source.Pages[i]);
        }

        using var outputStream = new MemoryStream();
        output.Save(outputStream, false);

        return new PdfPageLimitResult
        {
            Base64 = Convert.ToBase64String(outputStream.ToArray()),
            OriginalPages = originalPages,
            UsedPages = maxPages,
            Applied = true
        };
    }
}

public class PdfPageLimitResult
{
    public string Base64 { get; set; } = string.Empty;
    public int OriginalPages { get; set; }
    public int UsedPages { get; set; }
    public bool Applied { get; set; }
}
