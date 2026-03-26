using DocumentIA.Desktop.Models;
using Newtonsoft.Json;
using RestSharp;
using System;
using System.IO;
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

        public OrchestratorApiClient(string baseUrl = "http://localhost:7071")
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _client = new RestClient(_baseUrl);
        }

        public async Task<bool> CheckConnectionAsync()
        {
            try
            {
                var request = new RestRequest("/api/health", Method.Get);
                var response = await _client.ExecuteAsync(request);
                return response.IsSuccessful;
            }
            catch
            {
                return false;
            }
        }

        public async Task<ProcessingResponse> IngestDocumentAsync(ProcessingRequest request)
        {
            var restRequest = new RestRequest("/api/IngestDocument", Method.Post);
            restRequest.AddHeader("Content-Type", "application/json");
            var json = JsonConvert.SerializeObject(request, Formatting.Indented);
            restRequest.AddParameter("application/json", json, ParameterType.RequestBody);

            var response = await _client.ExecuteAsync(restRequest);

            if (!response.IsSuccessful)
            {
                throw new Exception($"API Error: {response.StatusCode} - {response.Content}");
            }

            var result = JsonConvert.DeserializeObject<ProcessingResponse>(response.Content);
            return result;
        }

        public async Task<ProcessingStatus> GetStatusAsync(string statusUri)
        {
            try
            {
                // Fix URL if it has incorrect localhost reference
                var fixedUri = statusUri.Replace("http://localhost/", "http://localhost:7071/");

                var request = new RestRequest(fixedUri, Method.Get);
                var response = await _client.ExecuteAsync(request);

                if (!response.IsSuccessful)
                {
                    throw new Exception($"Status check failed: {response.StatusCode}");
                }

                var result = JsonConvert.DeserializeObject<ProcessingStatus>(response.Content);
                return result;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get status: {ex.Message}", ex);
            }
        }
    }
}
