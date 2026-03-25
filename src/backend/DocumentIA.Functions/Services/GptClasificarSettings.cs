namespace DocumentIA.Functions.Services;

public class GptClasificarSettings
{
    /// <summary>Endpoint de Azure OpenAI (sin /openai/...). Ejemplo: https://xxx.openai.azure.com/</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>Api Key. Vacío si AuthMode = DefaultAzureCredential.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>ApiKey | DefaultAzureCredential</summary>
    public string AuthMode { get; set; } = "ApiKey";

    /// <summary>Nombre del deployment en Azure OpenAI. Se toma siempre de configuración.</summary>
    public string DeploymentName { get; set; } = string.Empty;

    /// <summary>
    /// Si false (valor por defecto), el fallback GPT está completamente desactivado.
    /// Poner a true explícitamente en la configuración para activarlo.
    /// Rollback instantáneo: volver a false.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Umbral interno de confianza para activar el fallback GPT.
    /// Si Azure Document Intelligence devuelve Confianza menor que este valor, se llama a GPT.
    /// Rango [0.0-1.0]. 0.0 = solo fallback en excepción. 1.0 = siempre fallback.
    /// </summary>
    public double FallbackThreshold { get; set; } = 0.6;

    /// <summary>Temperatura del modelo GPT. 0.0 = máxima determinismo.</summary>
    public double Temperature { get; set; } = 0.0;

    /// <summary>Máximo de tokens en la respuesta GPT.</summary>
    public int MaxTokens { get; set; } = 150;

    /// <summary>Timeout en segundos para la llamada a Azure OpenAI.</summary>
    public int TimeoutSeconds { get; set; } = 30;
}
