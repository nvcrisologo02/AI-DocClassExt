namespace DocumentIA.Core.Configuration;

/// <summary>
/// Configuración de la sección de prompt libre de una tipología.
/// Permite ejecutar un prompt personalizado contra un LLM y devolver
/// el resultado como texto libre en la salida del procesamiento.
/// </summary>
public class PromptConfig
{
    /// <summary>Habilita la ejecución del prompt para esta tipología.</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Clave del modelo LLM a usar, referenciada en config/prompt/models.json.
    /// Ejemplo: "default.gpt4o-mini"
    /// Si coincide con el DeploymentName del fallback de extracción, la ejecución se optimiza
    /// realizando una única llamada al LLM que extrae campos y ejecuta el prompt a la vez.
    /// </summary>
    public string ModelKey { get; set; } = string.Empty;

    /// <summary>Prompt de sistema que define el rol y comportamiento del modelo.</summary>
    public string SystemPrompt { get; set; } = string.Empty;

    /// <summary>
    /// Plantilla del prompt de usuario. Soporta los siguientes placeholders:
    ///   {contenido}          — markdown extraído del documento (o el documento completo en modo vision)
    ///   {campo:NombreCampo}  — valor de un campo ya extraído (ej. {campo:FincaRegistral})
    /// Los placeholders {campo:X} solo se resuelven cuando la extracción se ejecutó antes del prompt.
    /// </summary>
    public string UserPromptTemplate { get; set; } = string.Empty;

    /// <summary>Tokens máximos de la respuesta del modelo.</summary>
    public int MaxTokens { get; set; } = 2000;

    /// <summary>Temperatura del modelo. 0.0 = completamente determinista.</summary>
    public double Temperature { get; set; } = 0.0;

    /// <summary>
    /// Modo de paso del contenido al LLM cuando no hay markdown previo disponible.
    /// "markdown" — usa el markdown extraído por un paso de extracción previo.
    /// "vision"   — envía el PDF directamente como imagen al modelo multimodal.
    /// </summary>
    public string ContentMode { get; set; } = "markdown";
}

public class PromptDefaultsSettings
{
    public string ModelKey { get; set; } = string.Empty;
    public string SystemPrompt { get; set; } = string.Empty;
    public string UserPromptTemplate { get; set; } = string.Empty;
    public int MaxTokens { get; set; } = 2000;
    public double Temperature { get; set; } = 0.0;
    public string ContentMode { get; set; } = "markdown";

    public PromptConfig ToPromptConfig() => new()
    {
        Enabled = true,
        ModelKey = ModelKey,
        SystemPrompt = SystemPrompt,
        UserPromptTemplate = UserPromptTemplate,
        MaxTokens = MaxTokens,
        Temperature = Temperature,
        ContentMode = ContentMode
    };
}
