using DocumentIA.Functions.Services.Abstractions;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;

namespace DocumentIA.Functions.Services;

public class ApplicationInsightsTelemetryService : ITelemetryService
{
    private readonly TelemetryClient _telemetryClient;

    public ApplicationInsightsTelemetryService(TelemetryClient telemetryClient)
    {
        _telemetryClient = telemetryClient;
    }

    public void TrackEvent(string eventName, IDictionary<string, string>? properties = null)
    {
        _telemetryClient.TrackEvent(eventName, properties);
    }

    public void TrackMetric(string metricName, double value, IDictionary<string, string>? properties = null)
    {
        var metric = new MetricTelemetry(metricName, value);
        if (properties is not null)
        {
            foreach (var kvp in properties)
            {
                metric.Properties[kvp.Key] = kvp.Value;
            }
        }

        _telemetryClient.TrackMetric(metric);
    }
}