#nullable enable
using System.Text.Json;
using DocumentIA.Core.Configuration;
using DocumentIA.Core.Models;
using DocumentIA.Core.Services;
using DocumentIA.Data.Entities;
using DocumentIA.Data.Repositories;
using DocumentIA.Functions.Activities;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DocumentIA.Tests.Unit.Activities;

/// <summary>
/// Seleccion de la historica por clave de intencion (AB#100930): misma clave reutiliza; clave
/// distinta, modelo de extraccion distinto o paginas insuficientes no; una candidata invalida
/// deja paso a la siguiente; las filas antiguas se derivan y se graban.
/// </summary>
public class ObtenerUltimaEjecucionDuplicadoActivityClaveTests
{
    private const string Sha = "sha-clave";
    private const string NivelDefault = "TDN1_TDN2";
    private static readonly DocumentoEntity Documento = new() { Id = 10, SHA256 = Sha };

    private readonly Mock<IDocumentoRepository> _documentos = new(MockBehavior.Strict);
    private readonly Mock<IDocumentoEjecucionRepository> _ejecuciones = new(MockBehavior.Strict);
    private readonly Mock<ITipologiaRepository> _tipologias = new(MockBehavior.Strict);

    public ObtenerUltimaEjecucionDuplicadoActivityClaveTests()
    {
        _documentos.Setup(r => r.GetBySHA256Async(Sha)).ReturnsAsync(Documento);
        _tipologias.Setup(r => r.GetByCodigoAsync("tasacion")).ReturnsAsync(TipologiaPublicada("tasacion", "cu.tasacion"));
    }

    [Fact]
    public async Task MismaClave_Reutiliza_YMarcaClaveIgual()
    {
        var clave = ClaveSolo();
        var fila = Fila(1, clave.Clave, Contrato(classificationOnly: true, paginasIncluidas: 3, recorte: true));
        _ejecuciones.Setup(r => r.GetCandidatasPorClaveAsync(10, clave.Clave, 0, 1)).ReturnsAsync(new[] { fila });

        var r = await Sut().Run(Input(clave.Clave, documentoCompleto: false, paginas: 3, classificationOnly: true));

        r.Salida.Should().NotBeNull();
        r.ModoEstricto.Should().BeTrue();
        r.MotivoNoReutilizacion.Should().BeNull();
        r.Salida!.DetalleEjecucion.MotivoReutilizacion.Should().Be(MotivosReutilizacion.ClaveIgual);
        r.Salida.DetalleEjecucion.EjecucionOriginalGuid.Should().Be("guid-1");
        r.Salida.Resultado.ReutilizadaPorDuplicado.Should().BeTrue();
    }

    [Fact]
    public async Task SinFilasConEsaClave_PeroConOtras_ClaveDistinta()
    {
        var clave = ClaveSolo();
        _ejecuciones.Setup(r => r.GetCandidatasPorClaveAsync(10, clave.Clave, 0, 1)).ReturnsAsync(Array.Empty<DocumentoEjecucionEntity>());
        _ejecuciones.Setup(r => r.GetConContratoSinClaveAsync(10, 50)).ReturnsAsync(Array.Empty<DocumentoEjecucionEntity>());
        _ejecuciones.Setup(r => r.TieneFallidasPorClaveAsync(10, clave.Clave)).ReturnsAsync(false);
        _ejecuciones.Setup(r => r.TieneEjecucionesConContratoAsync(10)).ReturnsAsync(true);

        var r = await Sut().Run(Input(clave.Clave, false, 3, true));

        r.Salida.Should().BeNull();
        r.MotivoNoReutilizacion.Should().Be(MotivosReutilizacion.ClaveDistinta);
    }

    [Fact]
    public async Task DocumentoSinNingunaEjecucionConContrato_SinHistorica()
    {
        var clave = ClaveSolo();
        _ejecuciones.Setup(r => r.GetCandidatasPorClaveAsync(10, clave.Clave, 0, 1)).ReturnsAsync(Array.Empty<DocumentoEjecucionEntity>());
        _ejecuciones.Setup(r => r.GetConContratoSinClaveAsync(10, 50)).ReturnsAsync(Array.Empty<DocumentoEjecucionEntity>());
        _ejecuciones.Setup(r => r.TieneFallidasPorClaveAsync(10, clave.Clave)).ReturnsAsync(false);
        _ejecuciones.Setup(r => r.TieneEjecucionesConContratoAsync(10)).ReturnsAsync(false);

        var r = await Sut().Run(Input(clave.Clave, false, 3, true));

        r.MotivoNoReutilizacion.Should().Be(MotivosReutilizacion.SinHistorica);
    }

