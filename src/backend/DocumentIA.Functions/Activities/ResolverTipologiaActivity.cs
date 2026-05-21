using DocumentIA.Core.Configuration;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace DocumentIA.Functions.Activities;

public class ResolverTipologiaActivity
{
    private readonly ITipologiaVersionResolver _tipologiaVersionResolver;
    private readonly ILogger<ResolverTipologiaActivity> _logger;

    public ResolverTipologiaActivity(
        ITipologiaVersionResolver tipologiaVersionResolver,
        ILogger<ResolverTipologiaActivity> logger)
    {
        _tipologiaVersionResolver = tipologiaVersionResolver;
        _logger = logger;
    }

    [Function("ResolverTipologiaActivity")]
    public ResolvedTipologia Run([ActivityTrigger] string tipologia)
    {
        ResolvedTipologia resolved;

        try
        {
            resolved = _tipologiaVersionResolver.Resolve(tipologia);
        }
        catch (KeyNotFoundException)
        {
            // Cuando la clasificación no encuentra tipología válida,
            // degradamos a "Desconocido" para no romper la orquestación.
            resolved = new ResolvedTipologia(
                RequestedValue: tipologia,
                TipologiaId: "Desconocido",
                Version: "N/A",
                TechnicalKey: "Desconocido",
                IsDefault: true,
                SkipGDCUpload: true,
                PromptEnabled: false,
                ExtractionEnabled: false,
                ConfidenceConfig: null,
                ExtractionProvider: string.Empty,
                AssetResolverEnabled: false);

            _logger.LogWarning(
                "Tipología no resoluble ({TipologiaEntrada}). Se usa fallback a Desconocido.",
                tipologia);
        }

        _logger.LogInformation(
            "Tipologia resuelta: entrada={TipologiaEntrada}, familia={TipologiaFamilia}, version={TipologiaVersion}, technicalKey={TipologiaTecnica}",
            tipologia,
            resolved.TipologiaId,
            resolved.Version,
            resolved.TechnicalKey);

        return resolved;
    }
}