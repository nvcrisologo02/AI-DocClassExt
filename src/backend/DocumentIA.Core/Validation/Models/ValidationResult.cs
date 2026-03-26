namespace DocumentIA.Core.Validation.Models
{
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public ValidationSeverity Severity { get; set; }
        public string Message { get; set; } = string.Empty;
        public string FieldName { get; set; } = string.Empty;
        public string SuggestionString { get; set; } = string.Empty;
        public Dictionary<string, object?> Metadata { get; set; } = new Dictionary<string, object?>();

        public ValidationResult()
        {
            Metadata = new Dictionary<string, object?>();
        }
    }

    public enum ValidationSeverity
    {
        Info,
        Warning,
        Error
    }

    public class ValidationReport
    {
        public bool IsValid { get; set; }
        public List<ValidationResult> Results { get; set; }
        public int ErrorCount { get; set; }
        public int WarningCount { get; set; }
        public int InfoCount { get; set; }
        /// <summary>Total de reglas evaluadas (incluye las que pasaron).</summary>
        public int TotalChecked { get; set; }

        public ValidationReport()
        {
            Results = new List<ValidationResult>();
            // Sin resultados (sin errores) el reporte es valido por defecto.
            IsValid = true;
        }

        public void AddResult(ValidationResult result)
        {
            Results.Add(result);
            
            switch (result.Severity)
            {
                case ValidationSeverity.Error:
                    ErrorCount++;
                    break;
                case ValidationSeverity.Warning:
                    WarningCount++;
                    break;
                case ValidationSeverity.Info:
                    InfoCount++;
                    break;
            }

            // Si hay al menos un error, el reporte no es valido
            IsValid = ErrorCount == 0;
        }
    }
}