    [Fact]
    public async Task SinCandidatasPeroConFallidasDeLaMismaClave_HistoricaFallida()
    {
        // AB#100956: el filtro del repositorio ya ha dejado fuera las fallidas; la actividad
        // tiene que explicar que no reutiliza por eso, no por clave-distinta.
        var clave = ClaveSolo();
        _ejecuciones.Setup(r => r.GetCandidatasPorClaveAsync(10, clave.Clave, 0, 1)).ReturnsAsync(Array.Empty<DocumentoEjecucionEntity>());
        _ejecuciones.Setup(r => r.GetConContratoSinClaveAsync(10, 50)).ReturnsAsync(Array.Empty<DocumentoEjecucionEntity>());
        _ejecuciones.Setup(r => r.TieneFallidasPorClaveAsync(10, clave.Clave)).ReturnsAsync(true);

        var r = await Sut().Run(Input(clave.Clave, false, 3, true));

        r.Salida.Should().BeNull();
        r.ModoEstricto.Should().BeTrue();
        r.MotivoNoReutilizacion.Should().Be(MotivosReutilizacion.HistoricaFallida);
        _ejecuciones.Verify(r => r.TieneEjecucionesConContratoAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task DocumentoDesconocido_SinHistorica()
    {
        _documentos.Setup(r => r.GetBySHA256Async("sha-nuevo")).ReturnsAsync((DocumentoEntity?)null);

        var r = await Sut().Run(new ObtenerUltimaEjecucionDuplicadoInput { SHA256 = "sha-nuevo", ClaveIntencion = "k", PaginasMinimas = 3 });

        r.Salida.Should().BeNull();
        r.MotivoNoReutilizacion.Should().Be(MotivosReutilizacion.SinHistorica);
    }

    [Fact]
    public async Task ModoCompleto_ModeloDeExtraccionDistinto_NoReutiliza()
    {
        var clave = ClaveCompleto();
        var fila = Fila(1, clave.Clave, Contrato(classificationOnly: false, modelKeyExtraccion: "cu.tasacion.v1"));
        _ejecuciones.Setup(r => r.GetCandidatasPorClaveAsync(10, clave.Clave, 0, 1)).ReturnsAsync(new[] { fila });
        _ejecuciones.Setup(r => r.GetCandidatasPorClaveAsync(10, clave.Clave, 1, 4)).ReturnsAsync(Array.Empty<DocumentoEjecucionEntity>());

        var r = await Sut().Run(Input(clave.Clave, true, 0, false));

        r.Salida.Should().BeNull();
        r.MotivoNoReutilizacion.Should().Be(MotivosReutilizacion.ModeloExtraccionDistinto);
    }

    [Fact]
    public async Task ModoCompleto_TipologiaNoPublicada_NoReutiliza()
    {
        var clave = ClaveCompleto();
        _tipologias.Setup(r => r.GetByCodigoAsync("retirada")).ReturnsAsync((TipologiaEntity?)null);
        var fila = Fila(1, clave.Clave, Contrato(classificationOnly: false, tipologia: "retirada", modelKeyExtraccion: "cu.x"));
        _ejecuciones.Setup(r => r.GetCandidatasPorClaveAsync(10, clave.Clave, 0, 1)).ReturnsAsync(new[] { fila });
        _ejecuciones.Setup(r => r.GetCandidatasPorClaveAsync(10, clave.Clave, 1, 4)).ReturnsAsync(Array.Empty<DocumentoEjecucionEntity>());

        var r = await Sut().Run(Input(clave.Clave, true, 0, false));

        r.MotivoNoReutilizacion.Should().Be(MotivosReutilizacion.ModeloExtraccionDistinto);
    }

    [Fact]
    public async Task ModoCompleto_MismoModeloDeExtraccion_Reutiliza()
    {
        var clave = ClaveCompleto();
        var fila = Fila(1, clave.Clave, Contrato(classificationOnly: false, modelKeyExtraccion: "cu.tasacion"));
        _ejecuciones.Setup(r => r.GetCandidatasPorClaveAsync(10, clave.Clave, 0, 1)).ReturnsAsync(new[] { fila });

        var r = await Sut().Run(Input(clave.Clave, true, 0, false));

        r.Salida.Should().NotBeNull();
    }

    [Fact]
    public async Task ModoCompleto_ModeloSecundarioDeLaTipologia_Reutiliza()
    {
        _tipologias.Setup(r => r.GetByCodigoAsync("tasacion")).ReturnsAsync(TipologiaPublicada("tasacion", "cu.tasacion", "cu.tasacion.b"));
        var clave = ClaveCompleto();
        var fila = Fila(1, clave.Clave, Contrato(classificationOnly: false, modelKeyExtraccion: "cu.tasacion.b"));
        _ejecuciones.Setup(r => r.GetCandidatasPorClaveAsync(10, clave.Clave, 0, 1)).ReturnsAsync(new[] { fila });

        var r = await Sut().Run(Input(clave.Clave, true, 0, false));

        r.Salida.Should().NotBeNull();
    }

    [Fact]
    public async Task ModoCompleto_ModeloPedidoManda_SobreElSecundario()
    {
        _tipologias.Setup(r => r.GetByCodigoAsync("tasacion")).ReturnsAsync(TipologiaPublicada("tasacion", "cu.tasacion", "cu.tasacion.b"));
        var clave = ClaveCompleto();
        var fila = Fila(1, clave.Clave, Contrato(classificationOnly: false, modelKeyExtraccion: "cu.tasacion.b"));
        _ejecuciones.Setup(r => r.GetCandidatasPorClaveAsync(10, clave.Clave, 0, 1)).ReturnsAsync(new[] { fila });
        _ejecuciones.Setup(r => r.GetCandidatasPorClaveAsync(10, clave.Clave, 1, 4)).ReturnsAsync(Array.Empty<DocumentoEjecucionEntity>());
        var input = Input(clave.Clave, true, 0, false);
        input.ExtraccionModelKeyPedido = "cu.tasacion";

        var r = await Sut().Run(input);

        r.Salida.Should().BeNull();
        r.MotivoNoReutilizacion.Should().Be(MotivosReutilizacion.ModeloExtraccionDistinto);
    }

    [Fact]
    public async Task SoloClasificacion_PaginasInsuficientes_NoReutiliza()
    {
        var clave = ClaveSolo();
        var fila = Fila(1, clave.Clave, Contrato(classificationOnly: true, paginasIncluidas: 3, recorte: true));
        _ejecuciones.Setup(r => r.GetCandidatasPorClaveAsync(10, clave.Clave, 0, 1)).ReturnsAsync(new[] { fila });
        _ejecuciones.Setup(r => r.GetCandidatasPorClaveAsync(10, clave.Clave, 1, 4)).ReturnsAsync(Array.Empty<DocumentoEjecucionEntity>());

        var r = await Sut().Run(Input(clave.Clave, false, 6, true));

        r.Salida.Should().BeNull();
        r.MotivoNoReutilizacion.Should().Be(MotivosReutilizacion.PaginasInsuficientes);
    }

    [Fact]
    public async Task CandidataInvalida_PasaALaSiguiente()
    {
        var clave = ClaveSolo();
        var rota = Fila(2, clave.Clave, "{no json");
        var buena = Fila(1, clave.Clave, Contrato(classificationOnly: true, paginasIncluidas: 3, recorte: true));
        _ejecuciones.Setup(r => r.GetCandidatasPorClaveAsync(10, clave.Clave, 0, 1)).ReturnsAsync(new[] { rota });
        _ejecuciones.Setup(r => r.GetCandidatasPorClaveAsync(10, clave.Clave, 1, 4)).ReturnsAsync(new[] { buena });

        var r = await Sut().Run(Input(clave.Clave, false, 3, true));

        r.Salida.Should().NotBeNull();
        r.Salida!.DetalleEjecucion.EjecucionOriginalGuid.Should().Be("guid-1");
    }

    [Fact]
    public async Task TodasInvalidas_ContratoInvalido()
    {
        var clave = ClaveSolo();
        _ejecuciones.Setup(r => r.GetCandidatasPorClaveAsync(10, clave.Clave, 0, 1)).ReturnsAsync(new[] { Fila(2, clave.Clave, "{no json") });
        _ejecuciones.Setup(r => r.GetCandidatasPorClaveAsync(10, clave.Clave, 1, 4)).ReturnsAsync(Array.Empty<DocumentoEjecucionEntity>());

        var r = await Sut().Run(Input(clave.Clave, false, 3, true));

        r.MotivoNoReutilizacion.Should().Be(MotivosReutilizacion.ContratoInvalido);
    }

    [Fact]
    public async Task FilasAntiguasSinClave_SeDerivanSeGrabanYSeUsanSiCoinciden()
    {
        var clave = ClaveSolo();
        var antiguaIgual = Fila(1, null, Contrato(classificationOnly: true, paginasIncluidas: 3, recorte: true));
        var antiguaDistinta = Fila(2, null, Contrato(classificationOnly: false));
        var derivadaDistinta = ClaveIntencionDerivador.DesdeContrato(antiguaDistinta.ContratoSalidaCompletoJson, NivelDefault)!;

        _ejecuciones.Setup(r => r.GetCandidatasPorClaveAsync(10, clave.Clave, 0, 1)).ReturnsAsync(Array.Empty<DocumentoEjecucionEntity>());
        _ejecuciones.Setup(r => r.GetConContratoSinClaveAsync(10, 50)).ReturnsAsync(new[] { antiguaDistinta, antiguaIgual });
        _ejecuciones.Setup(r => r.ActualizarClaveIntencionAsync(1, clave.Clave, It.IsAny<string>())).Returns(Task.CompletedTask);
        _ejecuciones.Setup(r => r.ActualizarClaveIntencionAsync(2, derivadaDistinta.Clave, It.IsAny<string>())).Returns(Task.CompletedTask);

        var r = await Sut().Run(Input(clave.Clave, false, 3, true));

        r.Salida.Should().NotBeNull();
        r.Salida!.DetalleEjecucion.EjecucionOriginalGuid.Should().Be("guid-1");
        _ejecuciones.Verify(r => r.ActualizarClaveIntencionAsync(1, clave.Clave, It.IsAny<string>()), Times.Once);
        _ejecuciones.Verify(r => r.ActualizarClaveIntencionAsync(2, derivadaDistinta.Clave, It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task ModoEstrictoDesactivado_ReproduceLaReglaAnterior()
    {
        // Historica con otro modo: la regla anterior cae a la ultima con contrato (AB#100177).
        var historica = Fila(1, null, Contrato(classificationOnly: false));
        _ejecuciones.Setup(r => r.GetByDocumentoIdAsync(10)).ReturnsAsync(new[] { historica });

        var r = await Sut(estricto: false).Run(Input("cualquiera", false, 3, classificationOnly: true));

        r.ModoEstricto.Should().BeFalse();
        r.Salida.Should().NotBeNull();
        r.MotivoNoReutilizacion.Should().BeNull();
        r.Salida!.DetalleEjecucion.MotivoReutilizacion.Should().BeNull();
    }

    [Fact]
    public async Task ModoEstrictoDesactivado_TodasLasHistoricasEnFallo_NoReutilizaYMotivaHistoricaFallida()
    {
        // AB#100956: tambien con la regla anterior un ERROR no se sirve; el orquestador reprocesa.
        var fallida = Fila(1, null, Contrato(classificationOnly: true));
        fallida.EstadoFinal = "ERROR";
        _ejecuciones.Setup(r => r.GetByDocumentoIdAsync(10)).ReturnsAsync(new[] { fallida });

        var r = await Sut(estricto: false).Run(Input("cualquiera", false, 3, classificationOnly: true));

        r.ModoEstricto.Should().BeFalse();
        r.Salida.Should().BeNull();
        r.MotivoNoReutilizacion.Should().Be(MotivosReutilizacion.HistoricaFallida);
    }

    [Fact]
    public async Task ModoEstrictoDesactivado_UltimaEnFalloYAnteriorValida_ReutilizaLaValida()
    {
        var valida = Fila(1, null, Contrato(classificationOnly: true));
        var fallida = Fila(2, null, Contrato(classificationOnly: true));
        fallida.EstadoFinal = "PENDIENTE_REINTENTO";
        _ejecuciones.Setup(r => r.GetByDocumentoIdAsync(10)).ReturnsAsync(new[] { fallida, valida });

        var r = await Sut(estricto: false).Run(Input("cualquiera", false, 3, classificationOnly: true));

        r.Salida.Should().NotBeNull();
        r.MotivoNoReutilizacion.Should().BeNull();
        r.Salida!.DetalleEjecucion.EjecucionOriginalGuid.Should().Be("guid-1");
    }

    [Fact]
    public async Task ModoEstrictoDesactivado_SinNingunContrato_SinMotivo()
    {
        // Sin historica con contrato la regla anterior sigue devolviendo DUPLICADO vacio (AB#100178).
        _ejecuciones.Setup(r => r.GetByDocumentoIdAsync(10)).ReturnsAsync(Array.Empty<DocumentoEjecucionEntity>());

        var r = await Sut(estricto: false).Run(Input("cualquiera", false, 3, true));

        r.Salida.Should().BeNull();
        r.MotivoNoReutilizacion.Should().BeNull();
    }

    [Fact]
    public async Task SinClaveEnModoEstricto_NoReutilizaYAvisa()
    {
        var r = await Sut().Run(Input(null, false, 3, true));

        r.Salida.Should().BeNull();
        r.MotivoNoReutilizacion.Should().Be(MotivosReutilizacion.ClaveDistinta);
    }

    private ObtenerUltimaEjecucionDuplicadoActivity Sut(bool estricto = true) => new(
        new Mock<ILogger<ObtenerUltimaEjecucionDuplicadoActivity>>().Object,
        _documentos.Object,
        _ejecuciones.Object,
        _tipologias.Object,
        Options.Create(new DedupOptions { ModoEstricto = estricto, MaxCandidatas = 5, MaxDerivacionAlVuelo = 50 }));

    private static ObtenerUltimaEjecucionDuplicadoInput Input(string? clave, bool documentoCompleto, int paginas, bool classificationOnly) => new()
    {
        SHA256 = Sha,
        ClassificationOnly = classificationOnly,
        NivelClasificacion = NivelDefault,
        ClaveIntencion = clave,
        DocumentoCompleto = documentoCompleto,
        PaginasMinimas = paginas
    };

    private static ClaveIntencion ClaveSolo() => ClaveIntencionBuilder.Construir(
        ClaveIntencionBuilder.Normalizar(true, NivelDefault, NivelDefault, null, null, "gpt-4o-mini"));

    private static ClaveIntencion ClaveCompleto() => ClaveIntencionBuilder.Construir(
        ClaveIntencionBuilder.Normalizar(false, NivelDefault, NivelDefault, null, null, "gpt-4o-mini"));

    private static DocumentoEjecucionEntity Fila(int id, string? clave, string contrato) => new()
    {
        Id = id,
        DocumentoId = 10,
        EjecucionGuid = $"guid-{id}",
        EstadoFinal = "OK",
        ClaveIntencion = clave,
        ContratoSalidaCompletoJson = contrato,
        FechaEjecucion = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc).AddHours(id)
    };

    private static string Contrato(
        bool classificationOnly, string tipologia = "tasacion", string? modelKeyExtraccion = null,
        int paginasIncluidas = 0, bool recorte = false)
    {
        var salida = new ContratoSalida
        {
            Identificacion = new Identificacion { Documento = "doc.pdf", Tipologia = tipologia },
            Resultado = new ResultadoFinal { Estado = "OK", ConfianzaGlobal = 0.9 },
            DetalleEjecucion = new DetalleEjecucion
            {
                ClassificationOnly = classificationOnly,
                NivelClasificacion = NivelDefault,
                Clasificacion = new ResultadoClasificacion { Modelo = "gpt-4o-mini", Confianza = 0.9 },
                Extraccion = new ResultadoExtraccion { Modelo = classificationOnly ? "skipped" : "cu", ModelKeyEfectivo = modelKeyExtraccion },
                RecorteAplicado = recorte,
                PaginasIncluidas = paginasIncluidas
            }
        };
        return JsonSerializer.Serialize(salida);
    }

    private static TipologiaEntity TipologiaPublicada(string codigo, string modelKey, string? secondaryModelKey = null) => new()
    {
        Codigo = codigo,
        Activa = true,
        Estado = EstadoTipologia.Published,
        ConfiguracionJson = $$$"""{"tipologiaId":"{{{codigo}}}","extraction":{"enabled":true,"provider":"azure-content-understanding","modelKey":"{{{modelKey}}}"{{{(secondaryModelKey is null ? "" : $",\"secondaryModelKey\":\"{secondaryModelKey}\"")}}}}}"""
    };
}
