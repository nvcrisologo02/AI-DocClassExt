using DocumentIA.Core.Configuration;
using DocumentIA.Core.Models;
using DocumentIA.Functions.Services;
using DocumentIA.Functions.Orchestrators;
using FluentAssertions;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DocumentIA.Tests.Unit.Orchestrators;

/// <summary>
/// Implementacion fake de TaskOrchestrationContext para pruebas unitarias del orquestador.
/// Permite inyectar resultados o excepciones por nombre de activity.
/// </summary>
internal sealed class FakeTaskOrchestrationContext : TaskOrchestrationContext
{
    private readonly ContratoEntrada? _input;
    private readonly Dictionary<string, object?> _activityResults = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Exception> _activityThrows = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<object?>> _activityInputs = new(StringComparer.Ordinal);

    public FakeTaskOrchestrationContext(ContratoEntrada? input = null)
    {
        _input = input;
    }

    public void SetupActivity<T>(string activityName, T result)
        => _activityResults[activityName] = result;

    public void SetupActivityThrow(string activityName, Exception ex)
        => _activityThrows[activityName] = ex;

    public T? GetLastActivityInput<T>(string activityName)
    {
        if (!_activityInputs.TryGetValue(activityName, out var inputs) || inputs.Count == 0)
        {
            return default;
        }

        return (T?)inputs[^1];
    }

    public int GetActivityCallCount(string activityName)
        => _activityInputs.TryGetValue(activityName, out var inputs) ? inputs.Count : 0;

    public override TaskName Name => new TaskName("DocumentProcessOrchestrator");
    public override string InstanceId => "fake-instance-001";
    public override DateTime CurrentUtcDateTime => new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    public override bool IsReplaying => false;

    public override T? GetInput<T>() where T : default => (T?)(object?)_input;

    public override Task<TResult> CallActivityAsync<TResult>(TaskName name, object? input = null, TaskOptions? options = null)
    {
        if (!_activityInputs.TryGetValue(name.Name, out var inputs))
        {
            inputs = new List<object?>();
            _activityInputs[name.Name] = inputs;
        }

        inputs.Add(input);

        if (_activityThrows.TryGetValue(name.Name, out var ex))
        {
            throw ex;
        }
        if (_activityResults.TryGetValue(name.Name, out var result))
            return Task.FromResult((TResult)result!);
        return Task.FromResult(default(TResult)!);
    }

    public override Task CreateTimer(DateTime fireAt, CancellationToken cancellationToken)
        => Task.Delay(Timeout.Infinite, cancellationToken);

    public override void SetCustomStatus(object? customStatus) { }

    public override ILogger CreateReplaySafeLogger(string categoryName)
        => NullLogger.Instance;

    public override Task CallActivityAsync(TaskName name, object? input = null, TaskOptions? options = null)
    {
        if (!_activityInputs.TryGetValue(name.Name, out var inputs))
        {
            inputs = new List<object?>();
            _activityInputs[name.Name] = inputs;
        }

        inputs.Add(input);

        if (_activityThrows.TryGetValue(name.Name, out var ex))
        {
            throw ex;
        }
        return Task.CompletedTask;
    }

    protected override ILoggerFactory LoggerFactory => NullLoggerFactory.Instance;
    public override Guid NewGuid() => Guid.NewGuid();
    public override ParentOrchestrationInstance? Parent => null;

    public override Task<TResult> CallSubOrchestratorAsync<TResult>(TaskName orchestratorName, object? input = null, TaskOptions? options = null)
        => throw new NotImplementedException();

    public override void SendEvent(string instanceId, string eventName, object? payload = null)
        => throw new NotImplementedException();

    public override void ContinueAsNew(object? newInput = null, bool preserveUnprocessedEvents = true)
        => throw new NotImplementedException();

    public override Task<T> WaitForExternalEvent<T>(string eventName, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}

public class DocumentProcessOrchestratorTests
{
    private static string? _extractionRegistryPath;

    private static string EnsureExtractionRegistryPath()
    {
        if (!string.IsNullOrWhiteSpace(_extractionRegistryPath) && File.Exists(_extractionRegistryPath))
        {
            return _extractionRegistryPath;
        }

        _extractionRegistryPath = Path.Combine(Path.GetTempPath(), "documentia-tests-extraction.models.json");
        var registryJson =
            "{\"models\":[{\"key\":\"default.cu\",\"provider\":\"azure-content-understanding\",\"isDefault\":true,\"endpoint\":\"https://example.cu.azure.com\",\"apiKey\":\"test\",\"authMode\":\"ApiKey\",\"analyzerId\":\"analyzer-default\",\"processingLocation\":\"global\",\"minFieldsRatio\":0.5}]}";
        File.WriteAllText(_extractionRegistryPath, registryJson);

        return _extractionRegistryPath;
    }

    private static DocumentProcessOrchestrator CreateOrchestrator(ClassificationPreparationSettings? settings = null)
    {
        var loader = new ExtractionModelRegistryLoader(EnsureExtractionRegistryPath());
        var configuredSettings = settings ?? new ClassificationPreparationSettings();
        return new DocumentProcessOrchestrator(loader, Options.Create(configuredSettings), Options.Create(new PipelineSettings()));
    }

    private static ContratoEntrada BuildEntrada(
        string nombre = "test.pdf",
        string? objectIdGdc = null,
        string? expectedType = null,
        bool skipDuplicateCheck = false,
        bool classificationOnly = false,
        bool? executeIntegrarWhenClassificationOnly = null,
        int maxPagesForClassificationOnly = 0,
        bool forzarResumenPorDefecto = false)
        => new()
        {
            Documento = new Documento
            {
                Name = nombre,
                ObjectIdGDC = objectIdGdc,
                Content = new ContenidoDocumento { Base64 = "dGVzdA==" }
            },
            Instrucciones = new Instrucciones
            {
                ExpectedType = expectedType ?? string.Empty,
                ClassificationOnly = classificationOnly,
                ExecuteIntegrarWhenClassificationOnly = executeIntegrarWhenClassificationOnly,
                MaxPagesForClassificationOnly = maxPagesForClassificationOnly,
                ForzarResumenPorDefecto = forzarResumenPorDefecto,
                SkipDuplicateCheck = skipDuplicateCheck,
                SkipGDCUpload = true
            },
            Trazabilidad = new Trazabilidad
            {
                CorrelationId = "test-corr-001",
                SubmittedBy = "test-user"
            }
        };

    private static Dictionary<string, object> BuildNormalizarResult() => new()
    {
        ["SHA256"] = "sha256abc",
        ["MD5"] = "md5abc",
        ["CRC32"] = "crc32abc",
        ["Paginas"] = 1
    };

    private static Dictionary<string, object> BuildNormalizarResultConMarkdown() => new()
    {
        ["SHA256"] = "sha256abc",
        ["MD5"] = "md5abc",
        ["CRC32"] = "crc32abc",
        ["Paginas"] = 1,
        ["Markdown"] = "# markdown normalizado"
    };

    private static ResultadoClasificacion BuildClasificacionOk(string tipologia = "nota.simple") => new()
    {
        Modelo = "gpt-4o-mini",
        Confianza = 0.95,
        ConfianzaGPT = 0.95,
        ProveedorClasif = "GPT4oMini",
        TipologiaDetectada = tipologia,
        ContentExtraido = "# markdown clasificacion"
    };

    private static global::DocumentIA.Core.Models.ExtraccionResultado BuildExtraccionOk() => new()
    {
        Modelo = "gpt-4o-mini",
        DatosExtraidos = new Dictionary<string, object>
        {
            ["IDUFIR"] = "IDUFIR-123",
            ["ReferenciaCatastral"] = "REFCAT-123",
            ["Titular"] = "Titular de prueba"
        },
        ResumenCombinado = "Resumen de prueba"
    };

    private static DetalleValidacion BuildValidacionOk() => new()
    {
        TotalReglas = 0,
        ReglasAplicadas = 0,
        Errores = 0,
        Warnings = 0,
        Validaciones = new List<ItemValidacion>(),
        ConfianzaValidacion = 0.98
    };

    private static ResultadoAssetResolver BuildActivoOk() => new()
    {
        Ejecutado = true,
        Exitoso = true,
        Count = 1,
        Activos = new List<ActivoEncontrado>
        {
            new ActivoEncontrado
            {
                IdActivo = "ACT-1",
                CamposSolicitados = new Dictionary<string, object?>
                {
                    ["DES_SERVICER"] = "HipoGes",
                    ["IMP_PT"] = 125000m
                }
            }
        }
    };

    private static ResolvedTipologia BuildTipologia(
        bool extractionEnabled = false,
        bool skipGdc = true,
        bool assetResolverEnabled = false,
        bool promptEnabled = false,
        bool promptHasDefinition = false,
        string tdn1 = "",
        string tdn2 = "",
        // "nota.simple" es una tipología real del catálogo que resuelve. Desde AB#100242 el valor de
        // isDefault ya no decide si ExpectedType se conserva (lo decide el centinela "Desconocido"),
        // así que aquí solo modela si la tipología es la versión por defecto de su familia.
        bool isDefault = false,
        string extractionProvider = "")
        => new(
            RequestedValue: "nota.simple",
            TipologiaId: "nota.simple",
            Version: "1.0",
            TechnicalKey: "nota.simple.1_0",
            IsDefault: isDefault,
            ExtractionEnabled: extractionEnabled,
            SkipGDCUpload: skipGdc,
            PromptEnabled: promptEnabled,
            AssetResolverEnabled: assetResolverEnabled,
            PromptHasDefinition: promptHasDefinition,
            ExtractionProvider: extractionProvider,
            Tdn1: tdn1,
            Tdn2: tdn2);

    [Fact]
    public void BuildObtenerActivoInput_WithInstructionOverrides_PrioritizesRequestValues()
    {
        var entrada = new ContratoEntrada
        {
            Instrucciones = new Instrucciones
            {
                AssetResolver = new AssetResolverInstrucciones
                {
                    CamposBusqueda = new CamposBusquedaActivo
                    {
                        // CamposBusqueda debe indicar el nombre del campo en DatosExtraidos
                        Idufir = "IDUFIR",
                        ReferenciaCatastral = "ReferenciaCatastral"
                    },
                    CamposSolicitados = new List<string> { "DES_SERVICER", "IMP_PT" }
                }
            },
            Trazabilidad = new Trazabilidad { CorrelationId = "corr-001" }
        };

        var salida = new ContratoSalida
        {
            Identificacion = new Identificacion { Tipologia = "nota.simple.1_4" },
            DatosExtraidos = new Dictionary<string, object>
            {
                ["IDUFIR"] = "from-extract",
                ["ReferenciaCatastral"] = "from-extract-ref"
            }
        };

        var tipologia = new ResolvedTipologia(
            RequestedValue: "nota.simple@1.4",
            TipologiaId: "nota.simple",
            Version: "1.4",
            TechnicalKey: "nota.simple.1_4",
            IsDefault: true,
            AssetResolverEnabled: true,
            AssetResolverCamposSolicitados: new List<string> { "DES_TIPO_AAII" },
            AssetResolverModoCombinacionCriterios: "AND",
            AssetResolverMapeoIdufir: new List<string> { "IDUFIR", "IDUFIR_CRU" },
            AssetResolverMapeoReferenciaCatastral: new List<string> { "ReferenciaCatastral" },
            AssetResolverBusquedaIdufirHabilitada: true,
            AssetResolverBusquedaReferenciaCatastralHabilitada: true,
            AssetResolverBusquedaDireccionHabilitada: true,
            AssetResolverMapeoDireccionCompleta: new List<string> { "Localizacion" },
            AssetResolverMapeoDireccionNombreVia: new List<string> { "Via" },
            AssetResolverMapeoDireccionNumero: new List<string> { "Numero" },
            AssetResolverMapeoDireccionMunicipio: new List<string> { "Municipio" },
            AssetResolverMapeoDireccionCodigoPostal: new List<string> { "CodigoPostal" },
            AssetResolverUmbralScoreDireccion: 0.8);

        var input = DocumentProcessOrchestrator.BuildObtenerActivoInput(entrada, salida, tipologia);

        input.CorrelationId.Should().Be("corr-001");
        input.Tipologia.Should().Be("nota.simple.1_4");
        input.IdufirOverride.Should().Be("from-extract");
        input.ReferenciaCatastralOverride.Should().Be("from-extract-ref");
        input.CamposSolicitados.Should().BeEquivalentTo(new[] { "DES_SERVICER", "IMP_PT" });
        input.ModoCombinacionCriterios.Should().Be("AND");
        input.DatosExtraidos.Should().ContainKey("IDUFIR");
    }

    [Fact]
    public void BuildObtenerActivoInput_WithoutInstructionOverrides_UsesTipologiaValuesAndSafeDefaults()
    {
        var entrada = new ContratoEntrada
        {
            Instrucciones = new Instrucciones { AssetResolver = null },
            Trazabilidad = new Trazabilidad { CorrelationId = "corr-002" }
        };

        var salida = new ContratoSalida
        {
            Identificacion = new Identificacion { Tipologia = "nota.simple.1_3" },
            DatosExtraidos = new Dictionary<string, object>()
        };

        var tipologia = new ResolvedTipologia(
            RequestedValue: "nota.simple@1.3",
            TipologiaId: "nota.simple",
            Version: "1.3",
            TechnicalKey: "nota.simple.1_3",
            IsDefault: true,
            AssetResolverEnabled: true,
            AssetResolverCamposSolicitados: new List<string> { "DES_SERVICER", "DES_TIPO_AAII" },
            AssetResolverModoCombinacionCriterios: "OR",
            AssetResolverMapeoIdufir: new List<string> { "IDUFIR" },
            AssetResolverMapeoReferenciaCatastral: new List<string> { "ReferenciaCatastral" },
            AssetResolverBusquedaIdufirHabilitada: false,
            AssetResolverBusquedaReferenciaCatastralHabilitada: true,
            AssetResolverBusquedaDireccionHabilitada: true,
            AssetResolverMapeoDireccionCompleta: null,
            AssetResolverMapeoDireccionNombreVia: null,
            AssetResolverMapeoDireccionNumero: null,
            AssetResolverMapeoDireccionMunicipio: null,
            AssetResolverMapeoDireccionCodigoPostal: null,
            AssetResolverUmbralScoreDireccion: 0.75);

        var input = DocumentProcessOrchestrator.BuildObtenerActivoInput(entrada, salida, tipologia);

        input.CamposSolicitados.Should().BeEquivalentTo(new[] { "DES_SERVICER", "DES_TIPO_AAII" });
        input.IdufirOverride.Should().BeNull();
        input.ReferenciaCatastralOverride.Should().BeNull();
        input.MapeoIdufir.Should().BeEquivalentTo(new[] { "IDUFIR" });
        input.MapeoReferenciaCatastral.Should().BeEquivalentTo(new[] { "ReferenciaCatastral" });
        input.BusquedaIdufirHabilitada.Should().BeFalse();
        input.BusquedaReferenciaCatastralHabilitada.Should().BeTrue();
        input.BusquedaDireccionHabilitada.Should().BeTrue();
        input.MapeoDireccionCompleta.Should().NotBeNull().And.BeEmpty();
        input.MapeoDireccionNombreVia.Should().NotBeNull().And.BeEmpty();
        input.MapeoDireccionNumero.Should().NotBeNull().And.BeEmpty();
        input.MapeoDireccionMunicipio.Should().NotBeNull().And.BeEmpty();
        input.MapeoDireccionCodigoPostal.Should().NotBeNull().And.BeEmpty();
        input.UmbralScoreDireccion.Should().Be(0.75);
    }

    [Fact]
    public void BuildObtenerActivoInput_PropagaMapeoColeccionActivos()
    {
        var entrada = new ContratoEntrada
        {
            Instrucciones = new Instrucciones { AssetResolver = null },
            Trazabilidad = new Trazabilidad { CorrelationId = "corr-003" }
        };

        var salida = new ContratoSalida
        {
            Identificacion = new Identificacion { Tipologia = "tasa.basura.1_0" },
            DatosExtraidos = new Dictionary<string, object>()
        };

        var tipologia = new ResolvedTipologia(
            RequestedValue: "tasa.basura@1.0",
            TipologiaId: "tasa.basura",
            Version: "1.0",
            TechnicalKey: "tasa.basura.1_0",
            IsDefault: true,
            AssetResolverEnabled: true,
            AssetResolverMapeoReferenciaCatastral: new List<string> { "ReferenciaCastatral" },
            AssetResolverMapeoColeccionActivos: new List<string> { "DireccionPropiedades" });

        var input = DocumentProcessOrchestrator.BuildObtenerActivoInput(entrada, salida, tipologia);

        input.MapeoColeccionActivos.Should().BeEquivalentTo(new[] { "DireccionPropiedades" });
    }

    [Fact]
    public void BuildObtenerActivoInput_SinMapeoColeccionActivos_DevuelveListaVacia()
    {
        var entrada = new ContratoEntrada
        {
            Instrucciones = new Instrucciones { AssetResolver = null },
            Trazabilidad = new Trazabilidad { CorrelationId = "corr-004" }
        };

        var salida = new ContratoSalida
        {
            Identificacion = new Identificacion { Tipologia = "nota.simple.1_0" },
            DatosExtraidos = new Dictionary<string, object>()
        };

        var tipologia = new ResolvedTipologia(
            RequestedValue: "nota.simple@1.0",
            TipologiaId: "nota.simple",
            Version: "1.0",
            TechnicalKey: "nota.simple.1_0",
            IsDefault: true,
            AssetResolverEnabled: true);

        var input = DocumentProcessOrchestrator.BuildObtenerActivoInput(entrada, salida, tipologia);

        input.MapeoColeccionActivos.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public async Task RunOrchestrator_DuplicadoDetectado_RetornaSalidaReutilizada()
    {
        var orchestrator = CreateOrchestrator();
        var context = new FakeTaskOrchestrationContext(BuildEntrada(classificationOnly: true));

        var salidaPrevia = new ContratoSalida
        {
            Resultado = new ResultadoFinal { Estado = "OK" }
        };
        context.SetupActivity("NormalizarActivity", BuildNormalizarResult());
        context.SetupActivity("VerificarDuplicadoActivity", true);
        context.SetupActivity("ObtenerUltimaEjecucionDuplicadoActivity", salidaPrevia);

        var salida = await orchestrator.RunOrchestrator(context);

        salida.Resultado.ReutilizadaPorDuplicado.Should().BeTrue();
        salida.Resultado.MensajeReutilizacion.Should().NotBeNullOrWhiteSpace();

        var duplicadoInput = context.GetLastActivityInput<ObtenerUltimaEjecucionDuplicadoInput>("ObtenerUltimaEjecucionDuplicadoActivity");
        duplicadoInput.Should().NotBeNull();
        duplicadoInput!.ClassificationOnly.Should().BeTrue();
    }

    [Fact]
    public async Task RunOrchestrator_DuplicadoSinHistorialReutilizable_RetornaEstadoDuplicado()
    {
        var orchestrator = CreateOrchestrator();
        var entrada = BuildEntrada(expectedType: "nota.simple");
        var context = new FakeTaskOrchestrationContext(entrada);

        context.SetupActivity("NormalizarActivity", BuildNormalizarResult());
        context.SetupActivity("VerificarDuplicadoActivity", true);
        context.SetupActivity<ContratoSalida?>("ObtenerUltimaEjecucionDuplicadoActivity", null);
        var salida = await orchestrator.RunOrchestrator(context);

        salida.Resultado.Estado.Should().Be("DUPLICADO");
        salida.Resultado.ReutilizadaPorDuplicado.Should().BeTrue();
        context.GetLastActivityInput<object>("SubirBlobActivity").Should().BeNull();
        context.GetLastActivityInput<object>("ResolverTipologiaActivity").Should().BeNull();
    }

    [Fact]
    public async Task RunOrchestrator_ClasificarFallaConTipologiaNoIdentificada_RetornaEstadoNoClasificado()
    {
        var orchestrator = CreateOrchestrator();
        var context = new FakeTaskOrchestrationContext(BuildEntrada());

        context.SetupActivity("NormalizarActivity", BuildNormalizarResult());
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "container/test.pdf");
        context.SetupActivityThrow("ClasificarActivity", new Exception("No se ha podido identificar la tipologia del documento"));

        var salida = await orchestrator.RunOrchestrator(context);

        salida.Resultado.Estado.Should().Be("NO_CLASIFICADO");
        salida.Resultado.MensajeError.Should().Contain("no clasificable");
    }

