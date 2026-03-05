using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DocumentIA.Plugins.Integration;

namespace Sareb.Enrichments
{
    /// <summary>
    /// Enriquecedor de ejemplo para Nota Simple
    /// Aplica reglas de negocio especificas de SAREB
    /// </summary>
    public class NotaSimpleEnricher : ICustomEnricher
    {
        public string Name => "SAREB Nota Simple Enricher";
        public string Version => "1.0.0";

        private bool enableCaching = false;

        public Task InitializeAsync(Dictionary<string, object> configuration)
        {
            if (configuration.TryGetValue("enableCaching", out var cachingObj))
                enableCaching = Convert.ToBoolean(cachingObj);

            Console.WriteLine($"[{Name}] Inicializado. Caching: {enableCaching}");

            return Task.CompletedTask;
        }

        public Task<Dictionary<string, object>> EnrichAsync(Dictionary<string, object> data)
        {
            var enriched = new Dictionary<string, object>(data);

            // 1. Clasificar riesgo basado en cargas
            if (data.ContainsKey("Cargas"))
            {
                var cargasCount = 0;
                if (data["Cargas"] is List<object> cargas)
                    cargasCount = cargas.Count;

                enriched["NumeroCargas"] = cargasCount;
                enriched["TieneCargas"] = cargasCount > 0;

                if (cargasCount > 3)
                    enriched["NivelRiesgoCargas"] = "ALTO";
                else if (cargasCount > 0)
                    enriched["NivelRiesgoCargas"] = "MEDIO";
                else
                    enriched["NivelRiesgoCargas"] = "BAJO";
            }

            // 2. Completitud de datos
            var camposRequeridos = new[] { "FincaRegistral", "Titular", "superficie", "Direccion" };
            var camposFaltantes = camposRequeridos.Count(c => 
                !data.ContainsKey(c) || string.IsNullOrEmpty(data[c]?.ToString()));

            var completitud = (int)((1 - (camposFaltantes / (double)camposRequeridos.Length)) * 100);
            enriched["CompletitudDocumento"] = completitud;
            enriched["RequiereRevisionManual"] = camposFaltantes > 1;

            // 3. ID interno SAREB
            enriched["IdInternoSAREB"] = $"NS-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}";

            // 4. Prioridad de gestion
            var prioridad = "NORMAL";
            if (enriched.ContainsKey("TieneCargas") && (bool)enriched["TieneCargas"]!)
                prioridad = "ALTA";
            if (enriched.ContainsKey("RequiereRevisionManual") && (bool)enriched["RequiereRevisionManual"]!)
                prioridad = "URGENTE";

            enriched["PrioridadGestion"] = prioridad;

            // 5. Metadata
            enriched["EnriquecidoEn"] = DateTime.UtcNow.ToString("o");
            enriched["EnriquecidoPor"] = Name;

            // 6. Preservar / propagar idActivo si viene en la entrada (soporte case-insensitive)
            if (data != null)
            {
                // Buscar claves que representen idActivo en diferentes formatos
                var idKey = data.Keys.FirstOrDefault(k => string.Equals(k, "idActivo", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(k, "id_activo", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(k, "IdActivo", StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrEmpty(idKey) && data.TryGetValue(idKey, out var idVal))
                {
                    var idStr = idVal?.ToString();
                    if (!string.IsNullOrWhiteSpace(idStr) && !enriched.ContainsKey("idActivo"))
                    {
                        enriched["idActivo"] = idStr;
                    }
                }
            }

            return Task.FromResult(enriched);
        }

        public Task<bool> HealthCheckAsync()
        {
            return Task.FromResult(true);
        }
    }
}
