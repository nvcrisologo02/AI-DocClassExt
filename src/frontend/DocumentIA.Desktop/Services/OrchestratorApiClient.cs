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
        Task<ProcessingResponse> IngestDocumentAsync(ProcessingRequest request);
        Task<ProcessingStatus> GetStatusAsync(string statusUri);
    }

    public class OrchestratorApiClient : IOrchestratorApiClient
    {
        private readonly RestClient _client;
        private readonly string _baseUrl;
        private readonly JsonSerializerSettings _jsonSettings;

        public OrchestratorApiClient(string baseUrl = "http://localhost:7071")
        {
            _baseUrl = baseUrl.TrimEnd('/');
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

        public async Task<bool> CheckConnectionAsync()
        {
            try
            {
                // Simply try to reach http://localhost:7071 and see if it responds
                // If we get any response (404, 500, etc.), the server is up
                var request = new RestRequest("/", Method.Get);
                var response = await _client.ExecuteAsync(request);
                
                // If we got any response at all, the server is reachable
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

        public async Task<ProcessingResponse> IngestDocumentAsync(ProcessingRequest request)
        {
            try
            {
                var restRequest = new RestRequest("/api/IngestDocument", Method.Post);
                
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

        public async Task<ProcessingStatus> GetStatusAsync(string statusUri)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(statusUri))
                {
                    throw new ArgumentException("Status URI is required", nameof(statusUri));
                }

                // Fix URL if it has incorrect localhost reference
                var fixedUri = statusUri.Replace("http://localhost/", "http://localhost:7071/");

                var request = new RestRequest(fixedUri, Method.Get);
                var response = await _client.ExecuteAsync(request);

                if (!response.IsSuccessful)
                {
                    throw new Exception($"Status check failed: {response.StatusCode}");
                }

                try
                {
                    if (string.IsNullOrWhiteSpace(response.Content))
                    {
                        throw new Exception("Status response is empty");
                    }

                    var result = JsonConvert.DeserializeObject<ProcessingStatus>(response.Content, _jsonSettings);
                    return result ?? throw new Exception("Failed to deserialize status response");
                }
                catch (JsonSerializationException jsonEx)
                {
                    System.Diagnostics.Debug.WriteLine($"JSON Deserialization Error: {jsonEx.Message}");
                    System.Diagnostics.Debug.WriteLine($"Response Content: {response.Content}");
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
