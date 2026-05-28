using System.Diagnostics;
using System.Text.Json;
using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using DocumentIA.Core.Configuration;
using DocumentIA.Core.Models;
using DocumentIA.Core.Services;
using DocumentIA.Data.Repositories;
using DocumentIA.Functions.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace DocumentIA.Functions.Services;

/// <summary>
/// Proveedor de clasificación basado en un deployment configurable de Azure OpenAI.
/// Se usa como fallback cuando Azure Document Intelligence falla o devuelve confianza insuficiente.
/// </summary>
public class GptClasificarDataProvider : IClasificarDataProvider
{
    private readonly ClassificationModelRegistryLoader _modelRegistryLoader;
    private readonly ClassificationTipologiaPromptBuilder _tipologiaPromptBuilder;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ClassificationRoutingSettings _routingSettings;
    private readonly PromptDefaultsSettings _promptDefaults;
    private readonly ILogger<GptClasificarDataProvider> _logger;
    private readonly Lazy<ClassificationModelConfig> _fallbackModel;

    public GptClasificarDataProvider(
        ClassificationModelRegistryLoader modelRegistryLoader,
        ClassificationTipologiaPromptBuilder tipologiaPromptBuilder,
        IServiceScopeFactory scopeFactory,
        IOptions<ClassificationRoutingSettings> routingSettings,
        IOptions<PromptDefaultsSettings> promptDefaults,
        ILogger<GptClasificarDataProvider> logger)
    {
        _modelRegistryLoader = modelRegistryLoader;
        _tipologiaPromptBuilder = tipologiaPromptBuilder;
        _scopeFactory = scopeFactory;
        _routingSettings = routingSettings.Value;
        _promptDefaults = promptDefaults.Value;
        _logger = logger;
        _fallbackModel = new Lazy<ClassificationModelConfig>(ResolveFallbackModel);
        // _tipologiasPromptSection se elimina: el IMemoryCache de ClassificationTipologiaPromptBuilder
        // ya cachea el prompt 5 min. Un Lazy<string> lo fijaría para siempre en el singleton.
    }

