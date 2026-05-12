using DocumentIA.Functions.Services;
using Microsoft.Azure.Functions.Worker;

namespace DocumentIA.Functions.Activities;

public class ApplyPdfPageLimitActivity
{
    private readonly PdfPageLimiterService _pdfPageLimiterService;

    public ApplyPdfPageLimitActivity(PdfPageLimiterService pdfPageLimiterService)
    {
        _pdfPageLimiterService = pdfPageLimiterService;
    }

    [Function("ApplyPdfPageLimitActivity")]
    public PdfPageLimitResult Run([ActivityTrigger] ApplyPdfPageLimitInput input)
    {
        return _pdfPageLimiterService.LimitForClassificationOnly(input.DocumentoBase64, input.MaxPages);
    }
}

public class ApplyPdfPageLimitInput
{
    public string DocumentoBase64 { get; set; } = string.Empty;
    public int MaxPages { get; set; }
}
