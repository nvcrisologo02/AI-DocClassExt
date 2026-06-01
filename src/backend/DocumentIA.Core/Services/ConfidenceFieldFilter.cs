using DocumentIA.Core.Configuration;

namespace DocumentIA.Core.Services;

public static class ConfidenceFieldFilter
{
    public static HashSet<string> GetAvoidConfidenceFields(TipologiaValidationConfig tipologiaConfig)
    {
        return tipologiaConfig.Fields
            .Where(f => f.AvoidConfidence && !string.IsNullOrWhiteSpace(f.Name))
            .Select(f => f.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public static List<double?>? FilterFieldConfidences(
        IReadOnlyDictionary<string, double>? confidenceMap,
        ISet<string> avoidConfidenceFields)
    {
        if (confidenceMap is null)
        {
            return null;
        }

        var filtered = confidenceMap
            .Where(kvp => !avoidConfidenceFields.Contains(kvp.Key))
            .Select(kvp => (double?)kvp.Value)
            .ToList();

        return filtered.Count > 0 ? filtered : null;
    }

    public static Dictionary<string, double> FilterConfidenceMap(
        IReadOnlyDictionary<string, double>? confidenceMap,
        ISet<string> avoidConfidenceFields)
    {
        if (confidenceMap is null)
        {
            return new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        }

        return confidenceMap
            .Where(kvp => !avoidConfidenceFields.Contains(kvp.Key))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);
    }

    public static List<string> GetLowConfidenceFields(
        IReadOnlyDictionary<string, double> confidenceMap,
        double threshold,
        ISet<string> avoidConfidenceFields)
    {
        return confidenceMap
            .Where(kvp => kvp.Value < threshold && !avoidConfidenceFields.Contains(kvp.Key))
            .Select(kvp => kvp.Key)
            .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static List<string> ToSortedList(ISet<string> fields)
    {
        return fields
            .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
