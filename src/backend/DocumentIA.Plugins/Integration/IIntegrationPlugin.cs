using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DocumentIA.Plugins.Integration
{
    /// <summary>
    /// Interfaz base para todos los plugins de integracion
    /// </summary>
    public interface IIntegrationPlugin
    {
        /// <summary>
        /// Nombre unico del plugin
        /// </summary>
        string PluginName { get; }

        /// <summary>
        /// Version del plugin
        /// </summary>
        string Version { get; }

        /// <summary>
        /// Inicializa el plugin con configuracion especifica
        /// </summary>
        Task InitializeAsync(Dictionary<string, object> configuration);

        /// <summary>
        /// Ejecuta la integracion
        /// </summary>
        /// <param name="data">Datos a enviar</param>
        /// <returns>Resultado de la integracion</returns>
        Task<IntegrationResult> ExecuteAsync(Dictionary<string, object> data);

        /// <summary>
        /// Verifica si el plugin esta disponible
        /// </summary>
        Task<bool> HealthCheckAsync();
    }

    /// <summary>
    /// Resultado de una operacion de integracion
    /// </summary>
    public class IntegrationResult
    {
        public bool Success { get; set; }
        public string Status { get; set; } = "OK"; // OK | ERROR | REVISION
        public string Message { get; set; } = string.Empty;
        public Dictionary<string, object> ResponseData { get; set; } = new();
        public int StatusCode { get; set; }
        public TimeSpan Duration { get; set; }
        public string ExternalId { get; set; } = string.Empty;
        public List<string> Errors { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Excepcion para errores de plugins
    /// </summary>
    public class PluginException : Exception
    {
        public string PluginName { get; }
        public bool IsTransient { get; }

        public PluginException(string pluginName, string message, bool isTransient = false)
            : base(message)
        {
            PluginName = pluginName;
            IsTransient = isTransient;
        }

        public PluginException(string pluginName, string message, Exception innerException, bool isTransient = false)
            : base(message, innerException)
        {
            PluginName = pluginName;
            IsTransient = isTransient;
        }
    }
}
