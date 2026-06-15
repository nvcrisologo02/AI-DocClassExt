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
    private readonly TipologiaConfigLoader _tipologiaConfigLoader;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ClassificationRoutingSettings _routingSettings;
    private readonly PromptDefaultsSettings _promptDefaults;
    private readonly ClassificationPromptsSettings _promptSettings;
    private readonly IClassificationPromptProvider _promptProvider;
    private readonly ILogger<GptClasificarDataProvider> _logger;
    private readonly PromptTraceTelemetryService _promptTraceTelemetry;
    private readonly Lazy<ClassificationModelConfig> _fallbackModel;

    public GptClasificarDataProvider(
        ClassificationModelRegistryLoader modelRegistryLoader,
        ClassificationTipologiaPromptBuilder tipologiaPromptBuilder,
        TipologiaConfigLoader tipologiaConfigLoader,
        IServiceScopeFactory scopeFactory,
        IOptions<ClassificationRoutingSettings> routingSettings,
        IOptions<PromptDefaultsSettings> promptDefaults,
        IOptions<ClassificationPromptsSettings> promptSettings,
        IClassificationPromptProvider promptProvider,
        PromptTraceTelemetryService promptTraceTelemetry,
        ILogger<GptClasificarDataProvider> logger)
    {
        _modelRegistryLoader = modelRegistryLoader;
        _tipologiaPromptBuilder = tipologiaPromptBuilder;
        _tipologiaConfigLoader = tipologiaConfigLoader;
        _scopeFactory = scopeFactory;
        _routingSettings = routingSettings.Value;
        _promptDefaults = promptDefaults.Value;
        _promptSettings = promptSettings.Value;
        _promptProvider = promptProvider;
        _promptTraceTelemetry = promptTraceTelemetry;
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

        // Obtener prompts configurables desde BD/cache/fallback
        var promptSet = await _promptProvider.GetPromptSetAsync(cancellationToken);
        _logger.LogInformation(
            "Prompts de clasificación resueltos desde {Source} (versión: {Version})",
            promptSet.Source,
            promptSet.Version);

        var phase1ResponseInstruction = resumenPrompt is null
            ? ClassificationTipologiaPromptBuilder.Phase1ResponseFormatInstruction
            : "Responde exclusivamente en JSON válido con esta estructura: {\"tdn1\": \"CODIGO_TDN1\" | null, \"propuesta\": \"texto libre\", \"resumen\": \"resumen ejecutivo\", \"confianza\": 0.0-1.0}. El campo 'confianza' debe ser un número entre 0.0 (ninguna certeza) y 1.0 (certeza absoluta) que refleje tu nivel de confianza en la clasificación. No incluyas texto fuera del JSON.";

        // Construir prompt Phase 1 desde configuración
        var phase1Catalog = _tipologiaPromptBuilder.BuildTdn1Catalog();
        var phase1SystemText = promptSet.Phase1SystemPrompt;
        var phase1UserText = promptSet.Phase1UserPrompt
            .Replace("{CONTEXT_PROMPT}", contextoPrompt)
            .Replace("{TDN1_CATALOG}", phase1Catalog)
            .Replace("{DOCUMENT_TEXT}", contextoTexto ?? string.Empty);

        if (resumenPrompt is not null && !string.IsNullOrWhiteSpace(resumenPrompt.SystemPrompt))
        {
            phase1SystemText += $"\n\nINSTRUCCIÓN ADICIONAL PARA 'resumen':\n{resumenPrompt.SystemPrompt}";
        }

        if (resumenPrompt is not null)
        {
            phase1UserText += $"\n\nInstrucción adicional para devolver en resumen:\n{resumenPrompt.UserPromptTemplate}";
        }

        if (string.IsNullOrWhiteSpace(contextoTexto))
        {
            _logger.LogWarning(
                "No hay contexto textual preprocesado para el fallback de clasificación en {Documento}. Se continuará con contexto mínimo.",
                input.Entrada.Documento.Name);

            phase1UserText +=
                $"\n\nNo hay contenido textual disponible para este fallback. " +
                $"Nombre de archivo: {input.Entrada.Documento.Name}.";
        }

        // Log prompts finales si está habilitado
        if (_promptSettings.EnableFullPromptLogging)
        {
            _logger.LogInformation(
                "[Classification] FULL FINAL PROMPT (Phase1.System, {Length} chars):\n{Content}",
                phase1SystemText.Length,
                phase1SystemText);
            _logger.LogInformation(
                "[Classification] FULL FINAL PROMPT (Phase1.User, {Length} chars):\n{Content}",
                phase1UserText.Length,
                phase1UserText);
        }

        var phase1ResponseText = await CompleteChatAsync(
            model,
            "classification.phase1",
            input.Entrada.Instrucciones.ExpectedType,
            phase1SystemText,
            phase1UserText,
            cancellationToken,
            resumenPrompt?.MaxTokens);

        var phase1Parsed = GptHierarchicalClassificationParser.ParsePhase1(phase1ResponseText);
        if (!phase1Parsed.Success || phase1Parsed.Value is null)
        {
            stopwatch.Stop();
            return BuildUnclassifiedResult(model, phase1Parsed.ErrorReason ?? GptHierarchicalClassificationParser.Phase1ParsingErrorReason, string.Empty);
        }

        // Guardar resumen de Phase 1 para reutilizar (no se regenera en Phase 2)
        var resumenPhase1 = phase1Parsed.Value.Resumen;
        
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
            var confianzaPhase1 = phase1Parsed.Value.Confianza ?? 0.9;
            _logger.LogInformation(
                "Clasificación GPT Fase 1 completada. TDN1={Tdn1}, ConfianzaSelfReported={ConfianzaSelfReported}, ConfianzaFinal={ConfianzaFinal}",
                tdn1Code,
                phase1Parsed.Value.Confianza.HasValue ? phase1Parsed.Value.Confianza.Value.ToString("F3") : "<no reportada>",
                confianzaPhase1.ToString("F3"));
            return new ResultadoClasificacion
            {
                Modelo = model.DeploymentName,
                ProveedorClasif = "GPT4oMini",
                TipologiaDetectada = tdn1Code,
                Confianza = confianzaPhase1,
                ConfianzaGPT = confianzaPhase1,
                ClasificacionParcial = true,
                PropuestaTipologia = propuesta,
                ResumenCombinado = phase1Parsed.Value.Resumen
            };
        }

        var phase2Catalog = _tipologiaPromptBuilder.BuildTdn2CatalogByFamilia(tdn1Code);
        if (string.IsNullOrWhiteSpace(phase2Catalog))
        {
            stopwatch.Stop();
            return BuildVirtualResult(
                model,
                tipologiaDetectada: tdn1Code,
                propuesta: tdn1Code);
        }

        // Phase 2: NO incluir instrucción de resumen
        // El resumen ya fue generado en Phase 1 (phase1Parsed.Value.Resumen)
        // Phase 2 solo debe refinar la clasificación TDN2 dentro de la familia conocida
        
        // Construir prompt Phase 2 desde configuración
        var phase2SystemText = promptSet.Phase2SystemPrompt;
        var phase2UserText = promptSet.Phase2UserPrompt
            .Replace("{TDN1_CODE}", tdn1Code)
            .Replace("{TDN2_CATALOG}", phase2Catalog)
            .Replace("{DOCUMENT_TEXT}", contextoTexto ?? string.Empty);

        // Log prompts finales si está habilitado
        if (_promptSettings.EnableFullPromptLogging)
        {
            _logger.LogInformation(
                "[Classification] FULL FINAL PROMPT (Phase2.System, {Length} chars):\n{Content}",
                phase2SystemText.Length,
                phase2SystemText);
            _logger.LogInformation(
                "[Classification] FULL FINAL PROMPT (Phase2.User, {Length} chars):\n{Content}",
                phase2UserText.Length,
                phase2UserText);
        }

        var phase2ResponseText = await CompleteChatAsync(
            model,
            "classification.phase2",
            input.Entrada.Instrucciones.ExpectedType,
            phase2SystemText,
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
            var tipologiaVirtual = BuildVirtualTipologiaDetectada(propuesta, tdn1Code, phase2Parsed.Value.Tdn2);
            var justificacionVirtual = BuildVirtualJustificacion(propuesta, tipologiaVirtual, phase2Parsed.Value.Tdn2, tdn1Code);
            return BuildVirtualResult(
                model,
                tipologiaDetectada: tipologiaVirtual,
                propuesta: justificacionVirtual);
        }

        stopwatch.Stop();

        var confianzaPhase2 = phase2Parsed.Value.Confianza ?? 0.9;
        _logger.LogInformation(
            "Clasificación GPT Fase 2 completada. Tipologia={Tipologia}, ConfianzaSelfReported={ConfianzaSelfReported}, ConfianzaFinal={ConfianzaFinal}",
            tipologiaCode,
            phase2Parsed.Value.Confianza.HasValue ? phase2Parsed.Value.Confianza.Value.ToString("F3") : "<no reportada>",
            confianzaPhase2.ToString("F3"));

        return new ResultadoClasificacion
        {
            Modelo = model.DeploymentName,
            TipologiaDetectada = tipologiaCode,
            Confianza = confianzaPhase2,
            ConfianzaGPT = confianzaPhase2,
            ProveedorClasif = "GPT4oMini",
            PropuestaTipologia = propuesta,
            ResultadoPromptCombinado = phase2Parsed.Value.ResultadoPrompt,
            ResumenCombinado = resumenPhase1  // Usar resumen de Phase 1 (no se regenera en Phase 2)
        };
    }

    private PromptConfig? ResolveResumenPrompt(ClasificacionInput input, string? contextoTexto)
    {
        if (!input.GenerarResumenPorDefecto)
        {
            return null;
        }

        // Intentar cargar prompts de resumen desde BD primero
        var dbResumenPrompt = TryLoadResumenPromptFromDatabase().GetAwaiter().GetResult();
        if (dbResumenPrompt is not null)
        {
            return new PromptConfig
            {
                Enabled = true,
                ModelKey = dbResumenPrompt.ModelKey,
                SystemPrompt = dbResumenPrompt.SystemPrompt,
                UserPromptTemplate = OpenAIPromptDataProvider.InterpolateTemplate(
                    dbResumenPrompt.UserPromptTemplate,
                    contextoTexto ?? string.Empty,
                    input.DatosNormalizados),
                MaxTokens = dbResumenPrompt.MaxTokens,
                Temperature = dbResumenPrompt.Temperature,
                ContentMode = dbResumenPrompt.ContentMode
            };
        }

        // Fallback: Intentar cargar desde tipología o PromptDefaults
        var defaults = _promptDefaults.ToPromptConfig();
        var tipologiaPrompt = TryLoadTipologiaPromptConfig(input);
        var effectivePrompt = tipologiaPrompt is null
            ? defaults
            : OpenAIPromptDataProvider.ResolvePromptConfig(
                tipologiaPrompt,
                requestPrompt: null,
                defaultPromptConfig: defaults);

        if (effectivePrompt is null || string.IsNullOrWhiteSpace(effectivePrompt.UserPromptTemplate))
        {
            return null;
        }

        return new PromptConfig
        {
            Enabled = true,
            ModelKey = effectivePrompt.ModelKey,
            SystemPrompt = effectivePrompt.SystemPrompt,
            UserPromptTemplate = OpenAIPromptDataProvider.InterpolateTemplate(
                effectivePrompt.UserPromptTemplate,
                contextoTexto ?? string.Empty,
                input.DatosNormalizados),
            MaxTokens = effectivePrompt.MaxTokens,
            Temperature = effectivePrompt.Temperature,
            ContentMode = effectivePrompt.ContentMode
        };
    }

    private async Task<PromptConfig?> TryLoadResumenPromptFromDatabase()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IPromptTemplateRepository>();

            // Cargar prompts de resumen desde BD
            var systemPrompt = await repository.GetActivePromptAsync("summary.system", CancellationToken.None);
            var userPrompt = await repository.GetActivePromptAsync("summary.user", CancellationToken.None);

            if (systemPrompt is null || userPrompt is null)
            {
                _logger.LogDebug(
                    "[ResumenPrompt] Summary prompts not found in database. SummarySystem={SExists}, SummaryUser={UExists}.",
                    systemPrompt is not null,
                    userPrompt is not null);
                return null;
            }

            _logger.LogInformation(
                "[ResumenPrompt] Summary prompts loaded from database. Version={Version}",
                systemPrompt.Version);

            return new PromptConfig
            {
                Enabled = true,
                ModelKey = _promptDefaults.ModelKey ?? "default.gpt4o-mini",
                SystemPrompt = systemPrompt.Content,
                UserPromptTemplate = userPrompt.Content,
                MaxTokens = _promptDefaults.MaxTokens,
                Temperature = _promptDefaults.Temperature,
                ContentMode = _promptDefaults.ContentMode
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[ResumenPrompt] Error loading summary prompts from database. Fallback to appsettings.");
            return null;
        }
    }

    private PromptConfig? TryLoadTipologiaPromptConfig(ClasificacionInput input)
    {
        var tipologiaHint = input.Entrada.Instrucciones.ExpectedType;
        if (string.IsNullOrWhiteSpace(tipologiaHint))
        {
            return null;
        }

        try
        {
            var tipologiaConfig = _tipologiaConfigLoader.LoadConfig(tipologiaHint.Trim());
            return tipologiaConfig.PromptConfig;
        }
        catch (FileNotFoundException ex)
        {
            _logger.LogWarning(
                ex,
                "No se encontró configuración publicada para tipología {Tipologia}. Se usarán PromptDefaults para resumen.",
                tipologiaHint);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Error cargando PromptConfig de tipología {Tipologia}. Se usarán PromptDefaults para resumen.",
                tipologiaHint);
            return null;
        }
    }

    private async Task<string> CompleteChatAsync(
        ClassificationModelConfig model,
        string operation,
        string? tipologia,
        string systemText,
        string userText,
        CancellationToken cancellationToken,
        int? maxOutputTokens = null)
    {
        _promptTraceTelemetry.TrackPrompt(
            provider: "gpt-classification",
            operation: operation,
            tipologia: tipologia ?? string.Empty,
            modelKey: model.Key,
            deployment: model.DeploymentName,
            systemPrompt: systemText,
            userPrompt: userText);

        var systemMessage = new SystemChatMessage(systemText);
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

    private static ResultadoClasificacion BuildVirtualResult(ClassificationModelConfig model, string tipologiaDetectada, string propuesta)
    {
        return new ResultadoClasificacion
        {
            Modelo = model.DeploymentName,
            ProveedorClasif = "GPT4oMini",
            TipologiaDetectada = tipologiaDetectada,
            Confianza = 0.1,
            ConfianzaGPT = 0.1,
            ClasificacionParcial = true,
            FallbackRazon = "Tipologia Virtual",
            PropuestaTipologia = propuesta
        };
    }

    private static string BuildVirtualTipologiaDetectada(string? propuesta, string tdn1Code, string tdn2Code)
    {
        if (!string.IsNullOrWhiteSpace(propuesta))
        {
            var propuestaNormalizada = propuesta.Trim();
            var separador = propuestaNormalizada.IndexOf(':');
            if (separador > 0)
            {
                var codigo = propuestaNormalizada[..separador].Trim();
                if (!string.IsNullOrWhiteSpace(codigo))
                {
                    return codigo;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(tdn2Code))
        {
            return tdn2Code.Trim().ToUpperInvariant();
        }

        return tdn1Code.Trim().ToUpperInvariant();
    }

    private static string BuildVirtualJustificacion(string? propuesta, string tipologiaVirtual, string tdn2Code, string tdn1Code)
    {
        if (!string.IsNullOrWhiteSpace(propuesta))
        {
            var propuestaNormalizada = propuesta.Trim();
            var separador = propuestaNormalizada.IndexOf(':');
            if (separador >= 0 && separador < propuestaNormalizada.Length - 1)
            {
                var justificacion = propuestaNormalizada[(separador + 1)..].Trim();
                if (!string.IsNullOrWhiteSpace(justificacion))
                {
                    return justificacion;
                }
            }
            return propuestaNormalizada;
        }

        return $"GPT propone {tipologiaVirtual} porque el TDN2 '{tdn2Code}' de la familia '{tdn1Code}' no tiene mapeo a tipologia publicada en BBDD.";
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
