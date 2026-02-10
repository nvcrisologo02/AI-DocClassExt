using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using DocumentIA.Core.Validation;
using DocumentIA.Core.Configuration;
using DocumentIA.Core.Validation.Models;
using DocumentIA.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DocumentIA.Functions.Activities
{
    public class ValidarActivity
    {
        private readonly ILogger<ValidarActivity> _logger;

        public ValidarActivity(ILogger<ValidarActivity> logger)
        {
            _logger = logger;
        }

        [Function(nameof(ValidarActivity))]
        public Task<DetalleValidacion> Run(
            [ActivityTrigger] ValidacionInput input)
        {
            _logger.LogInformation($"Validando documento de tipologia: {input.Tipologia}");

            try
            {
                var configLoader = new TipologiaConfigLoader("config/tipologias");
                ValidationEngine engine;
                
                try
                {
                    engine = configLoader.BuildValidationEngine(input.Tipologia.ToLower());
                    _logger.LogInformation($"Configuracion de validacion cargada para {input.Tipologia}");
                }
                catch (System.IO.FileNotFoundException ex)
                {
                    _logger.LogWarning(ex, $"No se encontro configuracion de validacion para {input.Tipologia}");
                    
                    return Task.FromResult(new DetalleValidacion
                    {
                        TotalReglas = 0,
                        ReglasAplicadas = 0,
                        Errores = 0,
                        Warnings = 1,
                        Validaciones = new List<ItemValidacion>
                        {
                            new ItemValidacion
                            {
                                Campo = "General",
                                Estado = "WARNING",
                                Severidad = "Warning",
                                Mensaje = $"No hay reglas de validacion configuradas para tipologia '{input.Tipologia}'",
                                Sugerencia = "Crear archivo de configuracion de validacion"
                            }
                        },
                        ConfianzaValidacion = 0.5
                    });
                }

                var report = engine.ValidateDocument(input.DatosExtraidos);

                _logger.LogInformation(
                    $"Validacion completada: {report.ErrorCount} errores, {report.WarningCount} warnings");

                var detalle = new DetalleValidacion
                {
                    TotalReglas = report.Results.Count,
                    ReglasAplicadas = report.Results.Count,
                    Errores = report.ErrorCount,
                    Warnings = report.WarningCount,
                    Validaciones = report.Results.Select(r => new ItemValidacion
                    {
                        Campo = r.FieldName,
                        Estado = r.IsValid ? "OK" : "FAILED",
                        Severidad = r.Severity.ToString(),
                        Mensaje = r.Message,
                        Sugerencia = r.SuggestionString
                    }).ToList(),
                    ConfianzaValidacion = report.IsValid ? 1.0 : 
                        Math.Max(0.0, 1.0 - ((double)report.ErrorCount / Math.Max(report.Results.Count, 1)))
                };

                return Task.FromResult(detalle);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error durante validacion");
                throw;
            }
        }
    }
}
