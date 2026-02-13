using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace DocumentIA.Plugins.Integration
{
    /// <summary>
    /// Plugin SOAP generico - solo transporte
    /// La logica de negocio esta en el servicio SOAP externo
    /// </summary>
    public class SoapPlugin : IIntegrationPlugin
    {
        private readonly HttpClient httpClient;
        private readonly ILogger<SoapPlugin> logger;
        
        private string endpoint = string.Empty;
        private string soapVersion = "1.1";
        private string soapAction = string.Empty;
        private string targetNamespace = "http://tempuri.org/";
        private string authType = "None";
        private string username = string.Empty;
        private string password = string.Empty;
        private int timeoutSeconds = 30;

        public string PluginName => "SoapPlugin";
        public string Version => "1.0.0";

        public SoapPlugin(HttpClient httpClient, ILogger<SoapPlugin> logger)
        {
            this.httpClient = httpClient;
            this.logger = logger;
        }

        public Task InitializeAsync(Dictionary<string, object> configuration)
        {
            endpoint = GetConfigValue(configuration, "endpoint") ?? string.Empty;
            soapVersion = GetConfigValue(configuration, "soapVersion") ?? "1.1";
            soapAction = GetConfigValue(configuration, "action") ?? string.Empty;
            targetNamespace = GetConfigValue(configuration, "namespace") ?? "http://tempuri.org/";
            authType = GetConfigValue(configuration, "authType") ?? "None";
            username = GetConfigValue(configuration, "username") ?? string.Empty;
            password = GetConfigValue(configuration, "password") ?? string.Empty;

            // Manejar timeoutSeconds de forma segura (puede venir como JsonElement)
            if (configuration.TryGetValue("timeoutSeconds", out var timeoutValue))
            {
                timeoutSeconds = ParseIntValue(timeoutValue, 30);
            }

            httpClient.Timeout = TimeSpan.FromSeconds(timeoutSeconds);

            logger.LogInformation("SoapPlugin inicializado. Endpoint: {Endpoint}, Version: {Version}", 
                endpoint, soapVersion);

            return Task.CompletedTask;
        }

        public async Task<IntegrationResult> ExecuteAsync(Dictionary<string, object> data)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new IntegrationResult();

            try
            {
                // Obtener datos a enviar
                var payload = new Dictionary<string, object>(data);
                payload.Remove("endpoint");
                payload.Remove("method");

                if (data.ContainsKey("datosExtraidos"))
                {
                    payload = data["datosExtraidos"] as Dictionary<string, object> ?? payload;
                }

                // Construir SOAP Envelope
                var soapEnvelope = BuildSoapEnvelope(payload);
                
                logger.LogDebug("Enviando SOAP Request a {Endpoint}", endpoint);

                // Crear request
                var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
                {
                    Content = new StringContent(soapEnvelope, Encoding.UTF8, GetContentType())
                };

                // SOAPAction header (SOAP 1.1)
                if (soapVersion == "1.1" && !string.IsNullOrEmpty(soapAction))
                {
                    request.Headers.Add("SOAPAction", $"\"{soapAction}\"");
                }

                // Autenticacion
                if (authType == "Basic" && !string.IsNullOrEmpty(username))
                {
                    var credentials = Convert.ToBase64String(
                        Encoding.UTF8.GetBytes($"{username}:{password}"));
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                        "Basic", credentials);
                }

                // Ejecutar
                var response = await httpClient.SendAsync(request);
                stopwatch.Stop();

                result.StatusCode = (int)response.StatusCode;
                result.Duration = stopwatch.Elapsed;

                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var parsedData = ExtractSoapBodyData(responseContent);

                    result.Success = true;
                    result.Status = "OK";
                    result.Message = "Consulta SOAP exitosa";
                    result.ResponseData = parsedData;

                    logger.LogInformation("SOAP exitoso. Campos recibidos: {Count}", parsedData.Count);
                }
                else
                {
                    result.Success = false;
                    result.Status = "ERROR";
                    result.Message = $"Error SOAP: {response.StatusCode}";
                    result.Errors.Add(responseContent);
                    
                    logger.LogWarning("SOAP fallo con status {Status}", response.StatusCode);
                }
            }
            catch (HttpRequestException ex)
            {
                stopwatch.Stop();
                result.Success = false;
                result.Status = "ERROR";
                result.Message = "Error de conexion con servicio SOAP";
                result.Errors.Add(ex.Message);
                result.Duration = stopwatch.Elapsed;
                result.Metadata["isTransient"] = true;
                
                logger.LogError(ex, "Error de conexion SOAP");
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                result.Success = false;
                result.Status = "ERROR";
                result.Message = "Error inesperado en SOAP";
                result.Errors.Add(ex.Message);
                result.Duration = stopwatch.Elapsed;
                
                logger.LogError(ex, "Error ejecutando SOAP");
            }

            return result;
        }

        public Task<bool> HealthCheckAsync()
        {
            return Task.FromResult(true);
        }

        private string BuildSoapEnvelope(Dictionary<string, object> payload)
        {
            var soapNs = soapVersion == "1.2" 
                ? "http://www.w3.org/2003/05/soap-envelope" 
                : "http://schemas.xmlsoap.org/soap/envelope/";

            var envelope = new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                new XElement(XName.Get("Envelope", soapNs),
                    new XAttribute(XNamespace.Xmlns + "soap", soapNs),
                    new XElement(XName.Get("Body", soapNs),
                        new XElement(XName.Get("Request", targetNamespace),
                            payload.Select(kvp => 
                                new XElement(kvp.Key, ConvertValueToString(kvp.Value)))
                        )
                    )
                )
            );

            return envelope.ToString();
        }

        private Dictionary<string, object> ExtractSoapBodyData(string soapResponse)
        {
            var result = new Dictionary<string, object>();

            try
            {
                var doc = XDocument.Parse(soapResponse);
                
                var body = doc.Descendants()
                    .FirstOrDefault(e => e.Name.LocalName == "Body");

                if (body != null)
                {
                    foreach (var element in body.Descendants())
                    {
                        if (!element.HasElements && !string.IsNullOrWhiteSpace(element.Value))
                        {
                            result[element.Name.LocalName] = element.Value;
                        }
                    }
                }

                if (result.Count == 0)
                {
                    result["raw"] = soapResponse;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error parseando respuesta SOAP, guardando como raw");
                result["raw"] = soapResponse;
            }

            return result;
        }

        private string GetContentType()
        {
            return soapVersion == "1.2" 
                ? "application/soap+xml" 
                : "text/xml";
        }

        private static string? GetConfigValue(Dictionary<string, object> config, string key)
        {
            if (!config.TryGetValue(key, out var value))
                return null;

            if (value is JsonElement element && element.ValueKind == JsonValueKind.String)
                return element.GetString();

            return value?.ToString();
        }

        private static int ParseIntValue(object? value, int defaultValue)
        {
            if (value == null)
                return defaultValue;

            if (value is int intValue)
                return intValue;

            if (value is JsonElement element)
            {
                if (element.TryGetInt32(out var parsedValue))
                    return parsedValue;
            }

            if (int.TryParse(value.ToString(), out var result))
                return result;

            return defaultValue;
        }

        private static string ConvertValueToString(object? value)
        {
            if (value == null)
                return string.Empty;

            if (value is JsonElement element)
            {
                return element.ValueKind switch
                {
                    JsonValueKind.String => element.GetString() ?? string.Empty,
                    JsonValueKind.Number => element.ToString(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    JsonValueKind.Null => string.Empty,
                    _ => element.ToString()
                };
            }

            return value.ToString() ?? string.Empty;
        }
    }
}
