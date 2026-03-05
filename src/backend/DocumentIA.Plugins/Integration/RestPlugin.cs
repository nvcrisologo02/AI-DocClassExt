using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;


namespace DocumentIA.Plugins.Integration
{
    /// <summary>
    /// Plugin generico para integraciones REST
    /// Soporta configuracion flexible de endpoints, headers, autenticacion
    /// </summary>
    public class RestPlugin : IIntegrationPlugin
    {
        private readonly HttpClient httpClient;
        private string baseUrl = string.Empty;
        private string endpoint = "/api/process";
        private string authToken = string.Empty;
        private string authType = "Bearer"; // Bearer, ApiKey, Basic
        private Dictionary<string, string> defaultHeaders = new();
        private int timeoutSeconds = 30;


        public string PluginName => "RestPlugin";
        public string Version => "1.0.0";


        public RestPlugin(HttpClient httpClient)
        {
            this.httpClient = httpClient;
        }


        public Task InitializeAsync(Dictionary<string, object> configuration)
        {
            if (configuration.TryGetValue("baseUrl", out var baseUrlValue))
                baseUrl = GetStringValue(baseUrlValue) ?? string.Empty;


            if (configuration.TryGetValue("endpoint", out var endpointValue))
                endpoint = GetStringValue(endpointValue) ?? "/api/process";


            if (configuration.TryGetValue("authToken", out var authTokenValue))
                authToken = GetStringValue(authTokenValue) ?? string.Empty;


            if (configuration.TryGetValue("authType", out var authTypeValue))
                authType = GetStringValue(authTypeValue) ?? "Bearer";


            if (configuration.TryGetValue("timeoutSeconds", out var timeoutValue)
                && TryGetIntValue(timeoutValue, out var parsedTimeout))
            {
                timeoutSeconds = parsedTimeout;
            }


            if (configuration.TryGetValue("headers", out var headersValue))
                defaultHeaders = ParseHeaders(headersValue);


            httpClient.Timeout = TimeSpan.FromSeconds(timeoutSeconds);


            if (!string.IsNullOrEmpty(authToken))
            {
                if (authType == "Bearer")
                    httpClient.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authToken);
                else if (authType == "ApiKey")
                    httpClient.DefaultRequestHeaders.Add("X-API-Key", authToken);
            }


            foreach (var header in defaultHeaders)
            {
                // Content-* headers are not valid on DefaultRequestHeaders; they belong to HttpContent.
                if (IsContentHeader(header.Key))
                    continue;


                if (httpClient.DefaultRequestHeaders.Contains(header.Key))
                    httpClient.DefaultRequestHeaders.Remove(header.Key);


                httpClient.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
            }


            return Task.CompletedTask;
        }


        private static string? GetStringValue(object? value)
        {
            if (value == null)
                return null;


            if (value is JsonElement element)
            {
                if (element.ValueKind == JsonValueKind.String)
                    return element.GetString();


                return element.ToString();
            }


            return value.ToString();
        }


        private static bool TryGetIntValue(object? value, out int result)
        {
            switch (value)
            {
                case null:
                    result = default;
                    return false;
                case int intValue:
                    result = intValue;
                    return true;
                case long longValue when longValue <= int.MaxValue && longValue >= int.MinValue:
                    result = (int)longValue;
                    return true;
                case JsonElement element when element.ValueKind == JsonValueKind.Number:
                    return element.TryGetInt32(out result);
                case JsonElement element when element.ValueKind == JsonValueKind.String:
                    return int.TryParse(element.GetString(), out result);
                default:
                    return int.TryParse(value.ToString(), out result);
            }
        }


        private static Dictionary<string, string> ParseHeaders(object? headersValue)
        {
            if (headersValue is Dictionary<string, string> stringHeaders)
                return stringHeaders;


            if (headersValue is Dictionary<string, object> objectHeaders)
            {
                var mappedHeaders = new Dictionary<string, string>();
                foreach (var header in objectHeaders)
                {
                    mappedHeaders[header.Key] = GetStringValue(header.Value) ?? string.Empty;
                }


                return mappedHeaders;
            }


            if (headersValue is JsonElement headersElement && headersElement.ValueKind == JsonValueKind.Object)
            {
                var mappedHeaders = new Dictionary<string, string>();
                foreach (var property in headersElement.EnumerateObject())
                {
                    mappedHeaders[property.Name] = property.Value.ToString();
                }


                return mappedHeaders;
            }


            return new Dictionary<string, string>();
        }


        private static bool IsContentHeader(string headerName)
        {
            return headerName.StartsWith("Content-", StringComparison.OrdinalIgnoreCase);
        }


        public async Task<IntegrationResult> ExecuteAsync(Dictionary<string, object> data)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new IntegrationResult();


