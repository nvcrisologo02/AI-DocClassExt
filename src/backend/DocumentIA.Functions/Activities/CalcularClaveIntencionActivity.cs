using DocumentIA.Core.Models;
using DocumentIA.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DocumentIA.Functions.Activities;

/// <summary>
/// Calcula la clave de intencion de la peticion (AB#100930). Es una actividad y no codigo del
/// orquestador porque el registro de modelos lee base de datos y el orquestador debe seguir
/// siendo determinista. Se llama una vez por ejecucion, antes de verificar duplicados.
/// </summary>
public class CalcularClaveIntencionActivity
{
    private readonly ClaveIntencionResolver _resolver;
    private readonly ILogger<CalcularClaveIntencionActivity> _logger;

    public CalcularClaveIntencionActivity(ClaveIntencionResolver resolver, ILogger<CalcularClaveIntencionActivity> logger)
    {
        _resolver = resolver;
        _logger = logger;
    }

    [Function("CalcularClaveIntencionActivity")]
    public ClaveIntencion Run([ActivityTrigger] Instrucciones instrucciones)
    {
        var clave = _resolver.Resolver(instrucciones);

        _logger.LogInformation(
            "Clave de intencion {Clave}: modo={Modo} nivel={Nivel} universo={Universo} expectedType={ExpectedType} modelo={Modelo}",
            clave.Clave, clave.Componentes.Modo, clave.Componentes.Nivel, clave.Componentes.Universo,
            clave.Componentes.ExpectedType, clave.Componentes.ModeloClasificacion);

        return clave;
    }
}