    public async Task<ResultadoClasificacion> ClasificarAsync(
        ClasificacionInput input,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var requestedModel = input.Entrada.Instrucciones.Classification.Model;
        var model = ResolveModel(requestedModel);
        _logger.LogInformation(
            "Iniciando clasificación Azure OpenAI para documento {Documento}. RequestedModel={RequestedModel}, ResolvedModelKey={ResolvedModelKey}, Deployment={DeploymentName}",
            input.Entrada.Documento.Name,
            string.IsNullOrWhiteSpace(requestedModel) ? "<empty>" : requestedModel,
            model.Key,
            model.DeploymentName);

        var nivelClasificacion = ClassificationLevelResolver.Resolve(
            input.Entrada.Instrucciones.Classification.NivelClasificacion,
            _routingSettings.NivelClasificacionDefault);

        var contextoTexto = ObtenerContextoTexto(input.DatosNormalizados);
        var resumenPrompt = ResolveResumenPrompt(input, contextoTexto);
        var contextoPrompt = BuildInstructionPromptContext(input.Entrada.Instrucciones.Prompt);

        var phase1ResponseInstruction = resumenPrompt is null
            ? ClassificationTipologiaPromptBuilder.Phase1ResponseFormatInstruction
            : "Responde exclusivamente en JSON válido con esta estructura: {\"tdn1\": \"CODIGO_TDN1\" | null, \"propuesta\": \"texto libre\", \"resumen\": \"resumen ejecutivo\"}. No incluyas texto fuera del JSON.";

        var phase1SystemText =
            "Eres un sistema experto en clasificación de documentos del sector inmobiliario español, " +
            "especialmente documentos de SAREB (Sociedad de Gestión de Activos procedentes de la Reestructuración Bancaria). " +
            "Analiza el documento adjunto y clasifícalo en una familia TDN1. " +
            phase1ResponseInstruction;

        if (resumenPrompt is not null && !string.IsNullOrWhiteSpace(resumenPrompt.SystemPrompt))
        {
            phase1SystemText += $"\n\nINSTRUCCIÓN ADICIONAL PARA 'resumen':\n{resumenPrompt.SystemPrompt}";
        }

        var phase1SystemMessage = new SystemChatMessage(phase1SystemText);

        var phase1Catalog = _tipologiaPromptBuilder.BuildTdn1Catalog();
        var phase1UserText =
            $"Prompt adicional de instrucciones (si aplica):\n{contextoPrompt}\n\n" +
            $"Familias TDN1 disponibles:\n{phase1Catalog}\n\n" +
            "Si no puedes resolver una familia, devuelve tdn1=null y completa propuesta con una sugerencia no vinculante.";

        if (resumenPrompt is not null)
        {
            phase1UserText += $"\n\nInstrucción adicional para devolver en resumen:\n{resumenPrompt.UserPromptTemplate}";
        }

        if (!string.IsNullOrWhiteSpace(contextoTexto))
        {
            phase1UserText += $"\n\nCONTENIDO DEL DOCUMENTO (texto/markdown):\n{contextoTexto}";
        }
        else
        {
            _logger.LogWarning(
                "No hay contexto textual preprocesado para el fallback de clasificación en {Documento}. Se continuará con contexto mínimo.",
                input.Entrada.Documento.Name);

            phase1UserText +=
                $"\n\nNo hay contenido textual disponible para este fallback. " +
                $"Nombre de archivo: {input.Entrada.Documento.Name}.";
        }

        var phase1ResponseText = await CompleteChatAsync(
            model,
            phase1SystemMessage,
            phase1UserText,
            cancellationToken,
            resumenPrompt?.MaxTokens);

        var phase1Parsed = GptHierarchicalClassificationParser.ParsePhase1(phase1ResponseText);
        if (!phase1Parsed.Success || phase1Parsed.Value is null)
        {
            stopwatch.Stop();
            return BuildUnclassifiedResult(model, phase1Parsed.ErrorReason ?? GptHierarchicalClassificationParser.Phase1ParsingErrorReason, string.Empty);
        }

        var propuesta = phase1Parsed.Value.Propuesta;
        if (string.IsNullOrWhiteSpace(phase1Parsed.Value.Tdn1))
        {
            stopwatch.Stop();
            // GPT no pudo mapear a ningún código del catálogo.
            // Si aportó una propuesta libre (tipología virtual), devolver ClasificacionParcial=true
            // para que el orquestador exponga la sugerencia en lugar de fallar con "Desconocido".
            if (!string.IsNullOrWhiteSpace(propuesta))
            {
                return new ResultadoClasificacion
                {
                    Modelo = model.DeploymentName,
                    ProveedorClasif = "GPT4oMini",
                    TipologiaDetectada = "Desconocido",
                    Confianza = 0.1,
                    ConfianzaGPT = 0.1,
                    ClasificacionParcial = true,
                    FallbackRazon = "tdn1_virtual_propuesta",
                    PropuestaTipologia = propuesta
                };
            }
            return BuildUnclassifiedResult(model, "tdn1_no_resuelto", propuesta);
        }

        var tdn1Code = phase1Parsed.Value.Tdn1!;
        if (string.Equals(nivelClasificacion, ClassificationLevelResolver.LevelTdn1, StringComparison.OrdinalIgnoreCase))
        {
            stopwatch.Stop();
            return new ResultadoClasificacion
            {
                Modelo = model.DeploymentName,
                ProveedorClasif = "GPT4oMini",
                TipologiaDetectada = tdn1Code,
                Confianza = 0.9,
                ConfianzaGPT = 0.9,
                ClasificacionParcial = true,
                PropuestaTipologia = propuesta,
                ResumenCombinado = phase1Parsed.Value.Resumen
            };
        }

        var phase2Catalog = _tipologiaPromptBuilder.BuildTdn2CatalogByFamilia(tdn1Code);
        if (string.IsNullOrWhiteSpace(phase2Catalog))
        {
            throw new InvalidOperationException($"No se encontraron subtipos TDN2 para la familia {tdn1Code}.");
        }

        var phase2ResponseInstruction = resumenPrompt is null
            ? ClassificationTipologiaPromptBuilder.Phase2ResponseFormatInstruction
            : "Responde exclusivamente en JSON válido con esta estructura: {\"tdn2\": \"CODIGO_TDN2\", \"resumen\": \"resumen ejecutivo\"}. No incluyas texto fuera del JSON.";

        var phase2SystemText =
            "Eres un sistema experto en clasificación documental. " +
            "Debes seleccionar exclusivamente un subtipo TDN2 de la familia ya resuelta. " +
            phase2ResponseInstruction;

        if (resumenPrompt is not null && !string.IsNullOrWhiteSpace(resumenPrompt.SystemPrompt))
        {
            phase2SystemText += $"\n\nINSTRUCCIÓN ADICIONAL PARA 'resumen':\n{resumenPrompt.SystemPrompt}";
        }

        var phase2SystemMessage = new SystemChatMessage(phase2SystemText);

        var phase2UserText =
            $"Familia TDN1 resuelta: {tdn1Code}\n\n" +
            $"Subtipos TDN2 disponibles:\n{phase2Catalog}";

        if (resumenPrompt is not null)
        {
            phase2UserText += $"\n\nInstrucción adicional para devolver en resumen:\n{resumenPrompt.UserPromptTemplate}";
        }

        if (!string.IsNullOrWhiteSpace(contextoTexto))
        {
            phase2UserText += $"\n\nCONTENIDO DEL DOCUMENTO (texto/markdown):\n{contextoTexto}";
        }

        var phase2ResponseText = await CompleteChatAsync(
            model,
            phase2SystemMessage,
            phase2UserText,
            cancellationToken,
            resumenPrompt?.MaxTokens);
        var phase2Parsed = GptHierarchicalClassificationParser.ParsePhase2(phase2ResponseText);

        if (!phase2Parsed.Success || phase2Parsed.Value is null)
        {
            stopwatch.Stop();
            return BuildUnclassifiedResult(model, phase2Parsed.ErrorReason ?? GptHierarchicalClassificationParser.Phase2ParsingErrorReason, propuesta);
        }

        var tipologiaCode = ResolveTipologiaByTdn2(phase2Parsed.Value.Tdn2);
        if (string.IsNullOrWhiteSpace(tipologiaCode))
        {
            stopwatch.Stop();
            return BuildUnclassifiedResult(model, "tdn2_sin_tipologia_asociada", propuesta);
        }

        stopwatch.Stop();

        return new ResultadoClasificacion
        {
            Modelo = model.DeploymentName,
            TipologiaDetectada = tipologiaCode,
            Confianza = 0.9,
            ConfianzaGPT = 0.9,
            ProveedorClasif = "GPT4oMini",
            PropuestaTipologia = propuesta,
            ResultadoPromptCombinado = phase2Parsed.Value.ResultadoPrompt,
            ResumenCombinado = phase2Parsed.Value.Resumen
        };
    }

