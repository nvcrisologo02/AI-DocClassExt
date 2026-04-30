using DocumentIA.Desktop.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace DocumentIA.Desktop.Services
{
    public interface IOrchestratorApiClient
    {
        Task<bool> CheckConnectionAsync();
        Task<SystemHealthResponse?> GetSystemHealthAsync();
        Task<ProcessingResponse> IngestDocumentAsync(ProcessingRequest request);
        Task<ProcessingStatus> GetStatusAsync(string statusUri);
        Task<List<TipologiaPublicadaDto>> GetTipologiasPublicadasAsync();
    }

    public class OrchestratorApiClient : IOrchestratorApiClient
    {
        private readonly RestClient _client;
        private readonly string _baseUrl;
        private readonly string? _functionKey;
        private readonly JsonSerializerSettings _jsonSettings;

        public OrchestratorApiClient(string baseUrl = "http://localhost:7071", string? functionKey = null)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _functionKey = functionKey;
            _client = new RestClient(_baseUrl);
            
            // Configure JSON settings to be more lenient with parsing
            _jsonSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                DateFormatString = "yyyy-MM-ddTHH:mm:ss.fffffffZ",
                DateTimeZoneHandling = DateTimeZoneHandling.Utc,
                FloatParseHandling = FloatParseHandling.Double,
                Formatting = Formatting.Indented,
                Error = (sender, args) => 
                {
                    // Log the error but continue deserializing
                    System.Diagnostics.Debug.WriteLine($"JSON Error at {args.CurrentObject}: {args.ErrorContext.Error.Message}");
                    args.ErrorContext.Handled = true;
                }
            };
        }

        private void AddFunctionKey(RestRequest request)
        {
            if (!string.IsNullOrWhiteSpace(_functionKey))
                request.AddHeader("x-functions-key", _functionKey);
        }

        private string FixStatusUri(string statusUri)
        {
            // En local puede venir con localhost sin puerto; en Azure puede venir con la URL interna
            var uri = statusUri
                .Replace("http://localhost/", $"{_baseUrl}/")
                .Replace("https://localhost/", $"{_baseUrl}/");

            // Si la URI empieza por http(s):// y la base es distinta, usarla tal cual (Azure ya devuelve la URL correcta)
            return uri;
        }

        public async Task<bool> CheckConnectionAsync()
        {
            try
            {
                var health = await GetSystemHealthAsync();
                if (health != null)
                {
                    return true;
                }

                var request = new RestRequest("/api/tipologias", Method.Get);
                AddFunctionKey(request);
                var response = await _client.ExecuteAsync(request);
                return response != null && !string.IsNullOrEmpty(response.Content);
            }
            catch (HttpRequestException ex)
            {
                System.Diagnostics.Debug.WriteLine($"HttpRequestException: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Exception in CheckConnection: {ex.Message}");
                return false;
            }
        }

        public async Task<SystemHealthResponse?> GetSystemHealthAsync()
        {
            try
            {
                var request = new RestRequest("/api/healthcheck", Method.Post);
                AddFunctionKey(request);
                var response = await _client.ExecuteAsync(request);

                // El endpoint puede devolver 503 cuando hay componentes unhealthy.
                if (response == null || string.IsNullOrWhiteSpace(response.Content))
                {
                    return null;
                }

                var statusCode = (int)response.StatusCode;
                if (statusCode != 200 && statusCode != 503)
                {
                    return null;
                }

                return JsonConvert.DeserializeObject<SystemHealthResponse>(response.Content, _jsonSettings);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetSystemHealthAsync Exception: {ex.Message}");
                return null;
            }
        }

        public async Task<ProcessingResponse> IngestDocumentAsync(ProcessingRequest request)
        {
            try
            {
                var restRequest = new RestRequest("/api/IngestDocument", Method.Post);
                AddFunctionKey(restRequest);

                // Serializar el request directamente
                var jsonString = JsonConvert.SerializeObject(request);
                
                // Usar AddBody que envía el string directly sin serializar de nuevo
                restRequest.AddBody(jsonString, "application/json");

                var response = await _client.ExecuteAsync(restRequest);

                if (!response.IsSuccessful)
                {
                    System.Diagnostics.Debug.WriteLine($"IngestDocument Error {response.StatusCode}: {response.Content}");
                    throw new Exception($"API Error: {response.StatusCode} - {response.Content}");
                }

                if (string.IsNullOrWhiteSpace(response.Content))
                {
                    throw new Exception("API returned an empty response");
                }

                var result = JsonConvert.DeserializeObject<ProcessingResponse>(response.Content, _jsonSettings);
                return result ?? throw new Exception("Failed to deserialize ingest response");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"IngestDocumentAsync Exception: {ex.Message}");
                throw;
            }
        }

        public async Task<List<TipologiaPublicadaDto>> GetTipologiasPublicadasAsync()
        {
            try
            {
                var request = new RestRequest("/api/tipologias", Method.Get);
                AddFunctionKey(request);
                var response = await _client.ExecuteAsync(request);

                if (!response.IsSuccessful || string.IsNullOrWhiteSpace(response.Content))
                    return new List<TipologiaPublicadaDto>();

                return JsonConvert.DeserializeObject<List<TipologiaPublicadaDto>>(response.Content, _jsonSettings)
                       ?? new List<TipologiaPublicadaDto>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetTipologiasPublicadasAsync Exception: {ex.Message}");
                return new List<TipologiaPublicadaDto>();
            }
        }

        public async Task<ProcessingStatus> GetStatusAsync(string statusUri)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(statusUri))
                {
                    throw new ArgumentException("Status URI is required", nameof(statusUri));
                }

                var fixedUri = FixStatusUri(statusUri);

                // Use HttpClient directly for the status check: the URI is absolute and may already
                // contain a ?code=... query param (Durable Functions system key). RestSharp combines
                // relative paths with its BaseUrl and can corrupt absolute URLs with query strings.
                using var http = new System.Net.Http.HttpClient();
                if (!string.IsNullOrWhiteSpace(_functionKey))
                    http.DefaultRequestHeaders.Add("x-functions-key", _functionKey);

                var httpResponse = await http.GetAsync(fixedUri);

                if (!httpResponse.IsSuccessStatusCode)
                {
                    throw new Exception($"Status check failed: {httpResponse.StatusCode}");
                }

                var content = await httpResponse.Content.ReadAsStringAsync();

                if (string.IsNullOrWhiteSpace(content))
                {
                    throw new Exception("Status response is empty");
                }

                try
                {
                    var result = JsonConvert.DeserializeObject<ProcessingStatus>(content, _jsonSettings);
                    return result ?? throw new Exception("Failed to deserialize status response");
                }
                catch (JsonSerializationException jsonEx)
                {
                    System.Diagnostics.Debug.WriteLine($"JSON Deserialization Error: {jsonEx.Message}");
                    System.Diagnostics.Debug.WriteLine($"Response Content: {content}");
                    throw new Exception($"Failed to parse status response: {jsonEx.Message}", jsonEx);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get status: {ex.Message}", ex);
            }
        }
    }
}
