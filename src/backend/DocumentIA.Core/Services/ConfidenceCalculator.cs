using DocumentIA.Core.Configuration;
using DocumentIA.Core.Models;

namespace DocumentIA.Core.Services;

/// <summary>
/// Calculadora estática de confianza para clasificación, extracción y validación.
/// No tiene dependencias de DI; todos los métodos son puros y testeables directamente.
/// </summary>
public static class ConfidenceCalculator
{
    private static readonly ConfidenceConfig DefaultConfig = new();

    /// <summary>
    /// Devuelve la confianza final de clasificación:
    /// si se activó fallback GPT, usa la confianza GPT; de lo contrario usa la DI.
    /// </summary>
    public static double ClasifFinal(double? diConf, double? gptConf, bool fallbackUsado)
    {
        if (fallbackUsado)
            return Math.Clamp(gptConf ?? diConf ?? 0.5, 0.0, 1.0);

        return Math.Clamp(diConf ?? 0.0, 0.0, 1.0);
    }

    /// <summary>
    /// Calcula la confianza de la extracción Azure Content Understanding usando tres componentes ponderados:
    ///   - Promedio de confianzas de campo (si el API las devuelve; si no, usa ratio de campos presentes).
    ///   - Ratio de campos requeridos que están presentes.
    ///   - Penalización por warnings (ratio invertido).
    /// </summary>
    /// <param name="fieldConfs">Confianzas individuales por campo del response CU (null si no disponibles).</param>
    /// <param name="camposPresentes">Número de campos con valor no vacío en el resultado.</param>
    /// <param name="camposTotales">Total de campos esperados por la tipología.</param>
    /// <param name="camposRequeridos">Campos requeridos según la tipología.</param>
    /// <param name="camposRequeridosPresentes">Campos requeridos que están efectivamente presentes.</param>
    /// <param name="warnings">Número de warnings producidos en la validación.</param>
    /// <param name="cfg">Configuración de pesos y umbrales; si null usa defaults.</param>
    public static (double Confianza, ConfidenceMetricasExtraccion Metricas) ExtracCU(
        IReadOnlyList<double?>? fieldConfs,
        int camposPresentes,
        int camposTotales,
        int camposRequeridos,
        int camposRequeridosPresentes,
        int warnings,
        ConfidenceConfig? cfg = null)
    {
        cfg ??= DefaultConfig;

        // Componente 1: promedio de confianza de campos
        double avgConf;
        int camposConConf = 0;
        if (fieldConfs is { Count: > 0 })
        {
            var validos = fieldConfs.Where(c => c.HasValue).Select(c => c!.Value).ToList();
            camposConConf = validos.Count;
            avgConf = validos.Count > 0 ? validos.Average() : (camposPresentes / (double)Math.Max(1, camposTotales));
        }
        else
        {
            avgConf = camposPresentes / (double)Math.Max(1, camposTotales);
        }

        // Componente 2: ratio de campos requeridos presentes
        double ratioReq = camposRequeridos > 0
            ? camposRequeridosPresentes / (double)camposRequeridos
            : 1.0;

        // Componente 3: penalización por warnings (invertida)
        double ratioWarn = camposTotales > 0
            ? Math.Min(1.0, warnings / (double)camposTotales)
            : 0.0;
        double penalizacionWarn = 1.0 - ratioWarn;

        double confianza = Math.Clamp(
            cfg.ExtracWeightCampos * avgConf
            + cfg.ExtracWeightRequeridos * ratioReq
            + cfg.ExtracWeightWarnings * penalizacionWarn,
            0.0, 1.0);

        var metricas = new ConfidenceMetricasExtraccion
        {
            PromedioConfianza = Math.Round(avgConf, 4),
            RatioRequeridos = Math.Round(ratioReq, 4),
            CamposConConfianza = camposConConf,
            CamposTotales = camposTotales
        };

        return (confianza, metricas);
    }

    /// <summary>
    /// Confianza de extracción GPT: usa el valor self-reported; 0.6 si no se proporcionó.
    /// </summary>
    public static double ExtracGPT(double? selfConf = null)
        => Math.Clamp(selfConf ?? 0.6, 0.0, 1.0);

    /// <summary>
    /// Calcula la confianza de la validación como 1 - (errores / reglasRequeridas).
    /// </summary>
    public static double Validacion(int errores, int reglasRequeridas)
    {
        if (reglasRequeridas <= 0) return 1.0;
        return Math.Clamp(1.0 - (errores / (double)reglasRequeridas), 0.0, 1.0);
    }

    /// <summary>
    /// Calcula la confianza global como el mínimo de los tres componentes.
    /// Si extracción está deshabilitada (extraccion == null), usa el mínimo de clasificación y validación.
    /// </summary>
    public static double Global(double clasif, double? extraccion, double validacion)
    {
        if (extraccion is null)
            return Math.Min(clasif, validacion);

        return Math.Min(Math.Min(clasif, extraccion.Value), validacion);
    }

    /// <summary>
    /// Determina el estado de calidad basado en la confianza global y los umbrales configurados.
    /// </summary>
    /// <returns>"OK" | "REVISION" | "ERROR"</returns>
    public static string EstadoCalidad(double global, ConfidenceConfig? cfg = null)
    {
        cfg ??= DefaultConfig;

        if (global >= cfg.UmbralOK) return "OK";
        if (global >= cfg.UmbralRevision) return "REVISION";
        return "ERROR";
    }
}
