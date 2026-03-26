using Newtonsoft.Json;
using System.Collections.Generic;

namespace DocumentIA.Desktop.Models
{
    public class ProcessingRequest
    {
        [JsonProperty("instrucciones")]
        public Instructions Instructions { get; set; }

        [JsonProperty("documento")]
        public DocumentInfo Document { get; set; }

        [JsonProperty("trazabilidad")]
        public Traceability Traceability { get; set; }
    }

    public class Instructions
    {
        [JsonProperty("expectedType")]
        public string ExpectedType { get; set; }

        [JsonProperty("skipDuplicateCheck")]
        public bool SkipDuplicateCheck { get; set; }

        [JsonProperty("forceReprocess")]
        public bool ForceReprocess { get; set; }

        [JsonProperty("SkipGDCUpload")]
        public bool SkipGDCUpload { get; set; }

        [JsonProperty("classification")]
        public ClassificationSettings Classification { get; set; }

        [JsonProperty("extraction")]
        public ExtractionSettings Extraction { get; set; }
    }

    public class ClassificationSettings
    {
        [JsonProperty("provider")]
        public string Provider { get; set; } = "auto";

        [JsonProperty("model")]
        public string Model { get; set; } = "auto";

        [JsonProperty("umbral")]
        public decimal Threshold { get; set; } = 0.5m;
    }

    public class ExtractionSettings
    {
        [JsonProperty("model")]
        public string Model { get; set; } = "auto";

        [JsonProperty("umbral")]
        public decimal Threshold { get; set; } = 0.80m;
    }

    public class DocumentInfo
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("content")]
        public DocumentContent Content { get; set; }
    }

    public class DocumentContent
    {
        [JsonProperty("base64")]
        public string Base64 { get; set; }
    }

    public class Traceability
    {
        [JsonProperty("correlationId")]
        public string CorrelationId { get; set; }

        [JsonProperty("submittedBy")]
        public string SubmittedBy { get; set; }

        [JsonProperty("idGDC")]
        public string IdGdc { get; set; }

        [JsonProperty("idActivo")]
        public string IdActivo { get; set; }
    }

    public class ProcessingResponse
    {
        [JsonProperty("instanceId")]
        public string InstanceId { get; set; }

        [JsonProperty("statusQueryUri")]
        public string StatusQueryUri { get; set; }

        [JsonProperty("correlationId")]
        public string CorrelationId { get; set; }
    }

    public class ProcessingStatus
    {
        [JsonProperty("runtimeStatus")]
        public string RuntimeStatus { get; set; }

        [JsonProperty("customStatus")]
        public CustomStatus CustomStatus { get; set; }

        [JsonProperty("output")]
        public object Output { get; set; }

        [JsonProperty("createdTime")]
        public string CreatedTime { get; set; }

        [JsonProperty("lastUpdatedTime")]
        public string LastUpdatedTime { get; set; }
    }

    public class CustomStatus
    {
        [JsonProperty("actividadActual")]
        public string CurrentActivity { get; set; }

        [JsonProperty("ActividadActual")]
        public string CurrentActivityAlt { get; set; }

        [JsonProperty("actividadesCompletadas")]
        public List<string> CompletedActivities { get; set; }

        [JsonProperty("ActividadesCompletadas")]
        public List<string> CompletedActivitiesAlt { get; set; }

        [JsonProperty("actividades")]
        public List<ActivityTimeline> ActivityTimeline { get; set; }

        [JsonProperty("Actividades")]
        public List<ActivityTimeline> ActivityTimelineAlt { get; set; }
    }

    public class ActivityTimeline
    {
        [JsonProperty("nombre")]
        public string Name { get; set; }

        [JsonProperty("Nombre")]
        public string NameAlt { get; set; }

        [JsonProperty("estado")]
        public string State { get; set; }

        [JsonProperty("Estado")]
        public string StateAlt { get; set; }

        [JsonProperty("duracionMs")]
        public long? DurationMs { get; set; }

        [JsonProperty("DuracionMs")]
        public long? DurationMsAlt { get; set; }

        public string GetName() => !string.IsNullOrEmpty(Name) ? Name : NameAlt;
        public string GetState() => !string.IsNullOrEmpty(State) ? State : StateAlt;
        public long? GetDuration() => DurationMs.HasValue ? DurationMs : DurationMsAlt;
    }
}
