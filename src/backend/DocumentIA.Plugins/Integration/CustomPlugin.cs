using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace DocumentIA.Plugins.Integration
{
    /// <summary>
    /// Plugin que carga y ejecuta enriquecedores personalizados (DLLs externas)
    /// Permite logica de negocio in-process sin HTTP
    /// </summary>
    public class CustomPlugin : IIntegrationPlugin
    {
        private readonly ILogger<CustomPlugin> logger;
        private string assemblyPath = string.Empty;
        private string className = string.Empty;
        private Dictionary<string, object> customConfig = new();
        private ICustomEnricher? enricherInstance;

        public string PluginName => "CustomPlugin";
        public string Version => "1.0.0";

        public CustomPlugin(ILogger<CustomPlugin> logger)
        {
            this.logger = logger;
        }

        public async Task InitializeAsync(Dictionary<string, object> configuration)
        {
            assemblyPath = GetConfigValue(configuration, "assemblyPath") ?? string.Empty;
            className = GetConfigValue(configuration, "className") ?? string.Empty;

            if (configuration.TryGetValue("customConfig", out var config) && 
                config is Dictionary<string, object> dict)
            {
                customConfig = dict;
            }

            // Cargar el enriquecedor externo
            if (!string.IsNullOrEmpty(assemblyPath) && !string.IsNullOrEmpty(className))
            {
                try
                {
                    // Resolver rutas relativas contra el directorio base de la aplicacion
                    if (!Path.IsPathRooted(assemblyPath))
                    {
                        assemblyPath = Path.GetFullPath(assemblyPath, AppContext.BaseDirectory);
                    }

                    logger.LogInformation(
                        "Cargando enriquecedor custom: {Assembly} - {Class}", 
                        assemblyPath, className);

                    // Verificar que el archivo existe
                    if (!File.Exists(assemblyPath))
                    {
                        throw new FileNotFoundException($"Assembly no encontrado: {assemblyPath}");
                    }

                    // Cargar assembly
                    var assembly = Assembly.LoadFrom(assemblyPath);
                    
                    // Crear instancia
                    var type = assembly.GetType(className);
                    if (type == null)
                    {
                        throw new InvalidOperationException(
                            $"Clase {className} no encontrada en {assemblyPath}");
                    }

                    enricherInstance = Activator.CreateInstance(type) as ICustomEnricher;
                    if (enricherInstance == null)
                    {
                        throw new InvalidOperationException(
                            $"La clase {className} no implementa ICustomEnricher");
                    }

                    // Inicializar el enriquecedor
                    await enricherInstance.InitializeAsync(customConfig);

                    logger.LogInformation(
                        "Enriquecedor custom cargado: {Name} v{Version}", 
                        enricherInstance.Name, enricherInstance.Version);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error cargando enriquecedor custom");
                    throw;
                }
            }
        }

        public async Task<IntegrationResult> ExecuteAsync(Dictionary<string, object> data)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new IntegrationResult();

            try
            {
                if (enricherInstance == null)
                {
                    throw new InvalidOperationException("Enriquecedor custom no inicializado");
                }

                // Obtener datos
                var payload = data.ContainsKey("datosExtraidos") 
                    ? data["datosExtraidos"] as Dictionary<string, object> ?? data
                    : data;

                logger.LogDebug("Ejecutando enriquecedor custom con {Count} campos", payload.Count);

                // Ejecutar enriquecedor
                var enrichedData = await enricherInstance.EnrichAsync(payload);

                stopwatch.Stop();

                result.Success = true;
                result.Status = "OK";
                result.Message = $"Enriquecimiento custom completado por {enricherInstance.Name}";
                result.ResponseData = enrichedData;
                result.Duration = stopwatch.Elapsed;

                logger.LogInformation(
                    "Enriquecimiento custom exitoso. Campos devueltos: {Count}", 
                    enrichedData.Count);
                // Si el payload original contenía idActivo y el enriquecedor no lo devolvió,
                // mantenerlo para asegurar trazabilidad en el pipeline
                if (data != null && data.TryGetValue("idActivo", out var idActivoFromPayload))
                {
                    var idActivoStr = idActivoFromPayload?.ToString();
                    if (!string.IsNullOrWhiteSpace(idActivoStr) && !result.ResponseData.ContainsKey("idActivo"))
                    {
                        result.ResponseData["idActivo"] = idActivoStr;
                    }
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                result.Success = false;
                result.Status = "ERROR";
                result.Message = $"Error en enriquecedor custom: {ex.Message}";
                result.Errors.Add(ex.ToString());
                result.Duration = stopwatch.Elapsed;
                
                logger.LogError(ex, "Error ejecutando enriquecedor custom");
            }

            return result;
        }

        public async Task<bool> HealthCheckAsync()
        {
            if (enricherInstance == null)
                return false;

            try
            {
                return await enricherInstance.HealthCheckAsync();
            }
            catch
            {
                return false;
            }
        }

        private static string? GetConfigValue(Dictionary<string, object> config, string key)
        {
            return config.TryGetValue(key, out var value) ? value?.ToString() : null;
        }
    }
}
