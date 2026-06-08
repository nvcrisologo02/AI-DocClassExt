namespace DocumentIA.Functions.Services.Abstractions;

public interface ITelemetryService
{
    void TrackEvent(string eventName, IDictionary<string, string>? properties = null);
    void TrackMetric(string metricName, double value, IDictionary<string, string>? properties = null);
}