    private PromptConfig? ResolveResumenPrompt(ClasificacionInput input, string? contextoTexto)
    {
        if (!input.GenerarResumenPorDefecto)
        {
            return null;
        }

        var defaults = _promptDefaults.ToPromptConfig();
        if (string.IsNullOrWhiteSpace(defaults.UserPromptTemplate))
        {
            return null;
        }

        return new PromptConfig
        {
            Enabled = true,
            ModelKey = defaults.ModelKey,
            SystemPrompt = defaults.SystemPrompt,
            UserPromptTemplate = OpenAIPromptDataProvider.InterpolateTemplate(
                defaults.UserPromptTemplate,
                contextoTexto ?? string.Empty,
                input.DatosNormalizados),
            MaxTokens = defaults.MaxTokens,
            Temperature = defaults.Temperature,
            ContentMode = defaults.ContentMode
        };
    }

    private async Task<string> CompleteChatAsync(
        ClassificationModelConfig model,
        SystemChatMessage systemMessage,
        string userText,
        CancellationToken cancellationToken,
        int? maxOutputTokens = null)
    {
        var userMessage = new UserChatMessage(ChatMessageContentPart.CreateTextPart(userText));

        var options = new ChatCompletionOptions
        {
            ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat(),
            Temperature = (float)model.Temperature,
            MaxOutputTokenCount = Math.Max(model.MaxTokens, maxOutputTokens ?? model.MaxTokens)
        };

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, model.TimeoutSeconds)));

        var chatClient = CreateChatClient(model);
        var response = await chatClient.CompleteChatAsync(
            new List<ChatMessage> { systemMessage, userMessage },
            options,
            cts.Token);

        return response.Value.Content[0].Text;
    }

    private string? ResolveTipologiaByTdn2(string tdn2Code)
    {
        if (string.IsNullOrWhiteSpace(tdn2Code))
        {
            return null;
        }

        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITipologiaRepository>();
        var tipologias = repository.GetAllPublishedAsync().GetAwaiter().GetResult();

        foreach (var tipologia in tipologias)
        {
            if (string.IsNullOrWhiteSpace(tipologia.ConfiguracionJson))
            {
                continue;
            }

            try
            {
                var config = JsonSerializer.Deserialize<TipologiaValidationConfig>(tipologia.ConfiguracionJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (config is null)
                {
                    continue;
                }

                if (string.Equals(config.ResolvedTdn2, tdn2Code, StringComparison.OrdinalIgnoreCase))
                {
                    return string.IsNullOrWhiteSpace(tipologia.Codigo) ? config.TipologiaId : tipologia.Codigo;
                }
            }
            catch
            {
                // Ignorar tipologías malformadas y continuar la búsqueda.
            }
        }

        return null;
    }

    private static ClassificationModelConfig ValidateGptModel(ClassificationModelConfig model)
    {
        if (!IsAzureOpenAiProvider(model.Provider))
        {
            throw new InvalidOperationException(
                $"El modelo de clasificación '{model.Key}' no es compatible con GPT/Azure OpenAI. Provider actual: '{model.Provider}'.");
        }

        if (string.IsNullOrWhiteSpace(model.Endpoint))
        {
            throw new InvalidOperationException(
                $"ClassificationModelConfig.Endpoint es obligatorio para el modelo '{model.Key}'.");
        }

        if (string.IsNullOrWhiteSpace(model.DeploymentName))
        {
            throw new InvalidOperationException(
                $"ClassificationModelConfig.DeploymentName es obligatorio para el modelo '{model.Key}'.");
        }

        if (!string.Equals(model.AuthMode, "DefaultAzureCredential", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(model.ApiKey))
        {
            throw new InvalidOperationException(
                $"ClassificationModelConfig.ApiKey es obligatorio para el modelo '{model.Key}' cuando AuthMode=ApiKey.");
        }

        return model;
    }

    private static string BuildInstructionPromptContext(PromptInstrucciones? prompt)
    {
        if (prompt is null)
        {
            return "(sin prompt adicional)";
        }

        var fragments = new List<string>();

        if (!string.IsNullOrWhiteSpace(prompt.SystemPrompt))
        {
            fragments.Add($"SystemPrompt: {prompt.SystemPrompt.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(prompt.UserPromptTemplate))
        {
            fragments.Add($"UserPromptTemplate: {prompt.UserPromptTemplate.Trim()}");
        }

        return fragments.Count == 0
            ? "(sin prompt adicional)"
            : string.Join("\n", fragments);
    }

    private static ResultadoClasificacion BuildUnclassifiedResult(ClassificationModelConfig model, string reason, string propuesta)
    {
        return new ResultadoClasificacion
        {
            Modelo = model.DeploymentName,
            ProveedorClasif = "GPT4oMini",
            TipologiaDetectada = "Desconocido",
            Confianza = 0.0,
            ConfianzaGPT = 0.0,
            FallbackRazon = reason,
            PropuestaTipologia = propuesta
        };
    }

    private static string? ObtenerContextoTexto(IDictionary<string, object> datosNormalizados)
    {
        if (datosNormalizados is null || datosNormalizados.Count == 0)
        {
            return null;
        }

        var claves = new[]
        {
            "Markdown",
            "markdown",
            "Texto",
            "texto",
            "ContentText",
            "contentText"
        };

        foreach (var clave in claves)
        {
            if (!datosNormalizados.TryGetValue(clave, out var raw) || raw is null)
            {
                continue;
            }

            if (raw is string s && !string.IsNullOrWhiteSpace(s))
            {
                return s;
            }

            if (raw is JsonElement json && json.ValueKind == JsonValueKind.String)
            {
                var value = json.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }

        return null;
    }

    private ClassificationModelConfig ResolveModel(string? requestedModel)
    {
        if (string.IsNullOrWhiteSpace(requestedModel) || string.Equals(requestedModel, "auto", StringComparison.OrdinalIgnoreCase))
        {
            return _fallbackModel.Value;
        }

        return ValidateGptModel(_modelRegistryLoader.GetModel(requestedModel));
    }

    private ChatClient CreateChatClient(ClassificationModelConfig model)
    {
        AzureOpenAIClient azureClient;

        if (string.Equals(model.AuthMode, "DefaultAzureCredential", StringComparison.OrdinalIgnoreCase))
        {
            azureClient = new AzureOpenAIClient(new Uri(model.Endpoint), new DefaultAzureCredential());
        }
        else
        {
            azureClient = new AzureOpenAIClient(
                new Uri(model.Endpoint),
                new AzureKeyCredential(model.ApiKey));
        }

        return azureClient.GetChatClient(model.DeploymentName);
    }

    private ClassificationModelConfig ResolveFallbackModel()
    {
        var model = _modelRegistryLoader.GetFallbackModel();
        return ValidateGptModel(model);
    }

    private static bool IsAzureOpenAiProvider(string provider) =>
        provider.ToLowerInvariant() is "azure-openai" or "gpt" or "openai";
}
