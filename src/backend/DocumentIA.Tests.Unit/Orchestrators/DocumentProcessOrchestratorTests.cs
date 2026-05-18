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
/// Implementación fake de TaskOrchestrationContext para pruebas unitarias del orquestador.
/// Permite inyectar resultados o excepciones por nombre de actividad.
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

    /// <summary>Configura el resultado para una actividad por nombre.</summary>
    public void SetupActivity<T>(string activityName, T result)
        => _activityResults[activityName] = result;

    /// <summary>Configura que una actividad lance una excepción.</summary>
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

    // ── Abstract overrides ──────────────────────────────────────────────────
    public override TaskName Name => new TaskName("DocumentProcessOrchestrator");
    public override string InstanceId => "fake-instance-001";
    public override DateTime CurrentUtcDateTime => new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    public override bool IsReplaying => false;

    public override T? GetInput<T>() where T : default => (T?)(object?)_input;

    public override Task<TResult> CallActivityAsync<TResult>(
        TaskName name, object? input = null, TaskOptions? options = null)
    {
        if (!_activityInputs.TryGetValue(name.Name, out var inputs))
        {
            inputs = new List<object?>();
            _activityInputs[name.Name] = inputs;
        }

        inputs.Add(input);

        if (_activityThrows.TryGetValue(name.Name, out var ex)) throw ex;
        if (_activityResults.TryGetValue(name.Name, out var result))
            return Task.FromResult((TResult)result!);
        return Task.FromResult(default(TResult)!);
    }

    public override Task CreateTimer(DateTime fireAt, CancellationToken cancellationToken)
        // Para pruebas, el timer "nunca" completa solo para que WhenAny use la actividad real.
        => Task.Delay(Timeout.Infinite, cancellationToken);

    public override void SetCustomStatus(object? customStatus) { }

    public override ILogger CreateReplaySafeLogger(string categoryName)
        => NullLogger.Instance;

    // Void variant of CallActivityAsync
    public override Task CallActivityAsync(TaskName name, object? input = null, TaskOptions? options = null)
    {
        if (!_activityInputs.TryGetValue(name.Name, out var inputs))
        {
            inputs = new List<object?>();
            _activityInputs[name.Name] = inputs;
        }

        inputs.Add(input);

        if (_activityThrows.TryGetValue(name.Name, out var ex)) throw ex;
        return Task.CompletedTask;
    }

    // ── Members not used by the orchestrator under test ──────────────────────
    protected override ILoggerFactory LoggerFactory => NullLoggerFactory.Instance;
    public override Guid NewGuid() => Guid.NewGuid();
    public override ParentOrchestrationInstance? Parent => null;

    public override Task<TResult> CallSubOrchestratorAsync<TResult>(
        TaskName orchestratorName, object? input = null, TaskOptions? options = null)
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
    // ── Helpers ──────────────────────────────────────────────────────────────
    private static DocumentProcessOrchestrator CreateOrchestrator(ClassificationPreparationSettings? settings = null)
    {
        // El constructor string solo carga el fichero cuando se llama Load().
        // Para los escenarios que no llegan a la extracción, nunca se invoca.
        var loader = new ExtractionModelRegistryLoader("dummy.json");
        var configuredSettings = settings ?? new ClassificationPreparationSettings();
        return new DocumentProcessOrchestrator(loader, Options.Create(configuredSettings));
    }

    private static ContratoEntrada BuildEntrada(
        string nombre = "test.pdf",
        string? objectIdGdc = null,
        string? expectedType = null,
        bool skipDuplicateCheck = false,
        bool classificationOnly = false,
        bool? executeIntegrarWhenClassificationOnly = null,
        int maxPagesForClassificationOnly = 0)
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
        ["MD5"]    = "md5abc",
        ["CRC32"]  = "crc32abc",
        ["Paginas"] = 1
    };

    private static ResolvedTipologia BuildTipologia(
        bool extractionEnabled = false,
        bool skipGdc = true,
        bool assetResolverEnabled = false)
        => new(
            RequestedValue: "nota.simple",
            TipologiaId: "nota.simple",
            Version: "1.0",
            TechnicalKey: "nota.simple.1_0",
            IsDefault: true,
            ExtractionEnabled: extractionEnabled,
            SkipGDCUpload: skipGdc,
            AssetResolverEnabled: assetResolverEnabled);

    // ── Static helper tests (pre-existing) ──────────────────────────────────
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
                        Idufir = "IDUFIR-OVERRIDE",
                        ReferenciaCatastral = "REFCAT-OVERRIDE"
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
        input.IdufirOverride.Should().Be("IDUFIR-OVERRIDE");
        input.ReferenciaCatastralOverride.Should().Be("REFCAT-OVERRIDE");
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

    // ── Full orchestrator flow tests (T-1) ───────────────────────────────────

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
    public async Task RunOrchestrator_ClasificarFallaConTipologiaNoIdentificada_RetornaEstadoError()
    {
        var orchestrator = CreateOrchestrator();
        // Sin ExpectedType → el orquestador llama a ClasificarActivity
        var context = new FakeTaskOrchestrationContext(BuildEntrada());

        context.SetupActivity("NormalizarActivity", BuildNormalizarResult());
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "container/test.pdf");
        context.SetupActivityThrow("ClasificarActivity",
            new Exception("No se ha podido identificar la tipologia del documento"));

        var salida = await orchestrator.RunOrchestrator(context);

        salida.Resultado.Estado.Should().Be("ERROR");
        salida.Resultado.MensajeError.Should().Contain("tipologia");
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

        salida.Resultado.Estado.Should().Be("ERROR");

        var prepInput = context.GetLastActivityInput<PrepararDocumentoClasificacionInput>("PrepararDocumentoClasificacionActivity");
        prepInput.Should().NotBeNull();
        prepInput!.MaxPaginasClasificacion.Should().Be(7);
    }

    [Fact]
    public async Task RunOrchestrator_TipologiaNoResuelta_RetornaEstadoError()
    {
        var orchestrator = CreateOrchestrator();
        // Con ExpectedType → el orquestador salta ClasificarActivity y llama ResolverTipologiaActivity
        var context = new FakeTaskOrchestrationContext(BuildEntrada(expectedType: "nota.simple"));

        context.SetupActivity("NormalizarActivity", BuildNormalizarResult());
        context.SetupActivity("VerificarDuplicadoActivity", false);
        context.SetupActivity("SubirBlobActivity", "container/test.pdf");
        context.SetupActivityThrow("ResolverTipologiaActivity",
            new KeyNotFoundException("No existe la tipologia: nota.simple"));

        var salida = await orchestrator.RunOrchestrator(context);

        salida.Resultado.Estado.Should().Be("ERROR");
        salida.Resultado.MensajeError.Should().Contain("tipologia");
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
            Confianza = 0.1,          // por debajo del umbral 0.6
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
        var context = new FakeTaskOrchestrationContext(BuildEntrada(
            classificationOnly: true,
            maxPagesForClassificationOnly: 3));

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

        salida.Resultado.Estado.Should().Be("OK");
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
}