    [Fact]
    public async Task RunOrchestrator_ClasificarRateLimitExcedido_RetornaEstadoPendienteReintento()
    {
        var orchestrator = CreateOrchestrator();
        var context = new FakeTaskOrchestrationContext(BuildEntrada());

        context.SetupActivity("NormalizarActivity", BuildNormalizarResult());
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "container/test.pdf");
        context.SetupActivity("ClasificarActivity", new ResultadoClasificacion
        {
            RateLimitExcedido = true,
            FallbackRazon = "rate_limit_exhausted",
            TipologiaDetectada = "Desconocido",
            Confianza = 0
        });

        var salida = await orchestrator.RunOrchestrator(context);

        salida.Resultado.Estado.Should().Be("PENDIENTE_REINTENTO");
        salida.Resultado.EstadoCalidad.Should().Be("ERROR");
        salida.Resultado.MensajeError.Should().Contain("429");
        salida.Resultado.ConfianzaGlobal.Should().Be(0);
        salida.Resultado.ConfianzaClasificacion.Should().Be(0);
        salida.DetalleEjecucion.Seguimiento.Estado.Should().Be("PendienteReintento");
        salida.DetalleEjecucion.Clasificacion.RateLimitExcedido.Should().BeTrue();

        context.GetLastActivityInput<object>("ResolverTipologiaActivity").Should().BeNull();
        context.GetLastActivityInput<object>("ExtraerActivity").Should().BeNull();
        context.GetActivityCallCount("PersistirActivity").Should().Be(0);
    }

    [Fact]
    public async Task RunOrchestrator_ClasificacionUsaDocumentoPreparado_PropagaOverrideYMetadata()
    {
        var orchestrator = CreateOrchestrator();
        var context = new FakeTaskOrchestrationContext(BuildEntrada());

        context.SetupActivity("NormalizarActivity", BuildNormalizarResult());
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "container/test.pdf");
        context.SetupActivity("PrepararDocumentoClasificacionActivity", new PrepararDocumentoClasificacionResultado
        {
            DocumentoBase64Clasif = "cmVjb3J0YWRv",
            TotalPaginas = 8,
            CharsTextoNativo = 1234,
            PaginasIncluidas = 3,
            RecorteAplicado = true
        });
        context.SetupActivity("ClasificarActivity", new ResultadoClasificacion
        {
            Modelo = "di-test",
            Confianza = 0.1,
            TipologiaDetectada = "nota.simple"
        });
        context.SetupActivity("ResolverTipologiaActivity", BuildTipologia());

        var salida = await orchestrator.RunOrchestrator(context);

        salida.Resultado.Estado.Should().Be("BAJA_CONFIANZA_CLASIFICACION");

