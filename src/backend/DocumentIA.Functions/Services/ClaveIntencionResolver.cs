using DocumentIA.Core.Configuration;
using DocumentIA.Core.Models;
using DocumentIA.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocumentIA.Functions.Services;

/// <summary>
/// Construye la clave de intencion de una peticion (AB#100930). Resuelve el modelo de
/// clasificacion que el registro daria ahora para Classification.Provider y Model, con la
/// misma regla que los proveedores: modelo explicito por clave; flujo DI puro por el DI por
/// defecto; cualquier otro flujo (gpt, hibrido, auto) por el GPT de fallback, que es el que
/// acaba grabando Clasificacion.Modelo. Con ExpectedType la clave no depende del registro.
/// </summary>
public sealed class ClaveIntencionResolver
{
    public const string ModeloNoResuelto = "sin-modelo";

    private readonly ClassificationRoutingSettings _routing;
    private readonly ClassificationModelRegistryLoader _registro;
    private readonly ILogger<ClaveIntencionResolver> _logger;

    public ClaveIntencionResolver(
        IOptions<ClassificationRoutingSettings> routing,
        ClassificationModelRegistryLoader registro,
        ILogger<ClaveIntencionResolver> logger)
    {
        _routing = routing.Value;
        _registro = registro;
        _logger = logger;
    }

    public ClaveIntencion Resolver(Instrucciones instrucciones)
    {
        ArgumentNullException.ThrowIfNull(instrucciones);

        var expectedType = ClaveIntencionBuilder.NormalizarExpectedType(instrucciones.ExpectedType);
        var modelo = expectedType.Length > 0
            ? ClaveIntencionBuilder.ModeloExpectedType
            : ResolverModeloClasificacion(instrucciones.Classification);

        var componentes = ClaveIntencionBuilder.Normalizar(
            classificationOnly: instrucciones.ClassificationOnly,
            nivelClasificacion: instrucciones.Classification.NivelClasificacion,
            nivelDefault: _routing.NivelClasificacionDefault,
            codigosRestriccion: instrucciones.RestriccionTipologias?.Codigos,
            expectedType: instrucciones.ExpectedType,
            modeloClasificacion: modelo);

        return ClaveIntencionBuilder.Construir(componentes);
    }

    internal string ResolverModeloClasificacion(ConfiguracionIA classification)
    {
        try
        {
            var modelKey = classification.Model;
            if (!string.IsNullOrWhiteSpace(modelKey)
                && !string.Equals(modelKey, "auto", StringComparison.OrdinalIgnoreCase))
            {
                return Identificador(_registro.GetModel(modelKey));
            }

            var flujo = ConfigurableClasificarDataProvider.ResolveFlowName(classification.Provider, _routing);
            var proveedores = ConfigurableClasificarDataProvider.ResolveFlowProviders(flujo, _routing);
            var primero = proveedores.FirstOrDefault() ?? flujo;

            if (EsDocumentIntelligence(primero))
            {
                return Identificador(_registro.GetDefaultModel("azure-document-intelligence"));
            }

            return Identificador(_registro.GetFallbackModel());
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException or InvalidDataException)
        {
            _logger.LogWarning(ex,
                "No se pudo resolver el modelo de clasificacion para la clave de intencion (Provider={Provider}, Model={Model}); se usa el marcador {Marcador}",
                classification.Provider, classification.Model, ModeloNoResuelto);
            return ModeloNoResuelto;
        }
    }

    private static string Identificador(ClassificationModelConfig modelo)
        => !string.IsNullOrWhiteSpace(modelo.DeploymentName) ? modelo.DeploymentName : modelo.ClassifierId;

    private static bool EsDocumentIntelligence(string proveedor)
        => proveedor.ToLowerInvariant() is "azure-document-intelligence" or "azure-di" or "di";
}