            try
            {
                // Determinar endpoint y metodo
                string executionEndpoint = data.ContainsKey("endpoint") 
                    ? GetStringValue(data["endpoint"]) ?? endpoint 
                    : endpoint;
                
                string method = data.ContainsKey("method") 
                    ? GetStringValue(data["method"]) ?? "POST" 
                    : "POST";
                    
                string fullUrl = baseUrl.TrimEnd('/') + "/" + executionEndpoint.TrimStart('/');

                // Preparar payload limpio (sin campos de control)
                var payload = new Dictionary<string, object>(data);
                payload.Remove("endpoint");
                payload.Remove("method");
                
                // Si hay un campo "payload" especifico, usarlo
                if (data.ContainsKey("payload"))
                {
                    payload = data["payload"] as Dictionary<string, object> ?? payload;
                }

                // Serializar payload a JSON string para asegurar Content-Length correcto
                var jsonPayload = JsonSerializer.Serialize(payload, new JsonSerializerOptions
                {
                    WriteIndented = false
                });

                HttpResponseMessage response;


                switch (method.ToUpper())
                {
                    case "POST":
                        var postContent = new StringContent(
                            jsonPayload, 
                            Encoding.UTF8, 
                            "application/json");
                        response = await httpClient.PostAsync(fullUrl, postContent);
                        break;


                    case "PUT":
                        var putContent = new StringContent(
                            jsonPayload, 
                            Encoding.UTF8, 
                            "application/json");
                        response = await httpClient.PutAsync(fullUrl, putContent);
                        break;


                    case "GET":
                        var queryString = BuildQueryString(payload);
                        response = await httpClient.GetAsync(fullUrl + queryString);
                        break;


                    case "DELETE":
                        response = await httpClient.DeleteAsync(fullUrl);
                        break;


                    default:
                        throw new InvalidOperationException($"Metodo HTTP no soportado: {method}");
                }


                stopwatch.Stop();


                result.StatusCode = (int)response.StatusCode;
                result.Duration = stopwatch.Elapsed;
                result.Success = response.IsSuccessStatusCode;


                if (response.IsSuccessStatusCode)
                {
                    result.Status = "OK";
                    result.Message = $"Integracion exitosa con {baseUrl}";


                    var responseContent = await response.Content.ReadAsStringAsync();
                    if (!string.IsNullOrEmpty(responseContent))
                    {
                        try
                        {
                            // Intentar deserializar como Dictionary
                            var responseDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(responseContent);
                            if (responseDict != null)
                            {
                                // Convertir JsonElement a object para compatibilidad
                                foreach (var kvp in responseDict)
                                {
                                    result.ResponseData[kvp.Key] = ConvertJsonElement(kvp.Value);
                                }
                                // Si el payload incluía idActivo y la respuesta no, conservarlo para trazabilidad
                                if (payload != null && payload.TryGetValue("idActivo", out var idActivoValue))
                                {
                                    var idActivoStr = idActivoValue?.ToString();
                                    if (!string.IsNullOrWhiteSpace(idActivoStr) && !result.ResponseData.ContainsKey("idActivo"))
                                    {
                                        result.ResponseData["idActivo"] = idActivoStr;
                                    }
                                }
                            }
                        }
                        catch
                        {
                            result.ResponseData["raw"] = responseContent;
                        }
                    }
                }
                else
                {
                    result.Status = "ERROR";
                    result.Message = $"Error en integracion: {response.StatusCode}";
                    result.Errors.Add($"HTTP {response.StatusCode}: {response.ReasonPhrase}");


                    var errorContent = await response.Content.ReadAsStringAsync();
                    if (!string.IsNullOrEmpty(errorContent))
                        result.Errors.Add(errorContent);
                }
            }
            catch (HttpRequestException ex)
            {
                stopwatch.Stop();
                result.Success = false;
                result.Status = "ERROR";
                result.Message = "Error de conexion con el servicio externo";
                result.Errors.Add(ex.Message);
                result.Duration = stopwatch.Elapsed;
                result.Metadata["exception"] = ex.GetType().Name;
                result.Metadata["isTransient"] = true;
            }
            catch (TaskCanceledException ex)
            {
                stopwatch.Stop();
                result.Success = false;
                result.Status = "ERROR";
                result.Message = "Timeout en la conexion con el servicio externo";
                result.Errors.Add($"La operacion excedio el timeout de {timeoutSeconds} segundos");
                result.Duration = stopwatch.Elapsed;
                result.Metadata["exception"] = ex.GetType().Name;
                result.Metadata["isTransient"] = true;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                result.Success = false;
                result.Status = "ERROR";
                result.Message = "Error inesperado en la integracion";
                result.Errors.Add(ex.Message);
                result.Duration = stopwatch.Elapsed;
                result.Metadata["exception"] = ex.GetType().Name;
            }


            return result;
        }


        public async Task<bool> HealthCheckAsync()
        {
            try
            {
                var healthUrl = baseUrl.TrimEnd('/') + "/health";
                var response = await httpClient.GetAsync(healthUrl);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }


        private string BuildQueryString(Dictionary<string, object>? parameters)
        {
            if (parameters == null || parameters.Count == 0)
                return string.Empty;


            var queryParams = new List<string>();
            foreach (var param in parameters)
            {
                queryParams.Add($"{Uri.EscapeDataString(param.Key)}={Uri.EscapeDataString(param.Value?.ToString() ?? string.Empty)}");
            }


            return "?" + string.Join("&", queryParams);
        }


        /// <summary>
        /// Convierte JsonElement a object para compatibilidad
        /// </summary>
        private static object ConvertJsonElement(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                    return element.GetString() ?? string.Empty;
                case JsonValueKind.Number:
                    if (element.TryGetInt32(out var intVal))
                        return intVal;
                    if (element.TryGetInt64(out var longVal))
                        return longVal;
                    return element.GetDouble();
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.False:
                    return false;
                case JsonValueKind.Null:
                    return null!;
                case JsonValueKind.Object:
                    var dict = new Dictionary<string, object>();
                    foreach (var prop in element.EnumerateObject())
                    {
                        dict[prop.Name] = ConvertJsonElement(prop.Value);
                    }
                    return dict;
                case JsonValueKind.Array:
                    var list = new List<object>();
                    foreach (var item in element.EnumerateArray())
                    {
                        list.Add(ConvertJsonElement(item));
                    }
                    return list;
                default:
                    return element.ToString();
            }
        }
    }
}
