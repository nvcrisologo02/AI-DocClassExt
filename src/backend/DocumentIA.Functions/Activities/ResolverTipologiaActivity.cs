using DocumentIA.Core.Configuration;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

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
        var resolved = _tipologiaVersionResolver.Resolve(tipologia);

        _logger.LogInformation(
            "Tipologia resuelta: entrada={TipologiaEntrada}, familia={TipologiaFamilia}, version={TipologiaVersion}, technicalKey={TipologiaTecnica}",
            tipologia,
            resolved.TipologiaId,
            resolved.Version,
            resolved.TechnicalKey);

        return resolved;
    }
}