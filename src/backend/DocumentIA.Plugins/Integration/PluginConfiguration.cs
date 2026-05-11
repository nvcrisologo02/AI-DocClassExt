using System.Collections.Generic;

namespace DocumentIA.Plugins.Integration
{
    /// <summary>
    /// Configuracion de plugins por tipologia
    /// </summary>
    public class PluginConfiguration
    {
        public string TipologiaId { get; set; } = string.Empty;
        public List<PluginConfig> Plugins { get; set; } = new();
    }

    public class PluginConfig
    {
        public string PluginKey { get; set; } = string.Empty;
        public string PluginType { get; set; } = "rest"; // rest, soap, custom
        public bool Enabled { get; set; } = true;
        public int Priority { get; set; } = 1; // Para ejecutar en orden
        public Dictionary<string, object> Configuration { get; set; } = new();
        public RetryPolicy? RetryPolicy { get; set; }
    }

    public class RetryPolicy
    {
        public int MaxRetries { get; set; } = 3;
        public int InitialDelayMs { get; set; } = 1000;
        public bool ExponentialBackoff { get; set; } = true;
        public List<int> RetryOnStatusCodes { get; set; } = new() { 408, 429, 500, 502, 503, 504 };
    }
}
