using System.Threading.Tasks;

namespace DocumentIA.Core.Caching;

/// <summary>
/// Interfaz para gestionar invalidación de caché cuando cambian datos configurados.
/// AB#99750: Invalidación de caché JSON parseado
/// </summary>
public interface ICacheInvalidationService
{
    /// <summary>
    /// Invalida toda la caché de configuración.
    /// Se usa cuando se publican cambios masivos de tipologías.
    /// </summary>
    Task InvalidateAllAsync();

    /// <summary>
    /// Invalida caché específica de una tipología.
    /// </summary>
    /// <param name="tipologiaId">ID o código de tipología</param>
    /// <param name="tipologiaVersion">Versión para generar claves precisas</param>
    /// <param name="tipologiaUpdateTicks">Timestamp de actualización para claves con ticks</param>
    Task InvalidateTipologiaAsync(int tipologiaId, string tipologiaVersion, long tipologiaUpdateTicks);

    /// <summary>
    /// Invalida caché de configuración de plugins para una tipología.
    /// </summary>
    /// <param name="tipologiaId">ID de tipología</param>
    Task InvalidatePluginConfigAsync(int tipologiaId);
}