        var clasifInput = context.GetLastActivityInput<ClasificacionInput>("ClasificarActivity");
        clasifInput.Should().NotBeNull();
        clasifInput!.DocumentoBase64Override.Should().Be("cmVjb3J0YWRv");
        clasifInput.CharsTextoNativo.Should().Be(1234);
        clasifInput.TotalPaginas.Should().Be(8);
    }

    [Fact]
    public async Task RunOrchestrator_ResolveMaxPaginas_PriorizaOverrideTipologia()
    {
        var settings = new ClassificationPreparationSettings
        {
            Enabled = true,
            MaxPaginasClasificacionDefault = 3,
            OverridesPorFamilia = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["sere"] = 5
            },
            OverridesPorTipologia = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["sere.nota.simple"] = 7
            }
        };

        var orchestrator = CreateOrchestrator(settings);
        var context = new FakeTaskOrchestrationContext(BuildEntrada(expectedType: "sere.nota.simple"));

        context.SetupActivity("NormalizarActivity", BuildNormalizarResult());
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "container/test.pdf");
        context.SetupActivity("PrepararDocumentoClasificacionActivity", new PrepararDocumentoClasificacionResultado
        {
            DocumentoBase64Clasif = "dGVzdA==",
            TotalPaginas = 2,
            CharsTextoNativo = 10,
            PaginasIncluidas = 2,
            RecorteAplicado = false
        });
        context.SetupActivityThrow("ResolverTipologiaActivity", new KeyNotFoundException("No existe la tipologia"));

        var salida = await orchestrator.RunOrchestrator(context);

        salida.Resultado.Estado.Should().Be("NO_CLASIFICADO");

        var prepInput = context.GetLastActivityInput<PrepararDocumentoClasificacionInput>("PrepararDocumentoClasificacionActivity");
        prepInput.Should().NotBeNull();
        prepInput!.MaxPaginasClasificacion.Should().Be(7);
    }

    [Fact]
    public async Task RunOrchestrator_TipologiaNoResuelta_RetornaEstadoNoClasificado()
    {
        var orchestrator = CreateOrchestrator();
        var context = new FakeTaskOrchestrationContext(BuildEntrada(expectedType: "nota.simple"));

        context.SetupActivity("NormalizarActivity", BuildNormalizarResult());
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "container/test.pdf");
        context.SetupActivityThrow("ResolverTipologiaActivity", new KeyNotFoundException("No existe la tipologia: nota.simple"));

        var salida = await orchestrator.RunOrchestrator(context);

        salida.Resultado.Estado.Should().Be("NO_CLASIFICADO");
        salida.Resultado.MensajeError.Should().Contain("no clasificable");
    }

    [Fact]
    public async Task RunOrchestrator_TipologiaDesconocido_TerminaSinErrorTecnico()
    {
        var orchestrator = CreateOrchestrator();
        var context = new FakeTaskOrchestrationContext(BuildEntrada(expectedType: "Desconocido"));

        context.SetupActivity("NormalizarActivity", BuildNormalizarResult());
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "container/test.pdf");
        context.SetupActivity("ResolverTipologiaActivity", new ResolvedTipologia(
            RequestedValue: "Desconocido",
            TipologiaId: "Desconocido",
            Version: "N/A",
            TechnicalKey: "Desconocido",
            IsDefault: true,
            SkipGDCUpload: true,
            PromptEnabled: false,
            ExtractionEnabled: false));

        var salida = await orchestrator.RunOrchestrator(context);

        salida.Resultado.Estado.Should().Be("NO_CLASIFICADO");
        salida.DetalleEjecucion.Seguimiento.Estado.Should().Be("Completed");
        salida.Identificacion.Tipologia.Should().Be("Desconocido");
    }

    [Fact]
    public async Task RunOrchestrator_BajaConfianzaClasificacion_RetornaEstadoBajaConfianza()
    {
        var orchestrator = CreateOrchestrator();
        var context = new FakeTaskOrchestrationContext(BuildEntrada());

        context.SetupActivity("NormalizarActivity", BuildNormalizarResult());
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "container/test.pdf");
        context.SetupActivity("ClasificarActivity", new ResultadoClasificacion
        {
            Modelo = "di-test",
            Confianza = 0.1,
            ConfianzaDI = 0.1,
            TipologiaDetectada = "nota.simple"
        });
        context.SetupActivity("ResolverTipologiaActivity", BuildTipologia());

        var salida = await orchestrator.RunOrchestrator(context);

        salida.Resultado.Estado.Should().Be("BAJA_CONFIANZA_CLASIFICACION");
        salida.Resultado.ConfianzaClasificacion.Should().Be(0);
        salida.Resultado.ConfianzaGlobal.Should().Be(0);
        salida.DetalleEjecucion.Seguimiento.Estado.Should().Be("Completed");
        salida.DetalleEjecucion.Seguimiento.Actividades
            .Where(a => a.Nombre is "Extraer" or "Validar" or "ObtenerActivo" or "Integrar" or "SubirGDC" or "Persistir")
            .All(a => a.Estado is "Pending" or "Skipped")
            .Should().BeTrue();
    }

    [Fact]
    public async Task RunOrchestrator_ClasificacionParcial_OmitePipelinePosteriorYPersiste()
    {
        var orchestrator = CreateOrchestrator();
        var context = new FakeTaskOrchestrationContext(BuildEntrada());

        context.SetupActivity("NormalizarActivity", BuildNormalizarResult());
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "container/test.pdf");
        context.SetupActivity("ClasificarActivity", new ResultadoClasificacion
        {
            Modelo = "gpt-4o-mini",
            Confianza = 0.92,
            ConfianzaGPT = 0.92,
            ProveedorClasif = "GPT4oMini",
            TipologiaDetectada = "Desconocido",
            ClasificacionParcial = true,
            PropuestaTipologia = "Nota simple registral"
        });

        var salida = await orchestrator.RunOrchestrator(context);

        salida.Resultado.MensajeError.Should().BeNull();
        salida.Resultado.Estado.Should().Be("OK");
        salida.Identificacion.Tipologia.Should().Be("Desconocido");
        salida.Identificacion.TipologiaFamilia.Should().Be("Desconocido");
        salida.Identificacion.TipologiaVersion.Should().BeEmpty();
        salida.Identificacion.PropuestaTipologia.Should().Be("Nota simple registral");
        salida.DetalleEjecucion.Clasificacion.ClasificacionParcial.Should().BeTrue();
        salida.DetalleEjecucion.Clasificacion.PropuestaTipologia.Should().Be("Nota simple registral");
        salida.DetalleEjecucion.Extraccion.Modelo.Should().Be("skipped");

        context.GetLastActivityInput<object>("ResolverTipologiaActivity").Should().BeNull();
        context.GetLastActivityInput<object>("ExtraerActivity").Should().BeNull();
        context.GetLastActivityInput<object>("ValidarActivity").Should().BeNull();
        context.GetLastActivityInput<object>("ObtenerActivoActivity").Should().BeNull();
        context.GetLastActivityInput<object>("IntegrarActivity").Should().BeNull();
        context.GetLastActivityInput<PersistirInput>("PersistirActivity").Should().NotBeNull();
    }

    [Theory]
    [InlineData("tdn1_no_resuelto")]
    [InlineData("fase1_parsing_error")]
    [InlineData("tdn2_sin_tipologia_asociada")]
    public async Task RunOrchestrator_NoClasificadoConRazonControlada_PreservaPropuestaTipologia(string fallbackRazon)
    {
        var orchestrator = CreateOrchestrator();
        var context = new FakeTaskOrchestrationContext(BuildEntrada());

        context.SetupActivity("NormalizarActivity", BuildNormalizarResult());
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "container/test.pdf");
        context.SetupActivity("ClasificarActivity", new ResultadoClasificacion
        {
            Modelo = "gpt-4o-mini",
            Confianza = 0,
            ConfianzaGPT = 0,
            ProveedorClasif = "GPT4oMini",
            TipologiaDetectada = "Desconocido",
            FallbackLLM = true,
            FallbackRazon = fallbackRazon,
            PropuestaTipologia = "Sugerencia libre de tipologia"
        });
        context.SetupActivityThrow("ResolverTipologiaActivity", new KeyNotFoundException("No existe la tipologia: Desconocido"));

        var salida = await orchestrator.RunOrchestrator(context);

        salida.Resultado.Estado.Should().Be("NO_CLASIFICADO");
        salida.DetalleEjecucion.Clasificacion.FallbackRazon.Should().Be(fallbackRazon);
        salida.DetalleEjecucion.Clasificacion.PropuestaTipologia.Should().Be("Sugerencia libre de tipologia");
        salida.Resultado.MensajeError.Should().Contain("no clasificable");
    }

    [Fact]
    public async Task RunOrchestrator_ObjectIdGdc_SincronizaNombreEnSalidaParaPersistencia()
    {
        var orchestrator = CreateOrchestrator();
        var context = new FakeTaskOrchestrationContext(BuildEntrada(nombre: string.Empty, objectIdGdc: "GDC-123"));

        context.SetupActivity("ObtenerDocumentoGDCActivity", new ObtenerDocumentoGDCResult
        {
            Base64 = "dGVzdA==",
            NombreArchivo = "nota_simple_gdc.pdf"
        });
        context.SetupActivityThrow("NormalizarActivity", new Exception("stop test"));

        var salida = await orchestrator.RunOrchestrator(context);

        salida.Identificacion.Documento.Should().Be("nota_simple_gdc.pdf");
    }

    [Fact]
    public async Task RunOrchestrator_ClassificationOnly_OmitePipelinePosteriorYCompletaTrazas()
    {
        var orchestrator = CreateOrchestrator();
        var context = new FakeTaskOrchestrationContext(BuildEntrada(classificationOnly: true, maxPagesForClassificationOnly: 3));

        context.SetupActivity("NormalizarActivity", BuildNormalizarResult());
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "container/test.pdf");
        context.SetupActivity("PrepararDocumentoClasificacionActivity", new PrepararDocumentoClasificacionResultado
        {
            DocumentoBase64Clasif = "cmVjb3J0YWRv",
            TotalPaginas = 8,
            CharsTextoNativo = 1234,
            PaginasIncluidas = 3,
            RecorteAplicado = true
        });
        context.SetupActivity("ClasificarActivity", new ResultadoClasificacion
        {
            Modelo = "gpt-4o-mini",
            Confianza = 0.91,
            ConfianzaDI = 0.55,
            ConfianzaGPT = 0.91,
            FallbackLLM = true,
            TipologiaDetectada = "nota.simple",
            ContentExtraido = "# markdown"
        });
        context.SetupActivity("ResolverTipologiaActivity", BuildTipologia(extractionEnabled: true));

        var salida = await orchestrator.RunOrchestrator(context);

        salida.Resultado.Estado.Should().Be("OK", $"error real: {salida.Resultado.MensajeError}");
        salida.DetalleEjecucion.ClassificationOnly.Should().BeTrue();
        salida.DetalleEjecucion.RecorteAplicado.Should().BeTrue();
        salida.DetalleEjecucion.PaginasIncluidas.Should().Be(3);
        salida.DetalleEjecucion.MarkdownGenerado.Should().BeTrue();
        salida.DetalleEjecucion.OrigenMarkdown.Should().Be("Clasificacion");
        salida.DetalleEjecucion.ModeloLLMUsado.Should().Be("gpt-4o-mini");
        salida.DetalleEjecucion.Postproceso.Markdown.Should().Be("# markdown");

        context.GetLastActivityInput<object>("ExtraerActivity").Should().BeNull();
        context.GetLastActivityInput<object>("ValidarActivity").Should().BeNull();
        context.GetLastActivityInput<object>("ObtenerActivoActivity").Should().BeNull();

        salida.DetalleEjecucion.Seguimiento.Actividades
            .Where(a => a.Nombre is "Extraer" or "Validar" or "ObtenerActivo" or "Integrar" or "SubirGDC")
            .Select(a => a.Estado)
            .Should().OnlyContain(estado => estado == "Skipped" || estado == "Completed");
    }

    [Fact]
    public async Task RunOrchestrator_ClassificationOnlyConPromptActivo_EjecutaPromptYExponeResultado()
    {
        var orchestrator = CreateOrchestrator();
        var context = new FakeTaskOrchestrationContext(BuildEntrada(classificationOnly: true));

        context.SetupActivity("NormalizarActivity", BuildNormalizarResult());
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "container/test.pdf");
        context.SetupActivity("PrepararDocumentoClasificacionActivity", new PrepararDocumentoClasificacionResultado
        {
            DocumentoBase64Clasif = "cmVjb3J0YWRv",
            TotalPaginas = 2,
            PaginasIncluidas = 2,
            RecorteAplicado = false
        });
        context.SetupActivity("ClasificarActivity", new ResultadoClasificacion
        {
            Modelo = "gpt-4o-mini",
            Confianza = 0.91,
            ConfianzaGPT = 0.91,
            FallbackLLM = true,
            TipologiaDetectada = "nota.simple",
            ContentExtraido = "# markdown classificationOnly"
        });
        context.SetupActivity("ResolverTipologiaActivity", BuildTipologia(promptEnabled: true, promptHasDefinition: true));
        context.SetupActivity("PromptActivity", new PromptResultado
        {
            Modelo = "gpt-4o-mini",
            Resultado = "Resumen ejecutivo"
        });

        var salida = await orchestrator.RunOrchestrator(context);

        salida.Resultado.Estado.Should().Be("OK", $"error real: {salida.Resultado.MensajeError}");
        salida.DatosExtraidos.Should().ContainKey("ResultadoPrompt");
        salida.DatosExtraidos["ResultadoPrompt"].Should().Be("Resumen ejecutivo");
        salida.DetalleEjecucion.Prompt.Should().NotBeNull();
        var promptDetalle = salida.DetalleEjecucion.Prompt!;
        promptDetalle.Modelo.Should().Be("gpt-4o-mini");

        var promptInput = context.GetLastActivityInput<PromptActivityInput>("PromptActivity");
        promptInput.Should().NotBeNull();
        promptInput!.MarkdownExtraido.Should().Be("# markdown classificationOnly");

        salida.DetalleEjecucion.Seguimiento.Actividades
            .Single(a => a.Nombre == "Prompt")
            .Estado.Should().Be("Completed");
    }

    [Fact]
    public async Task RunOrchestrator_ForzarResumenPorDefectoSinGptPrevio_EjecutaPromptActivitySoloResumen()
    {
        var orchestrator = CreateOrchestrator();
        var context = new FakeTaskOrchestrationContext(BuildEntrada(classificationOnly: true, forzarResumenPorDefecto: true));

        context.SetupActivity("NormalizarActivity", BuildNormalizarResult());
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "container/test.pdf");
        context.SetupActivity("PrepararDocumentoClasificacionActivity", new PrepararDocumentoClasificacionResultado
        {
            DocumentoBase64Clasif = "cmVjb3J0YWRv",
            TotalPaginas = 2,
            PaginasIncluidas = 2,
            RecorteAplicado = false
        });
        context.SetupActivity("ClasificarActivity", new ResultadoClasificacion
        {
            Modelo = "di-test",
            Confianza = 0.91,
            ConfianzaDI = 0.91,
            ProveedorClasif = "DocumentIntelligence",
            TipologiaDetectada = "nota.simple",
            ContentExtraido = "# markdown DI"
        });
        context.SetupActivity("ResolverTipologiaActivity", BuildTipologia());
        context.SetupActivity("PromptActivity", new PromptResultado
        {
            Modelo = "gpt-4o-mini",
            Resumen = "Resumen ejecutivo"
        });

        var salida = await orchestrator.RunOrchestrator(context);

        salida.Resultado.Estado.Should().Be("OK", $"error real: {salida.Resultado.MensajeError}");
        salida.DatosExtraidos.Should().ContainKey("Resumen");
        salida.DatosExtraidos["Resumen"].Should().Be("Resumen ejecutivo");
        salida.DatosExtraidos.Should().NotContainKey("ResultadoPrompt");

        var promptInput = context.GetLastActivityInput<PromptActivityInput>("PromptActivity");
        promptInput.Should().NotBeNull();
        promptInput!.ForzarResumenPorDefecto.Should().BeTrue();
        promptInput.ResultadoPromptCombinado.Should().BeNull();
    }

    [Fact]
    public async Task RunOrchestrator_ClassificationOnly_ResumenForzadoSinMarkdown_EjecutaLayoutAntesDePrompt()
    {
        var orchestrator = CreateOrchestrator();
        var context = new FakeTaskOrchestrationContext(BuildEntrada(classificationOnly: true, forzarResumenPorDefecto: true));

        context.SetupActivity("NormalizarActivity", BuildNormalizarResult());
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "container/test.pdf");
        context.SetupActivity("PrepararDocumentoClasificacionActivity", new PrepararDocumentoClasificacionResultado
        {
            DocumentoBase64Clasif = "cmVjb3J0YWRv",
            TotalPaginas = 2,
            PaginasIncluidas = 2,
            RecorteAplicado = false
        });
        context.SetupActivity("ClasificarActivity", new ResultadoClasificacion
        {
            Modelo = "di-test",
            Confianza = 0.91,
            ConfianzaDI = 0.91,
            ProveedorClasif = "DocumentIntelligence",
            TipologiaDetectada = "nota.simple",
            ContentExtraido = null
        });
        context.SetupActivity("ResolverTipologiaActivity", BuildTipologia());
        // El markdown para el resumen lo aporta ahora el resolutor con la necesidad de
        // clasificacion (recorte de 3 paginas), no una llamada directa a DI Layout.
        context.SetupActivity("ObtenerMarkdownActivity", MarkdownResuelto("# markdown layout resumen", 3, completo: false));
        context.SetupActivity("PromptActivity", new PromptResultado
        {
            Modelo = "gpt-4o-mini",
            Resumen = "Resumen ejecutivo"
        });

        var salida = await orchestrator.RunOrchestrator(context);

        salida.Resultado.Estado.Should().Be("OK");
        salida.DatosExtraidos.Should().ContainKey("Resumen");
        salida.DatosExtraidos["Resumen"].Should().Be("Resumen ejecutivo");

        var markdownInput = context.GetLastActivityInput<ObtenerMarkdownInput>("ObtenerMarkdownActivity");
        markdownInput.Should().NotBeNull();
        markdownInput!.Necesidad.DocumentoCompleto.Should().BeFalse();
        markdownInput.Necesidad.PaginasMinimas.Should().Be(3);

        var promptInput = context.GetLastActivityInput<PromptActivityInput>("PromptActivity");
        promptInput.Should().NotBeNull();
        promptInput!.MarkdownExtraido.Should().Be("# markdown layout resumen");
    }

    [Fact]
    public async Task RunOrchestrator_ClassificationOnly_LayoutDedicadoFalla_NoReintentaEnPromptBajoDemanda()
    {
        // Regresión AB#100029: si el layout dedicado de ClassificationOnly (resumen forzado sin
        // markdown) falla, el prompt bajo demanda reintentaba con el mismo input, pagando DI Layout
        // dos veces para fallar dos veces. Tras el fix se intenta una sola vez y se continúa sin
        // markdown hacia PromptActivity. Ahora el corte lo da la guarda de necesidades sin
        // resultado del resolutor (AB#100252).
        var orchestrator = CreateOrchestrator();
        // ExpectedType omite la Clasificación real y el Paso 2.8 (obtención de markdown previa a
        // clasificar), que es una llamada a ObtenerMarkdownActivity independiente del bloque
        // dedicado de ClassificationOnly: así el conteo de la aserción aísla exclusivamente el
        // camino bajo prueba (bloque dedicado + reintento bajo demanda).
        var context = new FakeTaskOrchestrationContext(
            BuildEntrada(classificationOnly: true, forzarResumenPorDefecto: true, expectedType: "nota.simple"));

        context.SetupActivity("NormalizarActivity", BuildNormalizarResult());
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "container/test.pdf");
        context.SetupActivity("PrepararDocumentoClasificacionActivity", new PrepararDocumentoClasificacionResultado
        {
            DocumentoBase64Clasif = "cmVjb3J0YWRv",
            TotalPaginas = 2,
            PaginasIncluidas = 2,
            RecorteAplicado = false
        });
        context.SetupActivity("ResolverTipologiaActivity", BuildTipologia());
        context.SetupActivityThrow("ObtenerMarkdownActivity", new InvalidOperationException("DI Layout no disponible"));
        context.SetupActivity("PromptActivity", new PromptResultado
        {
            Modelo = "gpt-4o-mini",
            Resumen = "Resumen sin markdown"
        });

        var salida = await orchestrator.RunOrchestrator(context);

        salida.Resultado.Estado.Should().Be("OK", $"error real: {salida.Resultado.MensajeError}");
        context.GetActivityCallCount("ObtenerMarkdownActivity").Should().Be(1);

        var promptInput = context.GetLastActivityInput<PromptActivityInput>("PromptActivity");
        promptInput.Should().NotBeNull();
        promptInput!.MarkdownExtraido.Should().BeNull();
    }

    [Fact]
    public async Task RunOrchestrator_ClassificationOnlyNivelTdn1_NoDegradaANoClasificadoCuandoTdn1EsValido()
    {
        var orchestrator = CreateOrchestrator();
        var entrada = BuildEntrada(classificationOnly: true);
        entrada.Instrucciones.Classification.NivelClasificacion = "TDN1";

        var context = new FakeTaskOrchestrationContext(entrada);

        context.SetupActivity("NormalizarActivity", BuildNormalizarResult());
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "container/test.pdf");
        context.SetupActivity("PrepararDocumentoClasificacionActivity", new PrepararDocumentoClasificacionResultado
        {
            DocumentoBase64Clasif = "cmVjb3J0YWRv",
            TotalPaginas = 2,
            PaginasIncluidas = 2,
            RecorteAplicado = false
        });
        context.SetupActivity("ClasificarActivity", new ResultadoClasificacion
        {
            Modelo = "gpt-4o-mini",
            Confianza = 0.9,
            ConfianzaGPT = 0.9,
            ProveedorClasif = "GPT4oMini",
            TipologiaDetectada = "CEDU",
            ClasificacionParcial = true
        });
        context.SetupActivity("PromptActivity", new PromptResultado
        {
            Modelo = "gpt-4o-mini",
            Resumen = "Resumen TDN1"
        });
        context.SetupActivityThrow("ResolverTipologiaActivity", new InvalidOperationException("No deberia llamarse ResolverTipologiaActivity para TDN1 classificationOnly"));

        var salida = await orchestrator.RunOrchestrator(context);

        salida.Identificacion.Tdn1.Should().Be("CEDU");
        salida.Identificacion.Tipologia.Should().Be("CEDU");
        salida.Identificacion.TipologiaFamilia.Should().Be("CEDU");
        salida.DetalleEjecucion.MotivoErrorTipologia.Should().BeNull();

        var resolverInput = context.GetLastActivityInput<string>("ResolverTipologiaActivity");
        resolverInput.Should().BeNull();
        var integrarInput = context.GetLastActivityInput<IntegrarInput>("IntegrarActivity");
        integrarInput.Should().BeNull();
        var subirGdcInput = context.GetLastActivityInput<object>("SubirGDCActivity");
        subirGdcInput.Should().BeNull();
    }

    [Fact]
    public async Task RunOrchestrator_Paso28_SinMarkdown_PideMarkdownAlResolutorYPropagaAlClasificar()
    {
        var orchestrator = CreateOrchestrator();
        var context = new FakeTaskOrchestrationContext(BuildEntrada());

        context.SetupActivity("NormalizarActivity", BuildNormalizarResult());
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "container/test.pdf");
        context.SetupActivity("ObtenerMarkdownActivity", MarkdownResuelto("# Documento de prueba generado por Layout", 3, completo: false));
        context.SetupActivity("ClasificarActivity", new ResultadoClasificacion
        {
            Modelo = "gpt-4o-mini",
            Confianza = 0.9,
            ConfianzaGPT = 0.9,
            ProveedorClasif = "GPT4oMini",
            TipologiaDetectada = "NOTS",
            ClasificacionParcial = true
        });

        await orchestrator.RunOrchestrator(context);

        var markdownInput = context.GetLastActivityInput<ObtenerMarkdownInput>("ObtenerMarkdownActivity");
        markdownInput.Should().NotBeNull();
        markdownInput!.Necesidad.PaginasMinimas.Should().Be(3);

        var clasifInput = context.GetLastActivityInput<ClasificacionInput>("ClasificarActivity");
        clasifInput.Should().NotBeNull();
        clasifInput!.DatosNormalizados.Should().ContainKey("Markdown");
        clasifInput.DatosNormalizados["Markdown"].Should().Be("# Documento de prueba generado por Layout");
    }

    [Fact]
    public async Task RunOrchestrator_Paso28_ResolutorDevuelveMarkdownDeBD_LoUsaParaClasificar()
    {
        // Ya no hay Paso 2.8b: el fallback a BD vive dentro del resolutor. Desde el orquestador
        // solo se ve una resolucion con Fuente=BaseDatos, y la traza debe reflejarla.
        var orchestrator = CreateOrchestrator();
        var context = new FakeTaskOrchestrationContext(BuildEntrada());

        context.SetupActivity("NormalizarActivity", BuildNormalizarResult());
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "container/test.pdf");
        context.SetupActivity(
            "ObtenerMarkdownActivity",
            MarkdownResuelto("# Markdown recuperado de BD", 0, completo: false, FuenteMarkdown.BaseDatos));
        context.SetupActivity("ClasificarActivity", new ResultadoClasificacion
        {
            Modelo = "gpt-4o-mini",
            Confianza = 0.1,
            ConfianzaGPT = 0.1,
            ProveedorClasif = "GPT4oMini",
            TipologiaDetectada = "Desconocido",
            ClasificacionParcial = true
        });

        var salida = await orchestrator.RunOrchestrator(context);

        salida.Resultado.Estado.Should().Be("OK", $"error real: {salida.Resultado.MensajeError}");
        salida.DetalleEjecucion.OrigenMarkdown.Should().Be("LayoutPreClasificacion");
        salida.DetalleEjecucion.MarkdownFuente.Should().Be("BaseDatos");
        salida.DetalleEjecucion.MarkdownGenerado.Should().BeTrue();

        var clasifInput = context.GetLastActivityInput<ClasificacionInput>("ClasificarActivity");
        clasifInput.Should().NotBeNull();
        clasifInput!.DatosNormalizados.Should().ContainKey("Markdown");
        clasifInput.DatosNormalizados["Markdown"].Should().Be("# Markdown recuperado de BD");
    }

    [Fact]
    public async Task RunOrchestrator_Paso28_ResolutorSinMarkdown_ContinuaSinMarkdownComoHoy()
    {
        var orchestrator = CreateOrchestrator();
        var context = new FakeTaskOrchestrationContext(BuildEntrada());

        context.SetupActivity("NormalizarActivity", BuildNormalizarResult());
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "container/test.pdf");
        context.SetupActivity("ObtenerMarkdownActivity", new ResultadoMarkdown { Fuente = FuenteMarkdown.Ninguna });
        context.SetupActivity("ClasificarActivity", new ResultadoClasificacion
        {
            Modelo = "gpt-4o-mini",
            Confianza = 0.1,
            ConfianzaGPT = 0.1,
            ProveedorClasif = "GPT4oMini",
            TipologiaDetectada = "Desconocido",
            ClasificacionParcial = true
        });

        var salida = await orchestrator.RunOrchestrator(context);

        salida.Resultado.Estado.Should().Be("OK", $"error real: {salida.Resultado.MensajeError}");
        salida.DetalleEjecucion.OrigenMarkdown.Should().BeNull();
        salida.DetalleEjecucion.MarkdownGenerado.Should().BeFalse();

        var clasifInput = context.GetLastActivityInput<ClasificacionInput>("ClasificarActivity");
        clasifInput.Should().NotBeNull();
        clasifInput!.DatosNormalizados.Should().NotContainKey("Markdown");
    }

    [Fact]
    public async Task RunOrchestrator_ClasificacionParcial_AsignaTdn1EnIdentificacion()
    {
        var orchestrator = CreateOrchestrator();
        var context = new FakeTaskOrchestrationContext(BuildEntrada());

        context.SetupActivity("NormalizarActivity", BuildNormalizarResult());
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "container/test.pdf");
        context.SetupActivity("ClasificarActivity", new ResultadoClasificacion
        {
            Modelo = "gpt-4o-mini",
            Confianza = 0.9,
            ConfianzaGPT = 0.9,
            ProveedorClasif = "GPT4oMini",
            TipologiaDetectada = "NOTS",
            ClasificacionParcial = true,
            PropuestaTipologia = "Nota simple registral"
        });
        context.SetupActivityThrow("ResolverTipologiaActivity", new KeyNotFoundException("No existe la tipologia: NOTS"));

        var salida = await orchestrator.RunOrchestrator(context);

        salida.Identificacion.Tdn1.Should().Be("NOTS");
        context.GetLastActivityInput<object>("ResolverTipologiaActivity").Should().NotBeNull();
        salida.Resultado.Estado.Should().Be("NO_CLASIFICADO");
    }

    [Fact]
    public async Task RunOrchestrator_TipologiaVirtualParcial_EstadoOkConDesconocidoYTdn1Nulo()
    {
        var orchestrator = CreateOrchestrator();
        var context = new FakeTaskOrchestrationContext(BuildEntrada());

        context.SetupActivity("NormalizarActivity", BuildNormalizarResult());
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "container/test.pdf");
        context.SetupActivity("ClasificarActivity", new ResultadoClasificacion
        {
            Modelo = "gpt-4o-mini",
            Confianza = 0.1,
            ConfianzaGPT = 0.1,
            ProveedorClasif = "GPT4oMini",
            TipologiaDetectada = "Desconocido",
            ClasificacionParcial = true,
            FallbackRazon = "tdn1_virtual_propuesta",
            PropuestaTipologia = "Solicitud de cambio de titularidad"
        });

        var salida = await orchestrator.RunOrchestrator(context);

        salida.Resultado.Estado.Should().Be("OK");
        salida.Identificacion.Tipologia.Should().Be("Desconocido");
        salida.Identificacion.Tdn1.Should().BeNullOrWhiteSpace();
        salida.DetalleEjecucion.Clasificacion.FallbackRazon.Should().Be("tdn1_virtual_propuesta");
        salida.DetalleEjecucion.Clasificacion.PropuestaTipologia.Should().Be("Solicitud de cambio de titularidad");
        context.GetLastActivityInput<object>("ResolverTipologiaActivity").Should().BeNull();
    }

    [Fact]
    public async Task RunOrchestrator_ParcialPorFase2SinTdn2Parseable_EstadoOkConTdn1Virtual()
    {
        var orchestrator = CreateOrchestrator();
        var context = new FakeTaskOrchestrationContext(BuildEntrada());

        context.SetupActivity("NormalizarActivity", BuildNormalizarResultConMarkdown());
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "container/test.pdf");
        context.SetupActivity("ClasificarActivity", new ResultadoClasificacion
        {
            Modelo = "gpt-4o-mini",
            Confianza = 0.72,
            ConfianzaGPT = 0.72,
            ProveedorClasif = "GPT4oMini",
            TipologiaDetectada = "PRES",
            ClasificacionParcial = true,
            FallbackRazon = "fase2_parsing_error",
            PropuestaTipologia = "Presupuesto de adecuación de inmueble",
            ResumenCombinado = "Resumen del presupuesto"
        });

        var salida = await orchestrator.RunOrchestrator(context);

        salida.Resultado.Estado.Should().Be("OK");
        salida.Identificacion.Tipologia.Should().Be("PRES");
        salida.Identificacion.Tdn1.Should().Be("PRES");
        salida.DetalleEjecucion.Clasificacion.FallbackRazon.Should().Be("fase2_parsing_error");
        salida.DatosExtraidos.Should().ContainKey("Resumen");
        // El markdown disponible debe conservarse para que PersistirActivity lo comprima en Documentos
        salida.DetalleEjecucion.Postproceso.Markdown.Should().Be("# markdown normalizado");
        salida.DetalleEjecucion.Postproceso.Normalizaciones.Should().Contain("Markdown");
        context.GetLastActivityInput<object>("ResolverTipologiaActivity").Should().BeNull();
    }

    [Fact]
    public async Task RunOrchestrator_TipologiaVirtualConTdn2Detectado_PersisteTdn2EnIdentificacion()
    {
        var orchestrator = CreateOrchestrator();
        var context = new FakeTaskOrchestrationContext(BuildEntrada());

        context.SetupActivity("NormalizarActivity", BuildNormalizarResult());
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "container/test.pdf");
        context.SetupActivity("ClasificarActivity", new ResultadoClasificacion
        {
            Modelo = "gpt-4o-mini",
            Confianza = 0.9,
            ConfianzaGPT = 0.9,
            ProveedorClasif = "GPT4oMini",
            TipologiaDetectada = "ESIN",
            Tdn2Detectado = "ESIN-40",
            ClasificacionParcial = true,
            FallbackRazon = "Tipologia Virtual",
            PropuestaTipologia = "Informe de solvencia del titular"
        });

        var salida = await orchestrator.RunOrchestrator(context);

        salida.Resultado.Estado.Should().Be("OK");
        salida.Identificacion.Tipologia.Should().Be("ESIN");
        salida.Identificacion.Tdn1.Should().Be("ESIN");
        salida.Identificacion.Tdn2.Should().Be("ESIN-40");
        context.GetLastActivityInput<object>("ResolverTipologiaActivity").Should().BeNull();
    }

    [Fact]
    public async Task RunOrchestrator_TipologiaCatalogoResuelta_PueblaTdn1YTdn2DesdeTipologia()
    {
        var orchestrator = CreateOrchestrator();
        var context = new FakeTaskOrchestrationContext(BuildEntrada(classificationOnly: true));

        context.SetupActivity("NormalizarActivity", BuildNormalizarResult());
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "container/test.pdf");
        context.SetupActivity("ClasificarActivity", BuildClasificacionOk());
        context.SetupActivity("ResolverTipologiaActivity", BuildTipologia(tdn1: "SERE", tdn2: "SERE-01"));

        var salida = await orchestrator.RunOrchestrator(context);

        salida.Resultado.Estado.Should().Be("OK");
        salida.Identificacion.Tipologia.Should().Be("nota.simple.1_0");
        salida.Identificacion.Tdn1.Should().Be("SERE");
        salida.Identificacion.Tdn2.Should().Be("SERE-01");
    }

    [Fact]
    public async Task RunOrchestrator_TipologiaCatalogoConTdn2Detectado_ConservaTdn2DeFase2()
    {
        var orchestrator = CreateOrchestrator();
        var context = new FakeTaskOrchestrationContext(BuildEntrada(classificationOnly: true));

        var clasificacion = BuildClasificacionOk();
        clasificacion.Tdn2Detectado = "SERE-99";

        context.SetupActivity("NormalizarActivity", BuildNormalizarResult());
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "container/test.pdf");
        context.SetupActivity("ClasificarActivity", clasificacion);
        context.SetupActivity("ResolverTipologiaActivity", BuildTipologia(tdn1: "SERE", tdn2: "SERE-01"));

        var salida = await orchestrator.RunOrchestrator(context);

        salida.Resultado.Estado.Should().Be("OK");
        salida.Identificacion.Tdn1.Should().Be("SERE");
        salida.Identificacion.Tdn2.Should().Be("SERE-99");
    }

    [Fact]
    public async Task RunOrchestrator_SkipDuplicateCheck_OmiteVerificarDuplicadoActivity()
    {
        var orchestrator = CreateOrchestrator();
        var context = new FakeTaskOrchestrationContext(BuildEntrada(expectedType: "nota.simple", skipDuplicateCheck: true));

        context.SetupActivity("NormalizarActivity", BuildNormalizarResult());
        context.SetupActivityThrow("VerificarDuplicadoActivity", new InvalidOperationException("No debia llamarse VerificarDuplicadoActivity"));
        context.SetupActivity("SubirBlobActivity", "container/test.pdf");
        context.SetupActivityThrow("ResolverTipologiaActivity", new KeyNotFoundException("No existe la tipologia: nota.simple"));

        var salida = await orchestrator.RunOrchestrator(context);

        salida.Resultado.Estado.Should().Be("NO_CLASIFICADO");
        context.GetActivityCallCount("VerificarDuplicadoActivity").Should().Be(0);
    }

    [Fact]
    public async Task RunOrchestrator_ClassificationOnlyConExecuteIntegrarTrue_EjecutaIntegrarActivity()
    {
        var orchestrator = CreateOrchestrator();
        var entrada = BuildEntrada(classificationOnly: true, executeIntegrarWhenClassificationOnly: true);
        // Forzar IdActivo en trazabilidad para permitir que el orquestador ejecute Integrar en classificationOnly
        entrada.Trazabilidad.IdActivo = "ACT-TEST-001";
        var context = new FakeTaskOrchestrationContext(entrada);

        context.SetupActivity("NormalizarActivity", BuildNormalizarResultConMarkdown());
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "container/test.pdf");
        context.SetupActivity("PrepararDocumentoClasificacionActivity", new PrepararDocumentoClasificacionResultado
        {
            DocumentoBase64Clasif = "cmVjb3J0YWRv",
            TotalPaginas = 4,
            PaginasIncluidas = 2,
            RecorteAplicado = true
        });
        context.SetupActivity("ClasificarActivity", BuildClasificacionOk());
        context.SetupActivity("ResolverTipologiaActivity", BuildTipologia(extractionEnabled: true));
        context.SetupActivity("IntegrarActivity", new global::DocumentIA.Core.Models.ResultadoIntegracion
        {
            DatosFinales = new Dictionary<string, object> { ["Integrado"] = true }
        });

        var salida = await orchestrator.RunOrchestrator(context);

        salida.Resultado.Estado.Should().Be("OK");
        context.GetActivityCallCount("IntegrarActivity").Should().BeGreaterThan(0);
        context.GetLastActivityInput<IntegrarInput>("IntegrarActivity").Should().NotBeNull();
    }

    // ========== Costes de IA por ejecucion (AB#100231) ==========

    private static ResultadoClasificacion BuildClasificacionConConsumos()
    {
        var resultado = BuildClasificacionOk();
        resultado.Consumos.Add(new ConsumoIA
        {
            Actividad = ActividadesIA.Clasificar,
            Operacion = "classification.phase1",
            Proveedor = ProveedoresIA.AzureOpenAI,
            Modelo = "gpt-5-mini",
            TokensEntrada = 1000,
            TokensSalida = 200,
            CosteEur = 0.10m
        });
        return resultado;
    }

    private static global::DocumentIA.Core.Models.ExtraccionResultado BuildExtraccionConConsumos()
    {
        var resultado = BuildExtraccionOk();
        resultado.Consumos.Add(new ConsumoIA
        {
            Actividad = ActividadesIA.Extraer,
            Operacion = "extraction.cu.servicio",
            Proveedor = ProveedoresIA.ContentUnderstanding,
            Modelo = "analyzer-default",
            Paginas = 6,
            CosteEur = 0.05m
        });
        return resultado;
    }

    private static FakeTaskOrchestrationContext BuildContextoFlujoCompletoConConsumos(ContratoEntrada entrada)
    {
        var context = new FakeTaskOrchestrationContext(entrada);

        context.SetupActivity("NormalizarActivity", BuildNormalizarResultConMarkdown());
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "container/test.pdf");
        context.SetupActivity("ClasificarActivity", BuildClasificacionConConsumos());
        context.SetupActivity("ResolverTipologiaActivity", BuildTipologia(extractionEnabled: true, skipGdc: true));
        context.SetupActivity("ExtraerActivity", BuildExtraccionConConsumos());
        context.SetupActivity("ValidarActivity", BuildValidacionOk());
        context.SetupActivity("IntegrarActivity", new global::DocumentIA.Core.Models.ResultadoIntegracion
        {
            Estado = "OK",
            DatosFinales = new Dictionary<string, object>()
        });

        return context;
    }

    [Fact]
    public async Task RunOrchestrator_SalidaTempranaPorRateLimit_NoExponeConsumosNiImportes()
    {
        // Camino de salida temprana: asigna la clasificacion y retorna antes del
        // flujo normal. El consumo parcial ya pagado se conserva para persistirlo,
        // pero no puede aparecer en la respuesta si no se piden costes.
        var orchestrator = CreateOrchestrator();
        var entrada = BuildEntrada();
        entrada.Instrucciones.IncluirCostes = false;

        var context = new FakeTaskOrchestrationContext(entrada);
        context.SetupActivity("NormalizarActivity", BuildNormalizarResultConMarkdown());
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "container/test.pdf");

        var rateLimit = new ResultadoClasificacion
        {
            RateLimitExcedido = true,
            FallbackRazon = "rate_limit_exhausted",
            TipologiaDetectada = "Desconocido",
            Confianza = 0,
            Consumos =
            {
                new ConsumoIA
                {
                    Modelo = "gpt-5-mini",
                    Operacion = "classification.phase1",
                    TokensEntrada = 1200,
                    CosteEur = 0.05m,
                    TarifaAplicada = "gpt-5-mini@2026-07-21"
                }
            }
        };
        context.SetupActivity("ClasificarActivity", rateLimit);

        var salida = await orchestrator.RunOrchestrator(context);

        salida.Resultado.Estado.Should().Be("PENDIENTE_REINTENTO");
        salida.DetalleEjecucion.Costes.Should().BeNull();
        salida.DetalleEjecucion.Clasificacion.Consumos.Should().BeEmpty();
    }

    [Fact]
    public async Task RunOrchestrator_SalidaTempranaPorRateLimit_ConCostes_DevuelveElGastoYaPagado()
    {
        // El 429 no borra lo que ya se facturo antes de agotarse.
        var orchestrator = CreateOrchestrator();
        var entrada = BuildEntrada();
        entrada.Instrucciones.IncluirCostes = true;

        var context = new FakeTaskOrchestrationContext(entrada);
        context.SetupActivity("NormalizarActivity", BuildNormalizarResultConMarkdown());
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "container/test.pdf");
        context.SetupActivity("ClasificarActivity", new ResultadoClasificacion
        {
            RateLimitExcedido = true,
            FallbackRazon = "rate_limit_exhausted",
            TipologiaDetectada = "Desconocido",
            Consumos =
            {
                new ConsumoIA { Modelo = "gpt-5-mini", TokensEntrada = 1200, CosteEur = 0.05m }
            }
        });

        var salida = await orchestrator.RunOrchestrator(context);

        salida.DetalleEjecucion.Costes.Should().NotBeNull();
        salida.DetalleEjecucion.Costes!.CosteTotalEur.Should().Be(0.05m);
    }

    [Fact]
    public async Task RunOrchestrator_SinIncluirCostes_TampocoExponeConsumosPorLaClasificacion()
    {
        // ResultadoClasificacion forma parte del contrato de salida y su lista de
        // consumos llega ya tarificada desde la actividad. Sin vaciarla, los importes
        // se colarian en la respuesta por la puerta de atras aunque no se pidan.
        var orchestrator = CreateOrchestrator();
        var entrada = BuildEntrada();
        entrada.Instrucciones.IncluirCostes = false;
        var context = BuildContextoFlujoCompletoConConsumos(entrada);

        var salida = await orchestrator.RunOrchestrator(context);

        salida.DetalleEjecucion.Costes.Should().BeNull();
        salida.DetalleEjecucion.Clasificacion.Consumos.Should().BeEmpty();
    }

    [Fact]
    public async Task RunOrchestrator_ConIncluirCostes_ElDesgloseVaSoloEnElBloqueDeCostes()
    {
        // Un unico sitio donde mirar: el bloque de costes. La clasificacion no
        // duplica el desglose.
        var orchestrator = CreateOrchestrator();
        var entrada = BuildEntrada();
        entrada.Instrucciones.IncluirCostes = true;
        var context = BuildContextoFlujoCompletoConConsumos(entrada);

        var salida = await orchestrator.RunOrchestrator(context);

        salida.DetalleEjecucion.Costes!.Consumos.Should().HaveCount(2);
        salida.DetalleEjecucion.Clasificacion.Consumos.Should().BeEmpty();
    }

    [Fact]
    public async Task RunOrchestrator_SinIncluirCostes_NoDevuelveElBloque()
    {
        var orchestrator = CreateOrchestrator();
        var entrada = BuildEntrada();
        entrada.Instrucciones.IncluirCostes = false;
        var context = BuildContextoFlujoCompletoConConsumos(entrada);

        var salida = await orchestrator.RunOrchestrator(context);

        salida.Resultado.Estado.Should().Be("OK");
        salida.DetalleEjecucion.Costes.Should().BeNull();
    }

    [Fact]
    public async Task RunOrchestrator_ConIncluirCostes_DevuelveElBloqueAgregado()
    {
        var orchestrator = CreateOrchestrator();
        var entrada = BuildEntrada();
        entrada.Instrucciones.IncluirCostes = true;
        var context = BuildContextoFlujoCompletoConConsumos(entrada);

        var salida = await orchestrator.RunOrchestrator(context);

        salida.DetalleEjecucion.Costes.Should().NotBeNull();
        salida.DetalleEjecucion.Costes!.CosteTotalEur.Should().Be(0.15m);
        salida.DetalleEjecucion.Costes.Consumos.Should().HaveCount(2);
        salida.DetalleEjecucion.Costes.PaginasTotales.Should().Be(6);
        salida.DetalleEjecucion.Costes.TokensTotales.Should().Be(1200);
        salida.DetalleEjecucion.Costes.TarifasCompletas.Should().BeTrue();
    }

    [Fact]
    public async Task RunOrchestrator_LaPersistenciaRecibeElBloqueDeCostes()
    {
        // El bloque llega a la actividad de persistencia con el agregado completo.
        var orchestrator = CreateOrchestrator();
        var entrada = BuildEntrada();
        entrada.Instrucciones.IncluirCostes = true;
        var context = BuildContextoFlujoCompletoConConsumos(entrada);

        await orchestrator.RunOrchestrator(context);

        var persistido = context.GetLastActivityInput<PersistirInput>("PersistirActivity");
        persistido.Should().NotBeNull();
        persistido!.Salida.DetalleEjecucion.Costes.Should().NotBeNull();
        persistido.Salida.DetalleEjecucion.Costes!.CosteTotalEur.Should().Be(0.15m);
    }

    [Fact]
    public async Task RunOrchestrator_SinPedirCostes_PersisteIgualmenteYLuegoOculta()
    {
        // La ocultacion ocurre en el envoltorio, despues de que el cuerpo haya
        // persistido: la actividad se invoca igual y solo la respuesta pierde el bloque.
        // El fake guarda el input por referencia, asi que aqui solo se puede afirmar
        // que la persistencia se ejecuto; que recibio el bloque lo fija el test anterior.
        var orchestrator = CreateOrchestrator();
        var entrada = BuildEntrada();
        entrada.Instrucciones.IncluirCostes = false;
        var context = BuildContextoFlujoCompletoConConsumos(entrada);

        var salida = await orchestrator.RunOrchestrator(context);

        context.GetActivityCallCount("PersistirActivity").Should().Be(1);
        salida.DetalleEjecucion.Costes.Should().BeNull();
    }

    [Fact]
    public async Task RunOrchestrator_ConsumoDeProveedorDescartado_CuentaEnElTotal()
    {
        // La clasificacion evalua varios proveedores y se queda con uno, pero todos
        // se han pagado: el descartado no puede desaparecer del total.
        var orchestrator = CreateOrchestrator();
        var entrada = BuildEntrada();
        entrada.Instrucciones.IncluirCostes = true;

        var clasificacion = BuildClasificacionOk();
        clasificacion.Consumos.Add(new ConsumoIA
        {
            Modelo = "sareb-classifier-v1",
            Paginas = 3,
            CosteEur = 0.04m,
            Descartado = true
        });
        clasificacion.Consumos.Add(new ConsumoIA
        {
            Modelo = "gpt-5-mini",
            TokensEntrada = 500,
            CosteEur = 0.10m
        });

        var context = BuildContextoFlujoCompletoConConsumos(entrada);
        context.SetupActivity("ClasificarActivity", clasificacion);

        var salida = await orchestrator.RunOrchestrator(context);

        salida.DetalleEjecucion.Costes!.CosteTotalEur.Should().Be(0.19m);
        salida.DetalleEjecucion.Costes.Consumos.Should().Contain(c => c.Descartado);
    }

    [Fact]
    public async Task RunOrchestrator_ModeloSinTarifa_MarcaElAgregadoIncompleto()
    {
        var orchestrator = CreateOrchestrator();
        var entrada = BuildEntrada();
        entrada.Instrucciones.IncluirCostes = true;

        var clasificacion = BuildClasificacionOk();
        clasificacion.Consumos.Add(new ConsumoIA
        {
            Modelo = "modelo-sin-tarifa",
            TokensEntrada = 100,
            CosteEur = null
        });

        var context = BuildContextoFlujoCompletoConConsumos(entrada);
        context.SetupActivity("ClasificarActivity", clasificacion);

        var salida = await orchestrator.RunOrchestrator(context);

        salida.DetalleEjecucion.Costes!.TarifasCompletas.Should().BeFalse();
        salida.DetalleEjecucion.Costes.ModelosSinTarifa.Should().Contain("modelo-sin-tarifa");
    }

    [Fact]
    public async Task RunOrchestrator_SinConsumoDeIA_DevuelveAgregadoACero()
    {
        // Por ejemplo clasificacion forzada por ExpectedType, que no llama a ningun servicio.
        var orchestrator = CreateOrchestrator();
        var entrada = BuildEntrada();
        entrada.Instrucciones.IncluirCostes = true;
        var context = BuildContextoFlujoCompletoConConsumos(entrada);
        context.SetupActivity("ClasificarActivity", BuildClasificacionOk());
        context.SetupActivity("ExtraerActivity", BuildExtraccionOk());

        var salida = await orchestrator.RunOrchestrator(context);

        salida.DetalleEjecucion.Costes.Should().NotBeNull();
        salida.DetalleEjecucion.Costes!.CosteTotalEur.Should().Be(0m);
        salida.DetalleEjecucion.Costes.Consumos.Should().BeEmpty();
    }

    [Fact]
    public async Task RunOrchestrator_FlujoCompletoConExtraccionValidacionYPersistencia_FinalizaOkYPersiste()
    {
        var orchestrator = CreateOrchestrator();
        var context = new FakeTaskOrchestrationContext(BuildEntrada());

        context.SetupActivity("NormalizarActivity", BuildNormalizarResultConMarkdown());
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "container/test.pdf");
        context.SetupActivity("ClasificarActivity", BuildClasificacionOk());
        context.SetupActivity("ResolverTipologiaActivity", BuildTipologia(extractionEnabled: true, skipGdc: true));
        context.SetupActivity("ExtraerActivity", BuildExtraccionOk());
        context.SetupActivity("ValidarActivity", BuildValidacionOk());
        context.SetupActivity("IntegrarActivity", new global::DocumentIA.Core.Models.ResultadoIntegracion
        {
            Estado = "OK",
            DatosFinales = new Dictionary<string, object>
            {
                ["Titular"] = "Titular de prueba"
            }
        });

        var salida = await orchestrator.RunOrchestrator(context);

        salida.Resultado.Estado.Should().Be("OK");
        context.GetActivityCallCount("ExtraerActivity").Should().Be(1);
        context.GetActivityCallCount("ValidarActivity").Should().Be(1);
        context.GetLastActivityInput<PersistirInput>("PersistirActivity").Should().NotBeNull();
    }

    [Fact]
    public async Task RunOrchestrator_FallbackExtraccionSinDatos_DegradaEstadoAExtraccionIncompleta()
    {
        var orchestrator = CreateOrchestrator();
        var context = new FakeTaskOrchestrationContext(BuildEntrada());

        context.SetupActivity("NormalizarActivity", BuildNormalizarResultConMarkdown());
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "container/test.pdf");
        context.SetupActivity("ClasificarActivity", BuildClasificacionOk());
        context.SetupActivity("ResolverTipologiaActivity", BuildTipologia(extractionEnabled: true, skipGdc: true));
        context.SetupActivity("ExtraerActivity", new global::DocumentIA.Core.Models.ExtraccionResultado
        {
            Modelo = "gpt-4o-mini",
            Proveedor = "azure-openai",
            FallbackUsado = true,
            FallbackRazon = "exception:TimeoutException:cuModelKey=default.cu",
            DatosExtraidos = new Dictionary<string, object>()
        });
        context.SetupActivity("ValidarActivity", BuildValidacionOk());
        context.SetupActivity("IntegrarActivity", new global::DocumentIA.Core.Models.ResultadoIntegracion
        {
            Estado = "OK",
            DatosFinales = new Dictionary<string, object>()
        });

        var salida = await orchestrator.RunOrchestrator(context);

        salida.Resultado.Estado.Should().Be("EXTRACCION_INCOMPLETA");
        salida.Resultado.MensajeError.Should().Contain("cuModelKey=default.cu");
    }

    [Fact]
    public async Task RunOrchestrator_FallbackExtraccionTimeoutPropio_NuncaDegradaEstadoCalidadAError()
    {
        // AB#100130 (Fix 1): cuando la llamada GPT (fallback) agota su propio TimeoutSeconds,
        // GptFallbackExtraerDataProvider.BuildTimeoutResultado devuelve un ExtraccionResultado
        // controlado con ExtraccionTimeoutPropio=true, ConfianzaExtraccion=0 y
        // FallbackRazon=RazonExtraccionTimeout. El orquestador debe:
        //  - Marcar Resultado.Estado="EXTRACCION_INCOMPLETA" (estado de negocio, no ERROR tecnico).
        //  - Excluir la extraccion del calculo de ConfianzaGlobal (igual que Extraction.Enabled=false)
        //    en vez de forzar ConfianzaGlobal=0 -> EstadoCalidad="ERROR" de forma artificial.
        //  - Conservar la clasificacion y dejar rastro del motivo (EXTRACCION_TIMEOUT_GPT) en
        //    DetalleEjecucion.Extraccion.FallbackRazon.
        var orchestrator = CreateOrchestrator();
        var context = new FakeTaskOrchestrationContext(BuildEntrada());

        context.SetupActivity("NormalizarActivity", BuildNormalizarResultConMarkdown());
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "container/test.pdf");
        context.SetupActivity("ClasificarActivity", BuildClasificacionOk());
        context.SetupActivity("ResolverTipologiaActivity", BuildTipologia(extractionEnabled: true, skipGdc: true));
        context.SetupActivity("ExtraerActivity", new global::DocumentIA.Core.Models.ExtraccionResultado
        {
            Modelo = "gpt-4o-mini-test",
            Proveedor = "azure-openai",
            ProveedorExtrac = "GPT4oMini",
            FallbackUsado = true,
            FallbackRazon = GptFallbackExtraerDataProvider.RazonExtraccionTimeout,
            ConfianzaExtraccion = 0,
            ExtraccionTimeoutPropio = true,
            DatosExtraidos = new Dictionary<string, object>()
        });
        context.SetupActivity("ValidarActivity", BuildValidacionOk());
        context.SetupActivity("IntegrarActivity", new global::DocumentIA.Core.Models.ResultadoIntegracion
        {
            Estado = "OK",
            DatosFinales = new Dictionary<string, object>()
        });

        var salida = await orchestrator.RunOrchestrator(context);

        salida.Resultado.Estado.Should().Be("EXTRACCION_INCOMPLETA");
        salida.Resultado.EstadoCalidad.Should().NotBe("ERROR");
        // Con BuildClasificacionOk (0.95) y BuildValidacionOk (0.98), al excluirse la extraccion
        // del calculo (ConfExtrac=null), ConfianzaGlobal = Min(0.95, 0.98) = 0.95 >= UmbralOK (0.85).
        salida.Resultado.EstadoCalidad.Should().Be("OK");
        salida.Resultado.ConfianzaGlobal.Should().BeApproximately(0.95, 0.001);
        salida.DetalleEjecucion.Extraccion.FallbackRazon.Should().Contain(GptFallbackExtraerDataProvider.RazonExtraccionTimeout);
    }

    [Fact]
    public async Task RunOrchestrator_ExtraccionGptDirectaTimeoutPropio_MarcaExtraccionIncompletaSinCalidadError()
    {
        // AB#100130 (Fix 2): en el camino GPT DIRECTO (sin CU, alcanzable con provider
        // azure-openai/gpt explicito o sin fallback registrado), GptDirectExtraerDataProvider
        // fija FallbackUsado=false SIEMPRE (linea 53) — a diferencia del camino con fallback
        // CU->GPT. Si esa extraccion agota su propio timeout, ExtraccionTimeoutPropio=true es la
        // UNICA señal disponible: la puerta original en el orquestador
        // (`resultadoExtraccion.FallbackUsado && camposUtilesExtraccion == 0`) no se activaba para
        // este camino, dejando pasar en silencio un Estado="OK" con extraccion vacia. La puerta
        // ahora tambien considera ExtraccionTimeoutPropio.
        var orchestrator = CreateOrchestrator();
        var context = new FakeTaskOrchestrationContext(BuildEntrada());

        context.SetupActivity("NormalizarActivity", BuildNormalizarResultConMarkdown());
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "container/test.pdf");
        context.SetupActivity("ClasificarActivity", BuildClasificacionOk());
        context.SetupActivity("ResolverTipologiaActivity", BuildTipologia(extractionEnabled: true, skipGdc: true));
        context.SetupActivity("ExtraerActivity", new global::DocumentIA.Core.Models.ExtraccionResultado
        {
            Modelo = "gpt-4o-mini-direct-test",
            Proveedor = "azure-openai",
            ProveedorExtrac = "GPT4oMini",
            FallbackUsado = false, // Camino directo: GptDirectExtraerDataProvider siempre lo fija asi.
            FallbackRazon = GptFallbackExtraerDataProvider.RazonExtraccionTimeout,
            ConfianzaExtraccion = 0,
            ExtraccionTimeoutPropio = true,
            DatosExtraidos = new Dictionary<string, object>()
        });
        context.SetupActivity("ValidarActivity", BuildValidacionOk());
        context.SetupActivity("IntegrarActivity", new global::DocumentIA.Core.Models.ResultadoIntegracion
        {
            Estado = "OK",
            DatosFinales = new Dictionary<string, object>()
        });

        var salida = await orchestrator.RunOrchestrator(context);

        salida.Resultado.Estado.Should().Be("EXTRACCION_INCOMPLETA");
        salida.Resultado.EstadoCalidad.Should().NotBe("ERROR");
        salida.DetalleEjecucion.Extraccion.FallbackRazon.Should().Contain(GptFallbackExtraerDataProvider.RazonExtraccionTimeout);
    }

    [Fact]
    public async Task RunOrchestrator_FlujoCompletoConAssetResolver_EjecutaObtenerActivoEIntegrar()
    {
        var orchestrator = CreateOrchestrator();
        var context = new FakeTaskOrchestrationContext(BuildEntrada());

        context.SetupActivity("NormalizarActivity", BuildNormalizarResultConMarkdown());
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "container/test.pdf");
        context.SetupActivity("ClasificarActivity", BuildClasificacionOk());
        context.SetupActivity("ResolverTipologiaActivity", BuildTipologia(extractionEnabled: true, assetResolverEnabled: true, skipGdc: true));
        context.SetupActivity("ExtraerActivity", BuildExtraccionOk());
        context.SetupActivity("ValidarActivity", BuildValidacionOk());
        context.SetupActivity("ObtenerActivoActivity", BuildActivoOk());
        context.SetupActivity("IntegrarActivity", new global::DocumentIA.Core.Models.ResultadoIntegracion
        {
            Estado = "OK",
            IdActivoResuelto = "ACT-1",
            DatosFinales = new Dictionary<string, object>
            {
                ["DES_SERVICER"] = "HipoGes"
            }
        });

        var salida = await orchestrator.RunOrchestrator(context);

        salida.Resultado.Estado.Should().Be("OK");
        context.GetActivityCallCount("ObtenerActivoActivity").Should().Be(1);
        context.GetActivityCallCount("IntegrarActivity").Should().Be(1);
        context.GetLastActivityInput<ObtenerActivoInput>("ObtenerActivoActivity").Should().NotBeNull();
    }

    [Fact]
    public async Task RunOrchestrator_SkipGdcFalse_EjecutaSubirGdcActivity()
    {
        var orchestrator = CreateOrchestrator();
        var entrada = BuildEntrada();
        entrada.Instrucciones.SkipGDCUpload = false;
        var context = new FakeTaskOrchestrationContext(entrada);

        context.SetupActivity("NormalizarActivity", BuildNormalizarResultConMarkdown());
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "container/test.pdf");
        context.SetupActivity("ClasificarActivity", BuildClasificacionOk());
        context.SetupActivity("ResolverTipologiaActivity", BuildTipologia(extractionEnabled: true, skipGdc: false));
        context.SetupActivity("ExtraerActivity", BuildExtraccionOk());
        context.SetupActivity("ValidarActivity", BuildValidacionOk());
        context.SetupActivity("IntegrarActivity", new global::DocumentIA.Core.Models.ResultadoIntegracion
        {
            Estado = "OK",
            IdActivoResuelto = "ACT-GDC-001",
            DatosFinales = new Dictionary<string, object>()
        });
        context.SetupActivity("SubirGDCActivity", new global::DocumentIA.Core.Models.ResultadoGDC
        {
            Exitoso = true,
            ObjectId = "GDC-RESULT-001",
            Mensaje = "OK"
        });

        var salida = await orchestrator.RunOrchestrator(context);

        salida.Resultado.Estado.Should().Be("OK", $"error real: {salida.Resultado.MensajeError}");
        context.GetActivityCallCount("SubirGDCActivity").Should().Be(1);
    }

    [Fact]
    public async Task RunOrchestrator_InputNulo_RetornaErrorControlado()
    {
        var orchestrator = CreateOrchestrator();
        var context = new FakeTaskOrchestrationContext(null);

        var act = async () => await orchestrator.RunOrchestrator(context);

        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithMessage("*Contrato de entrada no puede ser nulo*");
    }

    [Fact]
    public async Task RunOrchestrator_NormalizarActivityFalla_RetornaErrorControlado()
    {
        var orchestrator = CreateOrchestrator();
        var context = new FakeTaskOrchestrationContext(BuildEntrada());
        context.SetupActivityThrow("NormalizarActivity", new Exception("fallo normalizando"));

        var salida = await orchestrator.RunOrchestrator(context);

        salida.Resultado.Estado.Should().Be("ERROR");
        salida.Resultado.MensajeError.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task RunOrchestrator_SubirBlobActivityFalla_RetornaErrorControlado()
    {
        var orchestrator = CreateOrchestrator();
        var context = new FakeTaskOrchestrationContext(BuildEntrada());
        context.SetupActivity("NormalizarActivity", BuildNormalizarResult());
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivityThrow("SubirBlobActivity", new Exception("fallo blob"));

        var salida = await orchestrator.RunOrchestrator(context);

        salida.Resultado.Estado.Should().Be("ERROR");
        salida.Resultado.MensajeError.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task RunOrchestrator_PromptActivityFalla_NoRompeElPipeline()
    {
        var orchestrator = CreateOrchestrator();
        var context = new FakeTaskOrchestrationContext(BuildEntrada(classificationOnly: true));

        context.SetupActivity("NormalizarActivity", BuildNormalizarResultConMarkdown());
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "container/test.pdf");
        context.SetupActivity("PrepararDocumentoClasificacionActivity", new PrepararDocumentoClasificacionResultado
        {
            DocumentoBase64Clasif = "cmVjb3J0YWRv",
            TotalPaginas = 2,
            PaginasIncluidas = 2,
            RecorteAplicado = false
        });
        context.SetupActivity("ClasificarActivity", BuildClasificacionOk());
        context.SetupActivity("ResolverTipologiaActivity", BuildTipologia(promptEnabled: true, promptHasDefinition: true));
        context.SetupActivityThrow("PromptActivity", new Exception("fallo prompt"));

        var salida = await orchestrator.RunOrchestrator(context);

        salida.Resultado.Estado.Should().Be("ERROR");
        salida.Resultado.MensajeError.Should().Contain("fallo prompt");
        context.GetActivityCallCount("PromptActivity").Should().Be(1);
    }

    [Fact]
    public async Task RunOrchestrator_ClassificationOnlyNivelTipologia_ResuelveTipologiaTecnicaCorrectamente()
    {
        var orchestrator = CreateOrchestrator();
        var entrada = BuildEntrada(classificationOnly: true);
        entrada.Instrucciones.Classification.NivelClasificacion = "TIPOLOGIA";
        var context = new FakeTaskOrchestrationContext(entrada);

        context.SetupActivity("NormalizarActivity", BuildNormalizarResult());
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "container/test.pdf");
        context.SetupActivity("PrepararDocumentoClasificacionActivity", new PrepararDocumentoClasificacionResultado
        {
            DocumentoBase64Clasif = "cmVjb3J0YWRv",
            TotalPaginas = 2,
            PaginasIncluidas = 2,
            RecorteAplicado = false
        });
        context.SetupActivity("ClasificarActivity", BuildClasificacionOk("nota.simple"));
        context.SetupActivity("ResolverTipologiaActivity", BuildTipologia(extractionEnabled: true));

        var salida = await orchestrator.RunOrchestrator(context);

        salida.Resultado.Estado.Should().Be("OK");
        salida.Identificacion.Tipologia.Should().Be("nota.simple.1_0");
        context.GetActivityCallCount("ResolverTipologiaActivity").Should().Be(1);
    }

    [Fact]
    public async Task RunOrchestrator_ClassificationOnlyConMaxPagesOverride_AplicaValorDeEntrada()
    {
        var orchestrator = CreateOrchestrator();
        var context = new FakeTaskOrchestrationContext(BuildEntrada(classificationOnly: true, maxPagesForClassificationOnly: 5));

        context.SetupActivity("NormalizarActivity", BuildNormalizarResult());
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "container/test.pdf");
        context.SetupActivity("PrepararDocumentoClasificacionActivity", new PrepararDocumentoClasificacionResultado
        {
            DocumentoBase64Clasif = "cmVjb3J0YWRv",
            TotalPaginas = 6,
            PaginasIncluidas = 5,
            RecorteAplicado = true
        });
        context.SetupActivity("ClasificarActivity", BuildClasificacionOk());
        context.SetupActivity("ResolverTipologiaActivity", BuildTipologia(extractionEnabled: true));

        var salida = await orchestrator.RunOrchestrator(context);

        salida.Resultado.Estado.Should().Be("OK");
        var prepInput = context.GetLastActivityInput<PrepararDocumentoClasificacionInput>("PrepararDocumentoClasificacionActivity");
        prepInput.Should().NotBeNull();
        prepInput!.MaxPaginasClasificacion.Should().Be(5);
    }

    [Fact]
    public async Task RunOrchestrator_BlobFirstSinMarkdown_FallbackLayoutDocumentoCompletoPropagaBlobPath()
    {
        // Regresión: en modo blob-first el orquestador vacía Content.Base64 (ahorro de memoria) y
        // trabaja solo con BlobPath. El fallback DI Layout de "documento completo" construía el
        // input sin propagar BlobPath, por lo que el provider enviaba base64Source vacío y Azure DI
        // respondía 400 InvalidContent ("The file is corrupted or format is unsupported"). Desde
        // AB#100252 el contexto lo construye el orquestador para el resolutor, y la petición de
        // documento completo debe seguir llevando el BlobPath.
        var orchestrator = CreateOrchestrator();
        var entrada = BuildEntrada();
        // Prompt ad hoc: es lo que hace que algún paso declare la necesidad de documento completo.
        // Sin prompt ni resumen forzado ya no se pide markdown entero que nadie va a consumir.
        entrada.Instrucciones.Prompt = new PromptInstrucciones
        {
            UserPromptTemplate = "Resume el documento:\n\n{contenido}"
        };
        entrada.Documento.Content.Base64 = string.Empty;
        entrada.Documento.BlobPath = "documents/blob-first.pdf";
        var context = new FakeTaskOrchestrationContext(entrada);

        context.SetupActivity("NormalizarActivity", BuildNormalizarResult());
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "documents/blob-first.pdf");
        context.SetupActivity("PrepararDocumentoClasificacionActivity", new PrepararDocumentoClasificacionResultado
        {
            DocumentoBase64Clasif = "cmVjb3J0YWRv",
            TotalPaginas = 2,
            PaginasIncluidas = 2,
            RecorteAplicado = false
        });
        // El resolutor no devuelve markdown en ninguna llamada → la petición de documento completo
        // se produce igualmente y es la que debe llevar el BlobPath.
        context.SetupActivity("ObtenerMarkdownActivity", new ResultadoMarkdown { Fuente = FuenteMarkdown.Ninguna });
        context.SetupActivity("ClasificarActivity", new ResultadoClasificacion
        {
            Modelo = "gpt-4o-mini",
            Confianza = 0.95,
            ConfianzaGPT = 0.95,
            ProveedorClasif = "GPT4oMini",
            TipologiaDetectada = "nota.simple",
            ContentExtraido = null
        });
        context.SetupActivity("ResolverTipologiaActivity", BuildTipologia(extractionEnabled: true));
        // Extracción sin markdown → markdownNormalizacion vacío → dispara el fallback DI Layout.
        context.SetupActivity("ExtraerActivity", new global::DocumentIA.Core.Models.ExtraccionResultado
        {
            Modelo = "gpt",
            DatosExtraidos = new Dictionary<string, object>()
        });
        context.SetupActivity("ValidarActivity", BuildValidacionOk());

        await orchestrator.RunOrchestrator(context);

        var markdownInput = context.GetLastActivityInput<ObtenerMarkdownInput>("ObtenerMarkdownActivity");
        markdownInput.Should().NotBeNull();
        markdownInput!.Necesidad.DocumentoCompleto.Should().BeTrue();
        markdownInput.Contexto.BlobPath.Should().NotBeNullOrWhiteSpace(
            "en blob-first la petición de documento completo debe usar BlobPath (urlSource) y no un base64 vacío");
        markdownInput.Contexto.BlobPath.Should().Be("documents/blob-first.pdf");
    }

    [Fact]
    public async Task RunOrchestrator_TipologiaDesconocidaConPromptAdHoc_EjecutaPrompt()
    {
        var orchestrator = CreateOrchestrator();
        var entrada = BuildEntrada();
        entrada.Instrucciones.Prompt = new PromptInstrucciones
        {
            SystemPrompt = "Eres un analista documental.",
            UserPromptTemplate = "Resume el documento:\n\n{contenido}"
        };
        var context = new FakeTaskOrchestrationContext(entrada);

        context.SetupActivity("NormalizarActivity", BuildNormalizarResultConMarkdown());
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "container/test.pdf");
        context.SetupActivity("ClasificarActivity", new ResultadoClasificacion
        {
            Modelo = "gpt-4o-mini",
            Confianza = 0,
            ProveedorClasif = "GPT4oMini",
            TipologiaDetectada = "Desconocido"
        });
        context.SetupActivity("ResolverTipologiaActivity", new ResolvedTipologia(
            RequestedValue: "Desconocido",
            TipologiaId: "Desconocido",
            Version: "N/A",
            TechnicalKey: "Desconocido",
            IsDefault: true,
            SkipGDCUpload: true,
            PromptEnabled: false,
            ExtractionEnabled: false));
        context.SetupActivity("PromptActivity", new PromptResultado
        {
            Modelo = "gpt-5-mini",
            Resultado = "Resumen del documento en tres puntos.",
            TiempoMs = 1200
        });

        var salida = await orchestrator.RunOrchestrator(context);

        salida.Resultado.Estado.Should().Be("NO_CLASIFICADO");
        context.GetActivityCallCount("PromptActivity").Should().Be(1);
        salida.DatosExtraidos.Should().ContainKey("ResultadoPrompt");
        salida.DatosExtraidos["ResultadoPrompt"].Should().Be("Resumen del documento en tres puntos.");
    }

    [Fact]
    public async Task RunOrchestrator_TipologiaDesconocidaSinPrompt_NoEjecutaPrompt()
    {
        var orchestrator = CreateOrchestrator();
        var context = new FakeTaskOrchestrationContext(BuildEntrada());

        context.SetupActivity("NormalizarActivity", BuildNormalizarResultConMarkdown());
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "container/test.pdf");
        context.SetupActivity("ClasificarActivity", new ResultadoClasificacion
        {
            Modelo = "gpt-4o-mini",
            Confianza = 0,
            ProveedorClasif = "GPT4oMini",
            TipologiaDetectada = "Desconocido"
        });
        context.SetupActivity("ResolverTipologiaActivity", new ResolvedTipologia(
            RequestedValue: "Desconocido",
            TipologiaId: "Desconocido",
            Version: "N/A",
            TechnicalKey: "Desconocido",
            IsDefault: true,
            SkipGDCUpload: true,
            PromptEnabled: false,
            ExtractionEnabled: false));

        var salida = await orchestrator.RunOrchestrator(context);

        salida.Resultado.Estado.Should().Be("NO_CLASIFICADO");
        context.GetActivityCallCount("PromptActivity").Should().Be(0);
    }

    [Fact]
    public async Task RunOrchestrator_TipologiaNoResolubleConPromptAdHoc_EjecutaPrompt()
    {
        var orchestrator = CreateOrchestrator();
        var entrada = BuildEntrada();
        entrada.Instrucciones.Prompt = new PromptInstrucciones
        {
            UserPromptTemplate = "Resume el documento:\n\n{contenido}"
        };
        var context = new FakeTaskOrchestrationContext(entrada);

        context.SetupActivity("NormalizarActivity", BuildNormalizarResultConMarkdown());
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "container/test.pdf");
        context.SetupActivity("ClasificarActivity", new ResultadoClasificacion
        {
            Modelo = "gpt-4o-mini",
            Confianza = 0,
            ProveedorClasif = "GPT4oMini",
            TipologiaDetectada = "TipologiaInexistente"
        });
        context.SetupActivityThrow(
            "ResolverTipologiaActivity",
            new KeyNotFoundException("No existe la tipologia: TipologiaInexistente"));
        context.SetupActivity("PromptActivity", new PromptResultado
        {
            Modelo = "gpt-5-mini",
            Resultado = "Resumen sin tipologia resuelta.",
            TiempoMs = 900
        });

        var salida = await orchestrator.RunOrchestrator(context);

        salida.Resultado.Estado.Should().Be("NO_CLASIFICADO");
        context.GetActivityCallCount("PromptActivity").Should().Be(1);
        salida.DatosExtraidos["ResultadoPrompt"].Should().Be("Resumen sin tipologia resuelta.");
    }

    [Fact]
    public async Task RunOrchestrator_TipologiaDesconocidaConForzarResumen_EjecutaPrompt()
    {
        var orchestrator = CreateOrchestrator();
        var context = new FakeTaskOrchestrationContext(BuildEntrada(forzarResumenPorDefecto: true));

        context.SetupActivity("NormalizarActivity", BuildNormalizarResultConMarkdown());
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "container/test.pdf");
        context.SetupActivity("ClasificarActivity", new ResultadoClasificacion
        {
            Modelo = "gpt-4o-mini",
            Confianza = 0,
            ProveedorClasif = "GPT4oMini",
            TipologiaDetectada = "Desconocido"
        });
        context.SetupActivity("ResolverTipologiaActivity", new ResolvedTipologia(
            RequestedValue: "Desconocido",
            TipologiaId: "Desconocido",
            Version: "N/A",
            TechnicalKey: "Desconocido",
            IsDefault: true,
            SkipGDCUpload: true,
            PromptEnabled: false,
            ExtractionEnabled: false));
        context.SetupActivity("PromptActivity", new PromptResultado
        {
            Modelo = "gpt-5-mini",
            Resumen = "Resumen ejecutivo del documento.",
            TiempoMs = 800
        });

        var salida = await orchestrator.RunOrchestrator(context);

        context.GetActivityCallCount("PromptActivity").Should().Be(1);
        salida.DatosExtraidos.Should().ContainKey("Resumen");
        salida.DatosExtraidos["Resumen"].Should().Be("Resumen ejecutivo del documento.");
    }

    [Fact]
    public async Task RunOrchestrator_PromptSinMarkdown_ExtraeMarkdownBajoDemanda()
    {
        var orchestrator = CreateOrchestrator();
        var entrada = BuildEntrada(expectedType: "resumen.documental");
        entrada.Instrucciones.Prompt = new PromptInstrucciones
        {
            UserPromptTemplate = "Resume el documento:\n\n{contenido}"
        };
        var context = new FakeTaskOrchestrationContext(entrada);

        context.SetupActivity("NormalizarActivity", BuildNormalizarResult());
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "container/test.pdf");
        context.SetupActivity("ResolverTipologiaActivity", new ResolvedTipologia(
            RequestedValue: "resumen.documental",
            TipologiaId: "resumen.documental",
            Version: "1.0",
            TechnicalKey: "resumen.documental",
            IsDefault: false,
            SkipGDCUpload: true,
            PromptEnabled: false,
            ExtractionEnabled: false));
        context.SetupActivity("ObtenerMarkdownActivity", MarkdownResuelto("# Contenido real del documento", 3, completo: true));
        context.SetupActivity("PromptActivity", new PromptResultado
        {
            Modelo = "gpt-5-mini",
            Resultado = "Resumen basado en el contenido.",
            TiempoMs = 1500
        });

        var salida = await orchestrator.RunOrchestrator(context);

        context.GetActivityCallCount("ObtenerMarkdownActivity").Should().Be(1);
        var promptInput = context.GetLastActivityInput<PromptActivityInput>("PromptActivity");
        promptInput.Should().NotBeNull();
        promptInput!.MarkdownExtraido.Should().Be("# Contenido real del documento");
        salida.DetalleEjecucion.MarkdownGenerado.Should().BeTrue();
    }

    [Fact]
    public async Task RunOrchestrator_BlobFirstConRecorteFallido_Paso28RecibeBlobPath()
    {
        // Escenario blob-first real (AB#100045): el trigger sube el blob y vacia Content.Base64.
        // Con un documento no-PDF el recorte lanza y docClasif hereda el base64 vacio; el Paso 2.8
        // debe apoyarse en BlobPath para que DI Layout pueda obtener el documento.
        var orchestrator = CreateOrchestrator();
        var entrada = BuildEntrada(nombre: "documento.pptx");
        entrada.Documento.Content.Base64 = string.Empty;
        entrada.Documento.BlobPath = "documents/2026/08/hash-nuevo.pptx";
        var context = new FakeTaskOrchestrationContext(entrada);

        // La NormalizarActivity real no devuelve "Paginas" (solo hashes y tamano): para un Office
        // el conteo debe llegar del layout, y con la clave inyectada el guard <= 0 lo impediria.
        var normalizadoSinPaginas = BuildNormalizarResult();
        normalizadoSinPaginas.Remove("Paginas");

        context.SetupActivity("NormalizarActivity", normalizadoSinPaginas);
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "documents/2026/08/hash-nuevo.pptx");
        context.SetupActivityThrow(
            "PrepararDocumentoClasificacionActivity",
            new InvalidOperationException("PdfPig: Could not find the version header comment at the start of the document."));
        // Office: el resolutor no puede recortar y analiza el documento entero, asi que la
        // resolucion vuelve completa y sus paginas si son las del documento.
        context.SetupActivity("ObtenerMarkdownActivity", MarkdownResuelto("# Diapositiva 1", 5, completo: true));
        context.SetupActivity("ClasificarActivity", new ResultadoClasificacion
        {
            Modelo = "gpt-4o-mini",
            Confianza = 0,
            ProveedorClasif = "GPT4oMini",
            TipologiaDetectada = "Desconocido"
        });
        context.SetupActivity("ResolverTipologiaActivity", new ResolvedTipologia(
            RequestedValue: "Desconocido",
            TipologiaId: "Desconocido",
            Version: "N/A",
            TechnicalKey: "Desconocido",
            IsDefault: true,
            SkipGDCUpload: true,
            PromptEnabled: false,
            ExtractionEnabled: false));

        var salida = await orchestrator.RunOrchestrator(context);

        var markdownInput = context.GetLastActivityInput<ObtenerMarkdownInput>("ObtenerMarkdownActivity");
        markdownInput.Should().NotBeNull();
        markdownInput!.Contexto.BlobPath.Should().Be("documents/2026/08/hash-nuevo.pptx");
        salida.DetalleEjecucion.MarkdownGenerado.Should().BeTrue();
        salida.DetalleEjecucion.OrigenMarkdown.Should().Be("LayoutPreClasificacion");
        salida.Identificacion.Paginas.Should().Be(5);
    }

    [Fact]
    public async Task RunOrchestrator_RecorteValido_Paso28PideElRangoSobreElBlob()
    {
        // Antes el orquestador anulaba BlobPath para no pisar el recorte del Paso 2.7. Con el
        // resolutor (AB#100245) el rango viaja en la necesidad y el blob viaja siempre en el
        // contexto: es el resolutor quien pide a Layout el rango pages=1-N sobre el documento.
        var orchestrator = CreateOrchestrator();
        var context = new FakeTaskOrchestrationContext(BuildEntrada());

        context.SetupActivity("NormalizarActivity", BuildNormalizarResult());
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "container/test.pdf");
        context.SetupActivity("PrepararDocumentoClasificacionActivity", new PrepararDocumentoClasificacionResultado
        {
            DocumentoBase64Clasif = "cmVjb3J0ZS1wZGY=",
            TotalPaginas = 10,
            PaginasIncluidas = 3,
            CharsTextoNativo = 1200,
            RecorteAplicado = true
        });
        context.SetupActivity("ObtenerMarkdownActivity", MarkdownResuelto("# Contenido PDF", 3, completo: false));
        context.SetupActivity("ClasificarActivity", BuildClasificacionOk());
        context.SetupActivity("ResolverTipologiaActivity", new ResolvedTipologia(
            RequestedValue: "nota.simple",
            TipologiaId: "nota.simple",
            Version: "1.0",
            TechnicalKey: "nota.simple",
            IsDefault: false,
            SkipGDCUpload: true,
            PromptEnabled: false,
            ExtractionEnabled: false));

        await orchestrator.RunOrchestrator(context);

        var markdownInput = context.GetLastActivityInput<ObtenerMarkdownInput>("ObtenerMarkdownActivity");
        markdownInput.Should().NotBeNull();
        markdownInput!.Contexto.BlobPath.Should().Be("container/test.pdf");
        markdownInput.Necesidad.DocumentoCompleto.Should().BeFalse();
        markdownInput.Necesidad.PaginasMinimas.Should().Be(3);
    }

    [Fact]
    public async Task RunOrchestrator_MarkdownBajoDemandaConPaginas_PropagaAIdentificacion()
    {
        var orchestrator = CreateOrchestrator();
        var entrada = BuildEntrada(nombre: "presentacion.pptx", expectedType: "resumen.documental");
        entrada.Instrucciones.Prompt = new PromptInstrucciones
        {
            UserPromptTemplate = "Resume el documento:\n\n{contenido}"
        };
        var context = new FakeTaskOrchestrationContext(entrada);

        // NormalizarActivity no devuelve "Paginas" en produccion (solo hashes y tamano), asi que
        // el documento llega aqui con Paginas=0: es justo el caso de un Office con ExpectedType,
        // donde ni el Paso 2.7 (recorte, solo PDF) ni el Paso 2.8 (no corre con ExpectedType)
        // pueden informarlas.
        var normalizadoSinPaginas = BuildNormalizarResult();
        normalizadoSinPaginas.Remove("Paginas");

        context.SetupActivity("NormalizarActivity", normalizadoSinPaginas);
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "container/presentacion.pptx");
        context.SetupActivity("ResolverTipologiaActivity", new ResolvedTipologia(
            RequestedValue: "resumen.documental",
            TipologiaId: "resumen.documental",
            Version: "1.0",
            TechnicalKey: "resumen.documental",
            IsDefault: false,
            SkipGDCUpload: true,
            PromptEnabled: false,
            ExtractionEnabled: false));
        // Las páginas solo se propagan a Identificacion cuando el markdown cubre el documento
        // entero: con un recorte, sus páginas son las leídas, no las del documento (AB#100245).
        context.SetupActivity("ObtenerMarkdownActivity", MarkdownResuelto("# Diapositivas", 5, completo: true));
        context.SetupActivity("PromptActivity", new PromptResultado
        {
            Modelo = "gpt-5-mini",
            Resultado = "Resumen de la presentacion.",
            TiempoMs = 900
        });

        var salida = await orchestrator.RunOrchestrator(context);

        salida.Identificacion.Paginas.Should().Be(5);
    }

    [Fact]
    public async Task RunOrchestrator_MarkdownBajoDemandaConPaginas_NoPisaUnValorPrevio()
    {
        var orchestrator = CreateOrchestrator();
        var entrada = BuildEntrada(expectedType: "resumen.documental");
        entrada.Instrucciones.Prompt = new PromptInstrucciones
        {
            UserPromptTemplate = "Resume el documento:\n\n{contenido}"
        };
        var context = new FakeTaskOrchestrationContext(entrada);

        var normalizadoConPaginas = BuildNormalizarResult();
        normalizadoConPaginas["Paginas"] = 12;

        context.SetupActivity("NormalizarActivity", normalizadoConPaginas);
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "container/test.pdf");
        context.SetupActivity("ResolverTipologiaActivity", new ResolvedTipologia(
            RequestedValue: "resumen.documental",
            TipologiaId: "resumen.documental",
            Version: "1.0",
            TechnicalKey: "resumen.documental",
            IsDefault: false,
            SkipGDCUpload: true,
            PromptEnabled: false,
            ExtractionEnabled: false));
        context.SetupActivity("ObtenerMarkdownActivity", MarkdownResuelto("# Contenido", 3, completo: true));
        context.SetupActivity("PromptActivity", new PromptResultado
        {
            Modelo = "gpt-5-mini",
            Resultado = "Resumen.",
            TiempoMs = 500
        });

        var salida = await orchestrator.RunOrchestrator(context);

        salida.Identificacion.Paginas.Should().Be(12);
    }

    [Fact]
    public async Task RunOrchestrator_PromptConMarkdownDisponible_NoExtraeMarkdownBajoDemanda()
    {
        var orchestrator = CreateOrchestrator();
        var entrada = BuildEntrada(expectedType: "resumen.documental");
        entrada.Instrucciones.Prompt = new PromptInstrucciones
        {
            UserPromptTemplate = "Resume el documento:\n\n{contenido}"
        };
        entrada.Instrucciones.Classification.Markdown = "# Markdown aportado por el caller";
        var context = new FakeTaskOrchestrationContext(entrada);

        context.SetupActivity("NormalizarActivity", BuildNormalizarResultConMarkdown());
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "container/test.pdf");
        context.SetupActivity("ResolverTipologiaActivity", new ResolvedTipologia(
            RequestedValue: "resumen.documental",
            TipologiaId: "resumen.documental",
            Version: "1.0",
            TechnicalKey: "resumen.documental",
            IsDefault: false,
            SkipGDCUpload: true,
            PromptEnabled: false,
            ExtractionEnabled: false));
        context.SetupActivity("PromptActivity", new PromptResultado
        {
            Modelo = "gpt-5-mini",
            Resultado = "Resumen con markdown previo.",
            TiempoMs = 700
        });

        await orchestrator.RunOrchestrator(context);

        context.GetActivityCallCount("ObtenerMarkdownActivity").Should().Be(0);
        var promptInput = context.GetLastActivityInput<PromptActivityInput>("PromptActivity");
        promptInput!.MarkdownExtraido.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task RunOrchestrator_PromptSinMarkdownYLayoutFalla_NoRompeLaEjecucion()
    {
        var orchestrator = CreateOrchestrator();
        var entrada = BuildEntrada(expectedType: "resumen.documental");
        entrada.Instrucciones.Prompt = new PromptInstrucciones
        {
            UserPromptTemplate = "Resume el documento:\n\n{contenido}"
        };
        var context = new FakeTaskOrchestrationContext(entrada);

        context.SetupActivity("NormalizarActivity", BuildNormalizarResult());
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "container/test.pdf");
        context.SetupActivity("ResolverTipologiaActivity", new ResolvedTipologia(
            RequestedValue: "resumen.documental",
            TipologiaId: "resumen.documental",
            Version: "1.0",
            TechnicalKey: "resumen.documental",
            IsDefault: false,
            SkipGDCUpload: true,
            PromptEnabled: false,
            ExtractionEnabled: false));
        context.SetupActivityThrow(
            "ObtenerMarkdownActivity",
            new InvalidOperationException("DI layout no disponible"));
        context.SetupActivity("PromptActivity", new PromptResultado
        {
            Modelo = "gpt-5-mini",
            Resultado = "Respuesta sin contenido.",
            TiempoMs = 400
        });

        var salida = await orchestrator.RunOrchestrator(context);

        // Con prompt ad hoc, la anticipacion del Paso 2.76 ya pide el documento completo antes de
        // clasificar. Al fallar esa peticion, la necesidad queda registrada como no cubierta y ni
        // el fallback del Paso 4 ni la obtencion bajo demanda del prompt la repiten con el mismo
        // input: un solo intento fallido y la ejecucion no se rompe.
        context.GetActivityCallCount("ObtenerMarkdownActivity").Should().Be(1);
        salida.Should().NotBeNull();
        salida.DetalleEjecucion.MarkdownGenerado.Should().BeFalse();
    }

    [Fact]
    public async Task RunOrchestrator_TipologiaDesconocidaSinMarkdown_ExtraeMarkdownBajoDemandaEnSalidaTemprana()
    {
        // Salida temprana por tipologia no resoluble: no pasa por el bloque de fallback del Paso 4
        // (ese solo existe en el camino normal de extraccion), asi que el prompt debe recibir el
        // markdown que se haya resuelto para la peticion. Con un prompt ad hoc, quien lo resuelve
        // es la anticipacion del Paso 2.76 (documento completo, una sola vez): la obtencion bajo
        // demanda de EjecutarPromptLibreAsync se limita a reutilizar esa cache sin volver a pedir
        // nada (AB#100252). Se usa ExpectedType para saltar el Paso 2.8, que de lo contrario
        // pediria tambien el recorte; con ExpectedType informado, ClasificarActivity ni siquiera
        // se invoca (ver Paso 3 del orquestador).
        var orchestrator = CreateOrchestrator();
        var entrada = BuildEntrada(expectedType: "Desconocido");
        entrada.Instrucciones.Prompt = new PromptInstrucciones
        {
            UserPromptTemplate = "Resume el documento:\n\n{contenido}"
        };
        var context = new FakeTaskOrchestrationContext(entrada);

        context.SetupActivity("NormalizarActivity", BuildNormalizarResult());
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "container/test.pdf");
        context.SetupActivity("ResolverTipologiaActivity", new ResolvedTipologia(
            RequestedValue: "Desconocido",
            TipologiaId: "Desconocido",
            Version: "N/A",
            TechnicalKey: "Desconocido",
            IsDefault: true,
            SkipGDCUpload: true,
            PromptEnabled: false,
            ExtractionEnabled: false));
        context.SetupActivity(
            "ObtenerMarkdownActivity",
            MarkdownResuelto("# Contenido real del documento en salida temprana", 2, completo: true));
        context.SetupActivity("PromptActivity", new PromptResultado
        {
            Modelo = "gpt-5-mini",
            Resultado = "Resumen basado en el contenido de salida temprana.",
            TiempoMs = 1100
        });

        var salida = await orchestrator.RunOrchestrator(context);

        context.GetActivityCallCount("ObtenerMarkdownActivity").Should().Be(1);
        salida.DetalleEjecucion.OrigenMarkdown.Should().Be("LayoutDocumentoCompletoAnticipado");
        var promptInput = context.GetLastActivityInput<PromptActivityInput>("PromptActivity");
        promptInput.Should().NotBeNull();
        promptInput!.MarkdownExtraido.Should().Be("# Contenido real del documento en salida temprana");
        salida.DatosExtraidos.Should().ContainKey("ResultadoPrompt");
        salida.DatosExtraidos["ResultadoPrompt"].Should().Be("Resumen basado en el contenido de salida temprana.");
    }

    [Fact]
    public async Task RunOrchestrator_PromptSinContenido_PersisteEjecucionFallidaSinResumen()
    {
        var orchestrator = CreateOrchestrator();
        var entrada = BuildEntrada();
        entrada.Instrucciones.ForzarResumenPorDefecto = true;
        var context = new FakeTaskOrchestrationContext(entrada);

        context.SetupActivity("NormalizarActivity", BuildNormalizarResult());
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "documents/sin-contenido.pdf");
        context.SetupActivity("PrepararDocumentoClasificacionActivity", new PrepararDocumentoClasificacionResultado
        {
            DocumentoBase64Clasif = "dGVzdA==",
            TotalPaginas = 1,
            PaginasIncluidas = 1,
            RecorteAplicado = false
        });
        context.SetupActivity("ObtenerMarkdownActivity", MarkdownResuelto("# markdown de prueba", 1, completo: true));
        context.SetupActivity("ClasificarActivity", BuildClasificacionOk());
        context.SetupActivity("ResolverTipologiaActivity", BuildTipologia(extractionEnabled: true));
        // Sin ResumenCombinado: si lo llevara, AplicarResumenCombinado lo escribiria en DatosExtraidos
        // antes de llegar al prompt, contaminando la aserción NotContainKey("Resumen") de más abajo.
        context.SetupActivity("ExtraerActivity", new global::DocumentIA.Core.Models.ExtraccionResultado
        {
            Modelo = "gpt",
            DatosExtraidos = new Dictionary<string, object>()
        });
        context.SetupActivity("ValidarActivity", BuildValidacionOk());
        context.SetupActivity("IntegrarActivity", new global::DocumentIA.Core.Models.ResultadoIntegracion
        {
            Estado = "OK",
            DatosFinales = new Dictionary<string, object>()
        });

        // La guarda ya ha decidido en el proveedor: la actividad devuelve el resultado marcado.
        context.SetupActivity("PromptActivity", new PromptResultado
        {
            SinContenido = true,
            Error = "Sin contenido del documento: no se ejecuta el prompt ni el resumen.",
            Modelo = "gpt-5-mini"
        });

        var salida = await orchestrator.RunOrchestrator(context);

        salida.Resultado.Estado.Should().Be("SIN_CONTENIDO_DOCUMENTO");
        salida.DatosExtraidos.Should().NotContainKey("Resumen");
        salida.DatosExtraidos.Should().NotContainKey("ResultadoPrompt");

        var traza = salida.DetalleEjecucion.Seguimiento.Actividades.Single(a => a.Nombre == "Prompt");
        traza.Estado.Should().Be("Failed");
        salida.DetalleEjecucion.Seguimiento.ActividadesCompletadas.Should().NotContain("Prompt");

        // Regresión: PersistirActivity es el único escritor de DocumentoEjecuciones y el monitor
        // solo lee esa tabla. Un corte antes de Persistir haría desaparecer la ejecución en vez de
        // dejarla visible como fallida; por eso el flujo debe llegar hasta Persistir con el estado
        // SIN_CONTENIDO_DOCUMENTO intacto (no pisado por Validar/Integrar).
        context.GetActivityCallCount("PersistirActivity").Should().Be(1);
        var persistirInput = context.GetLastActivityInput<PersistirInput>("PersistirActivity");
        persistirInput.Should().NotBeNull();
        persistirInput!.Salida.Resultado.Estado.Should().Be("SIN_CONTENIDO_DOCUMENTO");
    }

    [Fact]
    public async Task RunOrchestrator_TipologiaDesconocidaYPromptSinContenido_GanaSinContenido()
    {
        // Montaje de tipología desconocida (AB#100028) cruzado con PromptActivity sin contenido:
        // el estado más grave (SIN_CONTENIDO_DOCUMENTO) debe prevalecer sobre NO_CLASIFICADO.
        var orchestrator = CreateOrchestrator();
        var entrada = BuildEntrada();
        entrada.Instrucciones.Prompt = new PromptInstrucciones
        {
            SystemPrompt = "Eres un analista documental.",
            UserPromptTemplate = "Resume el documento:\n\n{contenido}"
        };
        var context = new FakeTaskOrchestrationContext(entrada);

        context.SetupActivity("NormalizarActivity", BuildNormalizarResultConMarkdown());
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "container/test.pdf");
        context.SetupActivity("ClasificarActivity", new ResultadoClasificacion
        {
            Modelo = "gpt-4o-mini",
            Confianza = 0,
            ProveedorClasif = "GPT4oMini",
            TipologiaDetectada = "Desconocido"
        });
        context.SetupActivity("ResolverTipologiaActivity", new ResolvedTipologia(
            RequestedValue: "Desconocido",
            TipologiaId: "Desconocido",
            Version: "N/A",
            TechnicalKey: "Desconocido",
            IsDefault: true,
            SkipGDCUpload: true,
            PromptEnabled: false,
            ExtractionEnabled: false));
        context.SetupActivity("PromptActivity", new PromptResultado
        {
            SinContenido = true,
            Error = "Sin contenido del documento: no se ejecuta el prompt ni el resumen.",
            Modelo = "gpt-5-mini"
        });

        var salida = await orchestrator.RunOrchestrator(context);

        salida.Resultado.Estado.Should().Be("SIN_CONTENIDO_DOCUMENTO");
    }

    [Fact]
    public async Task RunOrchestrator_TipologiaDesconocidaYPromptSinContenido_PersisteLaEjecucion()
    {
        // Regresión AB#100031: la salida temprana por tipología desconocida (AB#100028) llamaba al
        // prompt en salida temprana pero devolvía sin pasar por PersistirActivity. Si ese prompt
        // activa la guarda de contenido (SIN_CONTENIDO_DOCUMENTO), la ejecución debe quedar
        // persistida igualmente: PersistirActivity es el único escritor de DocumentoEjecuciones.
        var orchestrator = CreateOrchestrator();
        var entrada = BuildEntrada();
        entrada.Instrucciones.Prompt = new PromptInstrucciones
        {
            SystemPrompt = "Eres un analista documental.",
            UserPromptTemplate = "Resume el documento:\n\n{contenido}"
        };
        var context = new FakeTaskOrchestrationContext(entrada);

        context.SetupActivity("NormalizarActivity", BuildNormalizarResultConMarkdown());
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "container/test.pdf");
        context.SetupActivity("ClasificarActivity", new ResultadoClasificacion
        {
            Modelo = "gpt-4o-mini",
            Confianza = 0,
            ProveedorClasif = "GPT4oMini",
            TipologiaDetectada = "Desconocido"
        });
        context.SetupActivity("ResolverTipologiaActivity", new ResolvedTipologia(
            RequestedValue: "Desconocido",
            TipologiaId: "Desconocido",
            Version: "N/A",
            TechnicalKey: "Desconocido",
            IsDefault: true,
            SkipGDCUpload: true,
            PromptEnabled: false,
            ExtractionEnabled: false));
        context.SetupActivity("PromptActivity", new PromptResultado
        {
            SinContenido = true,
            Error = "Sin contenido del documento: no se ejecuta el prompt ni el resumen.",
            Modelo = "gpt-5-mini"
        });

        var salida = await orchestrator.RunOrchestrator(context);

        salida.Resultado.Estado.Should().Be("SIN_CONTENIDO_DOCUMENTO");
        context.GetActivityCallCount("PersistirActivity").Should().Be(1);
        var persistirInput = context.GetLastActivityInput<PersistirInput>("PersistirActivity");
        persistirInput.Should().NotBeNull();
        persistirInput!.Salida.Resultado.Estado.Should().Be("SIN_CONTENIDO_DOCUMENTO");
    }

    [Fact]
    public async Task RunOrchestrator_TipologiaNoResoluble_PersisteEjecucionNoClasificada()
    {
        // AB#100178: la rama NO_CLASIFICADO retornaba sin PersistirActivity y la ejecución
        // desaparecía (caso DICTAMEN_TECNICO_CORNELLA en PRO, 25/08).
        var orchestrator = CreateOrchestrator();
        var entrada = BuildEntrada();
        var context = new FakeTaskOrchestrationContext(entrada);

        context.SetupActivity("NormalizarActivity", BuildNormalizarResult());
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "container/test.pdf");
        context.SetupActivity("ClasificarActivity", new ResultadoClasificacion
        {
            Modelo = "gpt-4o-mini",
            Confianza = 0.4,
            TipologiaDetectada = "etiqueta-inexistente"
        });
        context.SetupActivity("ResolverTipologiaActivity", new ResolvedTipologia(
            RequestedValue: "etiqueta-inexistente",
            TipologiaId: "Desconocido",
            Version: "N/A",
            TechnicalKey: "Desconocido",
            IsDefault: true,
            SkipGDCUpload: true,
            PromptEnabled: false,
            ExtractionEnabled: false));

        var salida = await orchestrator.RunOrchestrator(context);

        salida.Resultado.Estado.Should().Be("NO_CLASIFICADO");
        context.GetActivityCallCount("PersistirActivity").Should().Be(1);
        var persistirInput = context.GetLastActivityInput<PersistirInput>("PersistirActivity");
        persistirInput!.Salida.Resultado.Estado.Should().Be("NO_CLASIFICADO");
    }

    [Fact]
    public async Task RunOrchestrator_DuplicadoSinHistoricoReutilizable_PersisteEjecucionDuplicada()
    {
        var orchestrator = CreateOrchestrator();
        var entrada = BuildEntrada();
        var context = new FakeTaskOrchestrationContext(entrada);

        context.SetupActivity("NormalizarActivity", BuildNormalizarResult());
        context.SetupActivity("VerificarDuplicadoActivity", true);
        context.SetupActivity("ObtenerUltimaEjecucionDuplicadoActivity", (ContratoSalida?)null);

        var salida = await orchestrator.RunOrchestrator(context);

        salida.Resultado.Estado.Should().Be("DUPLICADO");
        context.GetActivityCallCount("PersistirActivity").Should().Be(1);
        var persistirInput = context.GetLastActivityInput<PersistirInput>("PersistirActivity");
        persistirInput!.Salida.Resultado.Estado.Should().Be("DUPLICADO");
    }

    [Fact]
    public async Task RunOrchestrator_ClassificationOnlyConTipologiaDesconocida_PersisteEjecucion()
    {
        var orchestrator = CreateOrchestrator();
        var entrada = BuildEntrada();
        entrada.Instrucciones.ClassificationOnly = true;
        var context = new FakeTaskOrchestrationContext(entrada);

        context.SetupActivity("NormalizarActivity", BuildNormalizarResult());
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "container/test.pdf");
        context.SetupActivity("ClasificarActivity", new ResultadoClasificacion
        {
            Modelo = "gpt-4o-mini",
            Confianza = 0,
            TipologiaDetectada = "sin-catalogo"
        });
        context.SetupActivity("ResolverTipologiaActivity", new ResolvedTipologia(
            RequestedValue: "sin-catalogo",
            TipologiaId: "Desconocido",
            Version: "N/A",
            TechnicalKey: "Desconocido",
            IsDefault: true,
            SkipGDCUpload: true,
            PromptEnabled: false,
            ExtractionEnabled: false));

        var salida = await orchestrator.RunOrchestrator(context);

        salida.Resultado.Estado.Should().Be("NO_CLASIFICADO");
        context.GetActivityCallCount("PersistirActivity").Should().Be(1);
    }

    [Fact]
    public async Task RunOrchestrator_ExpectedTypeNoResoluble_IgnoraElAtajoYClasifica()
    {
        // AB#100179: el canal GDC envía etiquetas de negocio ("Otros", "Ficha técnica")
        // que no son códigos de tipología; antes saltaban la clasificación y acababan
        // en NO_CLASIFICADO con Confianza=1.0.
        var orchestrator = CreateOrchestrator();
        var entrada = BuildEntrada(expectedType: "Ficha técnica");
        var context = new FakeTaskOrchestrationContext(entrada);

        context.SetupActivity("NormalizarActivity", BuildNormalizarResult());
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "container/test.pdf");
        context.SetupActivity("ClasificarActivity", BuildClasificacionOk());
        context.SetupActivity("ResolverTipologiaActivity", new ResolvedTipologia(
            RequestedValue: "Ficha técnica",
            TipologiaId: "Desconocido",
            Version: "N/A",
            TechnicalKey: "Desconocido",
            IsDefault: true,
            SkipGDCUpload: true,
            PromptEnabled: false,
            ExtractionEnabled: false));

        var salida = await orchestrator.RunOrchestrator(context);

        // La clasificación real DEBE ejecutarse (el atajo queda descartado).
        context.GetActivityCallCount("ClasificarActivity").Should().Be(1);
        salida.DetalleEjecucion.Clasificacion.Modelo.Should().NotBe("expectedtype-input");
    }

    [Fact]
    public async Task RunOrchestrator_ExpectedTypeValido_MantieneElAtajo()
    {
        var orchestrator = CreateOrchestrator();
        var entrada = BuildEntrada(expectedType: "nota.simple");
        var context = new FakeTaskOrchestrationContext(entrada);

        context.SetupActivity("NormalizarActivity", BuildNormalizarResult());
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "container/test.pdf");
        // IsDefault: false => ExpectedType resuelve contra el catálogo y el atajo se mantiene.
        context.SetupActivity("ResolverTipologiaActivity", BuildTipologia());

        var salida = await orchestrator.RunOrchestrator(context);

        context.GetActivityCallCount("ClasificarActivity").Should().Be(0);
        salida.DetalleEjecucion.Clasificacion.Modelo.Should().Be("expectedtype-input");
    }

    [Fact]
    public async Task RunOrchestrator_ClasificacionSinContenido_PersisteSinContenidoDocumento()
    {
        // AB#100180: sin texto extraíble, la clasificación respondía con resumen 'N/A' o
        // inventado desde el nombre del fichero, y la ejecución cerraba OK.
        var orchestrator = CreateOrchestrator();
        var entrada = BuildEntrada();
        var context = new FakeTaskOrchestrationContext(entrada);

        context.SetupActivity("NormalizarActivity", BuildNormalizarResult());
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "container/test.pdf");
        context.SetupActivity("ClasificarActivity", new ResultadoClasificacion
        {
            Modelo = "gpt-4o-mini",
            Confianza = 0,
            TipologiaDetectada = "Desconocido",
            SinContenido = true,
            FallbackRazon = "Sin contenido textual del documento para clasificar."
        });

        var salida = await orchestrator.RunOrchestrator(context);

        salida.Resultado.Estado.Should().Be("SIN_CONTENIDO_DOCUMENTO");
        salida.Resultado.EstadoCalidad.Should().Be("ERROR");
        salida.DatosExtraidos.Should().NotContainKey("Resumen");
        context.GetActivityCallCount("PersistirActivity").Should().Be(1);
        var persistirInput = context.GetLastActivityInput<PersistirInput>("PersistirActivity");
        persistirInput!.Salida.Resultado.Estado.Should().Be("SIN_CONTENIDO_DOCUMENTO");
    }

    [Fact]
    public async Task RunOrchestrator_ExpectedTypeValidoQueEsVersionPorDefecto_MantieneElAtajo()
    {
        // AB#100242: IsDefault tiene dos significados. El fallback de ResolverTipologiaActivity lo
        // pone a true para marcar "no resoluble", pero en una tipología que SÍ resuelve significa
        // "es la versión por defecto de su familia". Tomarlo por lo primero descartaba ExpectedType
        // válidos: "resumen.documental" (petición de solo resumen) acababa clasificando sin markdown
        // y cerrando en SIN_CONTENIDO_DOCUMENTO.
        var orchestrator = CreateOrchestrator();
        var entrada = BuildEntrada(expectedType: "resumen.documental");
        var context = new FakeTaskOrchestrationContext(entrada);

        context.SetupActivity("NormalizarActivity", BuildNormalizarResult());
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "container/test.pdf");
        // Tipología real del catálogo, publicada, y marcada como versión por defecto de su familia.
        context.SetupActivity("ResolverTipologiaActivity", new ResolvedTipologia(
            RequestedValue: "resumen.documental",
            TipologiaId: "resumen.documental",
            Version: "1.0",
            TechnicalKey: "resumen.documental",
            IsDefault: true,
            SkipGDCUpload: true,
            PromptEnabled: false,
            ExtractionEnabled: false));

        var salida = await orchestrator.RunOrchestrator(context);

        context.GetActivityCallCount("ClasificarActivity").Should().Be(0);
        salida.DetalleEjecucion.Clasificacion.Modelo.Should().Be("expectedtype-input");
    }

    [Fact]
    public async Task RunOrchestrator_ExpectedTypeNoResoluble_ExtraeMarkdownAntesDeClasificar()
    {
        // AB#100217: al descartar un ExpectedType inválido la clasificación procede "normalmente",
        // pero el Paso 2.8 ya había quedado atrás (su guarda mira ExpectedType antes de que la
        // validación lo anule), así que el clasificador llegaba sin texto. Un documento enviado sin
        // ExpectedType sí se habría OCR-eado: el flujo debe ser idéntico en ambos casos.
        var orchestrator = CreateOrchestrator();
        var entrada = BuildEntrada(expectedType: "Ficha técnica");
        var context = new FakeTaskOrchestrationContext(entrada);

        context.SetupActivity("NormalizarActivity", BuildNormalizarResult());
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "container/test.pdf");
        context.SetupActivity("PrepararDocumentoClasificacionActivity", new PrepararDocumentoClasificacionResultado
        {
            DocumentoBase64Clasif = "cmVjb3J0YWRv",
            TotalPaginas = 5,
            PaginasIncluidas = 3,
            RecorteAplicado = true
        });
        context.SetupActivity("ObtenerMarkdownActivity", MarkdownResuelto("# Contenido real del documento escaneado", 5, completo: false));
        context.SetupActivity("ClasificarActivity", BuildClasificacionOk());
        context.SetupActivity("ResolverTipologiaActivity", new ResolvedTipologia(
            RequestedValue: "Ficha técnica",
            TipologiaId: "Desconocido",
            Version: "N/A",
            TechnicalKey: "Desconocido",
            IsDefault: true,
            SkipGDCUpload: true,
            PromptEnabled: false,
            ExtractionEnabled: false));

        var salida = await orchestrator.RunOrchestrator(context);

        // El origen registra el momento en que se obtuvo el markdown. Es la aserción que distingue
        // "se extrajo antes de clasificar" de "lo aportó la propia clasificación después": el
        // diccionario DatosNormalizados es el mismo objeto y el Paso posterior a clasificar (L1161)
        // también escribe en él, así que comprobar solo su contenido final no probaría nada.
        salida.DetalleEjecucion.OrigenMarkdown.Should().Be("LayoutPreClasificacion");
        context.GetActivityCallCount("ObtenerMarkdownActivity").Should().Be(1);

        var clasificarInput = context.GetLastActivityInput<ClasificacionInput>("ClasificarActivity");
        clasificarInput.Should().NotBeNull();
        clasificarInput!.DatosNormalizados["Markdown"].Should().Be("# Contenido real del documento escaneado");
    }

    private static ResultadoMarkdown MarkdownResuelto(string markdown, int paginas, bool completo, FuenteMarkdown fuente = FuenteMarkdown.Layout)
        => new() { Markdown = markdown, Paginas = paginas, Completo = completo, Fuente = fuente };

    [Fact]
    public async Task RunOrchestrator_SinExpectedType_PideAlResolutorElRecorteParaClasificar()
    {
        // Paso 2.8 nuevo: la necesidad de clasificacion es Paginas(N) con N = maximo de paginas
        // de clasificacion (3 por defecto). El resolutor decide si viene de cache, BD o Layout.
        var orchestrator = CreateOrchestrator();
        var context = new FakeTaskOrchestrationContext(BuildEntrada());
        context.SetupActivity("NormalizarActivity", BuildNormalizarResult());
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "documents/2026/09/abc.pdf");
        context.SetupActivity("ObtenerMarkdownActivity", MarkdownResuelto("# recorte", 3, completo: false));
        context.SetupActivity("ClasificarActivity", BuildClasificacionOk());
        context.SetupActivity("ResolverTipologiaActivity", BuildTipologia());

        var salida = await orchestrator.RunOrchestrator(context);

        var input = context.GetLastActivityInput<ObtenerMarkdownInput>("ObtenerMarkdownActivity");
        input.Should().NotBeNull();
        input!.Necesidad.DocumentoCompleto.Should().BeFalse();
        input.Necesidad.PaginasMinimas.Should().Be(3);
        input.Contexto.BlobPath.Should().Be("documents/2026/09/abc.pdf");
        input.Contexto.Sha256.Should().Be("sha256abc");
        var clasificarInput = context.GetLastActivityInput<ClasificacionInput>("ClasificarActivity");
        clasificarInput!.DatosNormalizados["Markdown"].Should().Be("# recorte");
        salida.DetalleEjecucion.OrigenMarkdown.Should().Be("LayoutPreClasificacion");
        salida.DetalleEjecucion.MarkdownFuente.Should().Be("Layout");
        salida.DetalleEjecucion.MarkdownPaginas.Should().Be(3);
        salida.DetalleEjecucion.MarkdownCompleto.Should().BeFalse();
    }

    [Fact]
    public async Task RunOrchestrator_PromptAdHocSinExpectedType_ExtraeElCompletoUnaVezYClasificaConEl()
    {
        // Regla 7: la peticion ya declara que necesitara el completo (prompt ad hoc), asi que se
        // pide una sola vez al principio y la clasificacion lo usa tal cual, sin recorte.
        var orchestrator = CreateOrchestrator();
        var entrada = BuildEntrada();
        entrada.Instrucciones.Prompt = new PromptInstrucciones { UserPromptTemplate = "Resume:\n\n{contenido}" };
        var context = new FakeTaskOrchestrationContext(entrada);
        context.SetupActivity("NormalizarActivity", BuildNormalizarResult());
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "container/test.pdf");
        context.SetupActivity("ObtenerMarkdownActivity", MarkdownResuelto("# documento entero", 14, completo: true));
        context.SetupActivity("ClasificarActivity", BuildClasificacionOk());
        context.SetupActivity("ResolverTipologiaActivity", BuildTipologia());
        context.SetupActivity("ValidarActivity", BuildValidacionOk());
        context.SetupActivity("IntegrarActivity", new global::DocumentIA.Core.Models.ResultadoIntegracion { Estado = "OK", DatosFinales = new Dictionary<string, object>() });
        context.SetupActivity("PromptActivity", new PromptResultado { Modelo = "gpt-5-mini", Resultado = "ok" });

        var salida = await orchestrator.RunOrchestrator(context);

        context.GetActivityCallCount("ObtenerMarkdownActivity").Should().Be(1);
        var input = context.GetLastActivityInput<ObtenerMarkdownInput>("ObtenerMarkdownActivity");
        input!.Necesidad.DocumentoCompleto.Should().BeTrue();
        var clasificarInput = context.GetLastActivityInput<ClasificacionInput>("ClasificarActivity");
        clasificarInput!.DatosNormalizados["Markdown"].Should().Be("# documento entero");
        salida.DetalleEjecucion.OrigenMarkdown.Should().Be("LayoutDocumentoCompletoAnticipado");
        salida.DetalleEjecucion.MarkdownCompleto.Should().BeTrue();
    }

    [Fact]
    public async Task RunOrchestrator_ExpectedTypeConPromptDeTipologia_AnticipaElCompleto()
    {
        var orchestrator = CreateOrchestrator();
        var context = new FakeTaskOrchestrationContext(BuildEntrada(expectedType: "nota.simple"));
        context.SetupActivity("NormalizarActivity", BuildNormalizarResult());
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "container/test.pdf");
        context.SetupActivity("ResolverTipologiaActivity", BuildTipologia(promptEnabled: true, promptHasDefinition: true));
        context.SetupActivity("ObtenerMarkdownActivity", MarkdownResuelto("# entero", 5, completo: true));
        context.SetupActivity("ValidarActivity", BuildValidacionOk());
        context.SetupActivity("IntegrarActivity", new global::DocumentIA.Core.Models.ResultadoIntegracion { Estado = "OK", DatosFinales = new Dictionary<string, object>() });
        context.SetupActivity("PromptActivity", new PromptResultado { Modelo = "gpt-5-mini", Resultado = "ok" });

        await orchestrator.RunOrchestrator(context);

        context.GetActivityCallCount("ClasificarActivity").Should().Be(0);
        context.GetActivityCallCount("ObtenerMarkdownActivity").Should().Be(1);
        context.GetLastActivityInput<ObtenerMarkdownInput>("ObtenerMarkdownActivity")!.Necesidad.DocumentoCompleto.Should().BeTrue();
    }

    [Fact]
    public async Task RunOrchestrator_MarkdownDelCaller_NoLlamaAlResolutor()
    {
        var orchestrator = CreateOrchestrator();
        var entrada = BuildEntrada();
        entrada.Instrucciones.Classification.Markdown = "# lo trae el caller";
        var context = new FakeTaskOrchestrationContext(entrada);
        context.SetupActivity("NormalizarActivity", BuildNormalizarResult());
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "container/test.pdf");
        context.SetupActivity("ClasificarActivity", BuildClasificacionOk());
        context.SetupActivity("ResolverTipologiaActivity", BuildTipologia());

        var salida = await orchestrator.RunOrchestrator(context);

        context.GetActivityCallCount("ObtenerMarkdownActivity").Should().Be(0);
        context.GetLastActivityInput<ClasificacionInput>("ClasificarActivity")!.DatosNormalizados["Markdown"].Should().Be("# lo trae el caller");
        salida.DetalleEjecucion.MarkdownFuente.Should().Be("Caller");
        salida.DetalleEjecucion.OrigenMarkdown.Should().Be("InstruccionesCallerPreClasificacion");
    }

    [Fact]
    public async Task RunOrchestrator_ClasificadorConTextoPropio_LoPersisteConSuCobertura()
    {
        // DI y CU generan su propio texto: no se pide al resolutor antes de clasificar, pero lo que
        // devuelven entra en la cache y se persiste con la misma regla.
        var orchestrator = CreateOrchestrator();
        var entrada = BuildEntrada();
        entrada.Instrucciones.Classification.Provider = "di";
        var context = new FakeTaskOrchestrationContext(entrada);
        context.SetupActivity("NormalizarActivity", BuildNormalizarResult());
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "container/test.pdf");
        var clasificacion = BuildClasificacionOk();
        clasificacion.PagesProcessed = 3;
        context.SetupActivity("ClasificarActivity", clasificacion);
        context.SetupActivity("ResolverTipologiaActivity", BuildTipologia());

        var salida = await orchestrator.RunOrchestrator(context);

        context.GetActivityCallCount("ObtenerMarkdownActivity").Should().Be(0);
        context.GetActivityCallCount("PersistirMarkdownActivity").Should().Be(1);
        var persistir = context.GetLastActivityInput<PersistirMarkdownInput>("PersistirMarkdownActivity");
        persistir!.Markdown.Should().Be("# markdown clasificacion");
        persistir.Paginas.Should().Be(3);
        persistir.Sha256.Should().Be("sha256abc");
        salida.DetalleEjecucion.MarkdownFuente.Should().Be("Clasificador");
        salida.DetalleEjecucion.OrigenMarkdown.Should().Be("Clasificacion");
    }

    [Fact]
    public async Task RunOrchestrator_ResolutorSinContenido_NoSeReintentaLaMismaNecesidad()
    {
        // Regresion AB#100029: si el resolutor no pudo dar markdown para una necesidad, no se le
        // vuelve a pedir lo mismo en la misma ejecucion (pagaria Layout dos veces para fallar dos).
        // El escenario necesita dos peticiones reales al resolutor: prompt ad hoc sin ExpectedType
        // hace que el Paso 2.76 pida Completo() y que el Paso 2.8 pida despues Paginas(N). Como el
        // fallo fue con documento completo, la segunda peticion no debe llegar a la actividad.
        var orchestrator = CreateOrchestrator();
        var entrada = BuildEntrada();
        entrada.Instrucciones.Prompt = new PromptInstrucciones { UserPromptTemplate = "Resume:\n\n{contenido}" };
        var context = new FakeTaskOrchestrationContext(entrada);
        context.SetupActivity("NormalizarActivity", BuildNormalizarResult());
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "container/test.pdf");
        context.SetupActivity("ObtenerMarkdownActivity", new ResultadoMarkdown { Fuente = FuenteMarkdown.Ninguna });
        context.SetupActivity("ClasificarActivity", BuildClasificacionOk());
        context.SetupActivity("ResolverTipologiaActivity", BuildTipologia());
        context.SetupActivity("ValidarActivity", BuildValidacionOk());
        context.SetupActivity("IntegrarActivity", new global::DocumentIA.Core.Models.ResultadoIntegracion { Estado = "OK", DatosFinales = new Dictionary<string, object>() });
        context.SetupActivity("PromptActivity", new PromptResultado { Modelo = "gpt-5-mini", Resultado = "ok" });

        await orchestrator.RunOrchestrator(context);

        // Sin necesidadesSinResultado serian 2: Completo() en el 2.76 y Paginas(3) en el 2.8.
        context.GetActivityCallCount("ObtenerMarkdownActivity").Should().Be(1);
        context.GetLastActivityInput<ObtenerMarkdownInput>("ObtenerMarkdownActivity")!
            .Necesidad.DocumentoCompleto.Should().BeTrue();
    }

    [Fact]
    public async Task RunOrchestrator_ExtraccionGptForzadaPorInstrucciones_AnticipaElCompleto()
    {
        // La peticion fuerza extraccion GPT-directa sobre una tipologia cuyo proveedor es CU. La
        // guarda real del Paso 3.5 usa el proveedor efectivo (instrucciones > tipologia), asi que
        // la anticipacion tiene que mirar lo mismo: si solo mirase el de la tipologia no
        // anticiparia y el Paso 3.5 acabaria pagando su propio Layout.
        var orchestrator = CreateOrchestrator();
        var entrada = BuildEntrada(expectedType: "nota.simple");
        entrada.Instrucciones.Extraction.Provider = "gpt";
        var context = new FakeTaskOrchestrationContext(entrada);
        context.SetupActivity("NormalizarActivity", BuildNormalizarResult());
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "container/test.pdf");
        context.SetupActivity("ResolverTipologiaActivity", BuildTipologia(extractionEnabled: true, extractionProvider: "cu"));
        context.SetupActivity("ObtenerMarkdownActivity", MarkdownResuelto("# documento entero", 7, completo: true));
        context.SetupActivity("ExtraerActivity", BuildExtraccionOk());
        context.SetupActivity("ValidarActivity", BuildValidacionOk());
        context.SetupActivity("IntegrarActivity", new global::DocumentIA.Core.Models.ResultadoIntegracion { Estado = "OK", DatosFinales = new Dictionary<string, object>() });

        await orchestrator.RunOrchestrator(context);

        context.GetActivityCallCount("ObtenerMarkdownActivity").Should().Be(1);
        context.GetLastActivityInput<ObtenerMarkdownInput>("ObtenerMarkdownActivity")!
            .Necesidad.DocumentoCompleto.Should().BeTrue();
        // Con el completo ya en la cache, el Paso 3.5 no vuelve a pagar Layout.
        context.GetActivityCallCount("ExtraerMarkdownLayoutActivity").Should().Be(0);
    }

    [Fact]
    public async Task RunOrchestrator_ExtraccionDeshabilitadaConProveedorGpt_NoAnticipaElCompleto()
    {
        // ExtractionProvider GPT en el JSON pero ExtractionEnabled=false: el Paso 3.5 no llega a
        // ejecutarse nunca, asi que anticipar el documento completo seria pagar un Layout que
        // nadie consume. La anticipacion debe mirar tambien ExtractionEnabled.
        var orchestrator = CreateOrchestrator();
        var context = new FakeTaskOrchestrationContext(BuildEntrada(expectedType: "nota.simple"));
        context.SetupActivity("NormalizarActivity", BuildNormalizarResult());
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "container/test.pdf");
        context.SetupActivity("ResolverTipologiaActivity", BuildTipologia(extractionEnabled: false, extractionProvider: "gpt"));
        context.SetupActivity("ObtenerMarkdownActivity", MarkdownResuelto("# documento entero", 7, completo: true));
        context.SetupActivity("ValidarActivity", BuildValidacionOk());
        context.SetupActivity("IntegrarActivity", new global::DocumentIA.Core.Models.ResultadoIntegracion { Estado = "OK", DatosFinales = new Dictionary<string, object>() });

        await orchestrator.RunOrchestrator(context);

        // Con ExpectedType informado el Paso 2.8 tampoco corre: la unica peticion posible era la
        // anticipada, y no debe producirse.
        context.GetActivityCallCount("ObtenerMarkdownActivity").Should().Be(0);
    }

    [Fact]
    public async Task RunOrchestrator_ExpectedTypeSinPromptNiExtraccionGpt_NoPideMarkdownAntesDeClasificar()
    {
        // Escenario (c): ExpectedType valido, sin prompt de tipologia y con extraccion CU. Ni el
        // Paso 2.76 ni el Paso 2.8 tienen motivo para pedir markdown antes de clasificar.
        var orchestrator = CreateOrchestrator();
        var context = new FakeTaskOrchestrationContext(BuildEntrada(expectedType: "nota.simple"));
        context.SetupActivity("NormalizarActivity", BuildNormalizarResult());
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "container/test.pdf");
        context.SetupActivity("ResolverTipologiaActivity", BuildTipologia(extractionEnabled: true, extractionProvider: "cu"));
        context.SetupActivity("ExtraerActivity", BuildExtraccionOk());
        context.SetupActivity("ValidarActivity", BuildValidacionOk());
        context.SetupActivity("IntegrarActivity", new global::DocumentIA.Core.Models.ResultadoIntegracion { Estado = "OK", DatosFinales = new Dictionary<string, object>() });

        await orchestrator.RunOrchestrator(context);

        context.GetActivityCallCount("ObtenerMarkdownActivity").Should().Be(0);
        context.GetActivityCallCount("ClasificarActivity").Should().Be(0);
    }

    [Fact]
    public async Task RunOrchestrator_ClassificationOnlyResumenForzadoSinPrompt_PideElRecorteNoElCompleto()
    {
        // Regla 6: el resumen forzado va con recorte, como la clasificacion.
        var orchestrator = CreateOrchestrator();
        var context = new FakeTaskOrchestrationContext(
            BuildEntrada(classificationOnly: true, forzarResumenPorDefecto: true, expectedType: "nota.simple"));
        context.SetupActivity("NormalizarActivity", BuildNormalizarResult());
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "container/test.pdf");
        context.SetupActivity("ResolverTipologiaActivity", BuildTipologia());
        context.SetupActivity("ObtenerMarkdownActivity", MarkdownResuelto("# recorte", 3, completo: false));
        context.SetupActivity("PromptActivity", new PromptResultado { Modelo = "gpt-4o-mini", Resumen = "Resumen" });

        var salida = await orchestrator.RunOrchestrator(context);

        salida.Resultado.Estado.Should().Be("OK", $"error real: {salida.Resultado.MensajeError}");
        context.GetActivityCallCount("ObtenerMarkdownActivity").Should().Be(1);
        var input = context.GetLastActivityInput<ObtenerMarkdownInput>("ObtenerMarkdownActivity");
        input!.Necesidad.DocumentoCompleto.Should().BeFalse();
        input.Necesidad.PaginasMinimas.Should().Be(3);
        salida.DetalleEjecucion.OrigenMarkdown.Should().Be("LayoutResumenClassificationOnly");
    }

    [Fact]
    public async Task RunOrchestrator_CacheCompleta_UnaNecesidadDePaginasNoLlamaAlResolutor()
    {
        // Se usa el mayor, sin recortar: con el completo en cache, nada vuelve a pedir markdown.
        var orchestrator = CreateOrchestrator();
        var entrada = BuildEntrada();
        entrada.Instrucciones.Prompt = new PromptInstrucciones { UserPromptTemplate = "Resume:\n\n{contenido}" };
        entrada.Instrucciones.ForzarResumenPorDefecto = true;
        var context = new FakeTaskOrchestrationContext(entrada);
        context.SetupActivity("NormalizarActivity", BuildNormalizarResult());
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "container/test.pdf");
        context.SetupActivity("ObtenerMarkdownActivity", MarkdownResuelto("# entero", 14, completo: true));
        context.SetupActivity("ClasificarActivity", BuildClasificacionOk());
        context.SetupActivity("ResolverTipologiaActivity", BuildTipologia(extractionEnabled: true));
        context.SetupActivity("ExtraerActivity", new global::DocumentIA.Core.Models.ExtraccionResultado { Modelo = "gpt", DatosExtraidos = new Dictionary<string, object>() });
        context.SetupActivity("ValidarActivity", BuildValidacionOk());
        context.SetupActivity("IntegrarActivity", new global::DocumentIA.Core.Models.ResultadoIntegracion { Estado = "OK", DatosFinales = new Dictionary<string, object>() });
        context.SetupActivity("PromptActivity", new PromptResultado { Modelo = "gpt-5-mini", Resultado = "r", Resumen = "s" });

        await orchestrator.RunOrchestrator(context);

        context.GetActivityCallCount("ObtenerMarkdownActivity").Should().Be(1);
        context.GetLastActivityInput<PromptActivityInput>("PromptActivity")!.MarkdownExtraido.Should().Be("# entero");
    }

    [Fact]
    public async Task RunOrchestrator_SufijoDeResumenParcial_DependeDeLaCoberturaDelMarkdown()
    {
        var orchestrator = CreateOrchestrator();
        var context = new FakeTaskOrchestrationContext(BuildEntrada(classificationOnly: true));
        // Sin "Paginas" en la normalizacion: las paginas del documento las informa el recorte
        // (TotalPaginas=10). Con el 1 por defecto del helper, 3 < 1 seria falso y el test pasaria
        // sin ejercer la regla del sufijo.
        var normalizadoSinPaginas = BuildNormalizarResult();
        normalizadoSinPaginas.Remove("Paginas");
        context.SetupActivity("NormalizarActivity", normalizadoSinPaginas);
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "container/test.pdf");
        context.SetupActivity("PrepararDocumentoClasificacionActivity", new PrepararDocumentoClasificacionResultado
        {
            DocumentoBase64Clasif = "cmVjb3J0YWRv", TotalPaginas = 10, PaginasIncluidas = 3, RecorteAplicado = true
        });
        // El resolutor devuelve el documento completo (por ejemplo, ya estaba en BD).
        context.SetupActivity("ObtenerMarkdownActivity", MarkdownResuelto("# entero", 10, completo: true, FuenteMarkdown.BaseDatos));
        var clasificacion = BuildClasificacionOk();
        clasificacion.ResumenCombinado = "Resumen del documento";
        context.SetupActivity("ClasificarActivity", clasificacion);
        context.SetupActivity("ResolverTipologiaActivity", BuildTipologia());

        var salida = await orchestrator.RunOrchestrator(context);

        salida.DatosExtraidos["Resumen"].Should().Be("Resumen del documento", "con el completo el sufijo de 'primeras N paginas' no seria verdad");
    }

    [Fact]
    public async Task RunOrchestrator_AnticipacionDelCompletoSinResultado_NoSeReintentaEnElPromptLibre()
    {
        // Regresion AB#100029 reabierta por la anticipacion del Paso 2.76 (AB#100252): si el
        // resolutor no consigue el documento completo, el prompt libre no puede volver a pagar
        // exactamente la misma peticion para volver a fallar. El corte lo da la guarda de
        // necesidades sin resultado, y el orquestador ya no tiene ninguna via directa a DI Layout.
        var orchestrator = CreateOrchestrator();
        var entrada = BuildEntrada(expectedType: "resumen.documental");
        entrada.Instrucciones.Prompt = new PromptInstrucciones
        {
            UserPromptTemplate = "Resume el documento:\n\n{contenido}"
        };
        var context = new FakeTaskOrchestrationContext(entrada);

        context.SetupActivity("NormalizarActivity", BuildNormalizarResult());
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "container/test.pdf");
        context.SetupActivity("ResolverTipologiaActivity", new ResolvedTipologia(
            RequestedValue: "resumen.documental",
            TipologiaId: "resumen.documental",
            Version: "1.0",
            TechnicalKey: "resumen.documental",
            IsDefault: false,
            SkipGDCUpload: true,
            PromptEnabled: false,
            ExtractionEnabled: false));
        // El resolutor responde sin contenido (no lanza): la necesidad queda registrada como no
        // cubierta y ningun paso posterior debe repetirla.
        context.SetupActivity("ObtenerMarkdownActivity", new ResultadoMarkdown { Fuente = FuenteMarkdown.Ninguna });
        context.SetupActivity("PromptActivity", new PromptResultado
        {
            Modelo = "gpt-5-mini",
            Resultado = "Respuesta sin contenido.",
            TiempoMs = 400
        });

        var salida = await orchestrator.RunOrchestrator(context);

        context.GetActivityCallCount("ObtenerMarkdownActivity").Should().Be(
            1,
            "la anticipacion ya pidio el documento completo y no lo consiguio: repetirlo pagaria Layout dos veces para fallar dos");
        context.GetActivityCallCount("ExtraerMarkdownLayoutActivity").Should().Be(
            0,
            "el orquestador no vuelve a invocar DI Layout por su cuenta: todo el markdown pasa por el resolutor");
        context.GetLastActivityInput<PromptActivityInput>("PromptActivity")!.MarkdownExtraido.Should().BeNull();
        salida.DetalleEjecucion.MarkdownGenerado.Should().BeFalse();
    }
}
