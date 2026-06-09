using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DocumentIA.Core.Caching
{
    /// <summary>
    /// Interfaz de caching para configuraciones JSON.
    /// Proporciona operaciones de lectura, escritura y validación de cache con TTL configurable.
    /// AB#99750 [OPT-1]: Optimización de caching para JSON configuration
    /// </summary>
    public interface IConfigurationCache
    {
        /// <summary>
        /// Obtiene un valor del cache por clave.
        /// </summary>
        /// <param name="key">Clave del item en cache</param>
        /// <returns>Valor cacheado o null si no existe o expiró</returns>
        Task<T> GetAsync<T>(string key) where T : class;

        /// <summary>
        /// Establece un valor en cache con TTL opcional.
        /// </summary>
        /// <param name="key">Clave del item</param>
        /// <param name="value">Valor a cachear</param>
        /// <param name="ttl">Tiempo de vida del cache (null = sin expiración)</param>
        Task SetAsync<T>(string key, T value, TimeSpan? ttl = null) where T : class;

        /// <summary>
        /// Remueve un item del cache por clave.
        /// </summary>
        /// <param name="key">Clave a remover</param>
        Task RemoveAsync(string key);

        /// <summary>
        /// Limpia todo el cache.
        /// </summary>
        Task ClearAsync();

        /// <summary>
        /// Verifica si una clave existe en cache y no ha expirado.
        /// </summary>
        /// <param name="key">Clave a verificar</param>
        bool Exists(string key);

        /// <summary>
        /// Obtiene estadísticas del cache (ítems, tamaño, etc).
        /// </summary>
        CacheStats GetStats();
    }

    /// <summary>
    /// Estadísticas del cache.
    /// </summary>
    public class CacheStats
    {
        /// <summary>
        /// Total de items en cache.
        /// </summary>
        public int ItemCount { get; set; }

        /// <summary>
        /// Tamaño aproximado en bytes.
        /// </summary>
        public long ApproximateSizeBytes { get; set; }

        /// <summary>
        /// Último acceso (UTC).
        /// </summary>
        public DateTime? LastAccessUtc { get; set; }

        /// <summary>
        /// Contador de hits.
        /// </summary>
        public long HitCount { get; set; }

        /// <summary>
        /// Contador de misses.
        /// </summary>
        public long MissCount { get; set; }

        /// <summary>
        /// Ratio de hit rate (0.0 - 1.0).
        /// </summary>
        public double HitRate => (HitCount + MissCount) > 0 ? (double)HitCount / (HitCount + MissCount) : 0.0;
    }
}
