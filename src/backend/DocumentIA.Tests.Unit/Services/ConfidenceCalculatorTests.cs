using DocumentIA.Core.Configuration;
using DocumentIA.Core.Services;
using FluentAssertions;

namespace DocumentIA.Tests.Unit.Services;

public class ConfidenceCalculatorTests
{
    private static readonly ConfidenceConfig DefaultCfg = new();

    // ─── ClasifFinal ───────────────────────────────────────────────────────────

    [Fact]
    public void ClasifFinal_NofallBack_ReturnsDiConf()
    {
        var result = ConfidenceCalculator.ClasifFinal(diConf: 0.92, gptConf: null, fallbackUsado: false);
        result.Should().Be(0.92);
    }

    [Fact]
    public void ClasifFinal_FallbackUsed_ReturnsGptConf()
    {
        var result = ConfidenceCalculator.ClasifFinal(diConf: 0.60, gptConf: 0.88, fallbackUsado: true);
        result.Should().Be(0.88);
    }

    [Fact]
    public void ClasifFinal_FallbackUsed_NullGpt_FallsBackToDi()
    {
        var result = ConfidenceCalculator.ClasifFinal(diConf: 0.70, gptConf: null, fallbackUsado: true);
        result.Should().Be(0.70);
    }

    [Fact]
    public void ClasifFinal_BothNull_FallbackUsed_Returns0_5()
    {
        var result = ConfidenceCalculator.ClasifFinal(diConf: null, gptConf: null, fallbackUsado: true);
        result.Should().Be(0.5);
    }

    // ─── ExtracCU ──────────────────────────────────────────────────────────────

    [Fact]
    public void ExtracCU_PerfectFieldConfs_Returns1()
    {
        var confs = new List<double?> { 1.0, 1.0, 1.0 };
        var (conf, _) = ConfidenceCalculator.ExtracCU(
            fieldConfs: confs,
            camposPresentes: 3, camposTotales: 3,
            camposRequeridos: 3, camposRequeridosPresentes: 3,
            warnings: 0);
        conf.Should().BeApproximately(1.0, 0.0001);
    }

    [Fact]
    public void ExtracCU_NoFieldConfs_UsesCamposRatio()
    {
        // Sin confs de campo: avg = 2/4 = 0.5; ratioReq = 2/2 = 1.0; penalWarn = 1.0
        // 0.5*0.5 + 0.3*1.0 + 0.2*1.0 = 0.25 + 0.30 + 0.20 = 0.75
        var (conf, metricas) = ConfidenceCalculator.ExtracCU(
            fieldConfs: null,
            camposPresentes: 2, camposTotales: 4,
            camposRequeridos: 2, camposRequeridosPresentes: 2,
            warnings: 0);
        conf.Should().BeApproximately(0.75, 0.0001);
        metricas.RatioRequeridos.Should().BeApproximately(1.0, 0.0001);
    }

    [Fact]
    public void ExtracCU_MissingRequiredFields_LowersConf()
    {
        // avg=1.0, ratioReq=0/3=0.0, penalWarn=1.0
        // 0.5*1.0 + 0.3*0.0 + 0.2*1.0 = 0.70
        var confs = new List<double?> { 1.0, 1.0, 1.0 };
        var (conf, _) = ConfidenceCalculator.ExtracCU(
            fieldConfs: confs,
            camposPresentes: 3, camposTotales: 3,
            camposRequeridos: 3, camposRequeridosPresentes: 0,
            warnings: 0);
        conf.Should().BeApproximately(0.70, 0.0001);
    }

    [Fact]
    public void ExtracCU_HighWarnings_LowersConf()
    {
        // avg=1.0, ratioReq=1.0; warnings=3/3 → penalWarn=0
        // 0.5*1.0 + 0.3*1.0 + 0.2*0.0 = 0.80
        var confs = new List<double?> { 1.0, 1.0, 1.0 };
        var (conf, _) = ConfidenceCalculator.ExtracCU(
            fieldConfs: confs,
            camposPresentes: 3, camposTotales: 3,
            camposRequeridos: 3, camposRequeridosPresentes: 3,
            warnings: 3);
        conf.Should().BeApproximately(0.80, 0.0001);
    }

    [Fact]
    public void ExtracCU_CustomWeights_AppliesCorrectly()
    {
        var cfg = new ConfidenceConfig
        {
            ExtracWeightCampos = 1.0,
            ExtracWeightRequeridos = 0.0,
            ExtracWeightWarnings = 0.0
        };
        var confs = new List<double?> { 0.6, 0.8 };
        var (conf, _) = ConfidenceCalculator.ExtracCU(
            fieldConfs: confs,
            camposPresentes: 2, camposTotales: 2,
            camposRequeridos: 0, camposRequeridosPresentes: 0,
            warnings: 0,
            cfg: cfg);
        conf.Should().BeApproximately(0.70, 0.0001);
    }

    // ─── ExtracGPT ─────────────────────────────────────────────────────────────

    [Fact]
    public void ExtracGPT_WithValue_ReturnsValue()
    {
        ConfidenceCalculator.ExtracGPT(0.75).Should().Be(0.75);
    }

    [Fact]
    public void ExtracGPT_Null_Returns0_6()
    {
        ConfidenceCalculator.ExtracGPT(null).Should().Be(0.6);
    }

    [Fact]
    public void ExtracGPT_Clamped_AboveOne()
    {
        ConfidenceCalculator.ExtracGPT(1.5).Should().Be(1.0);
    }

    // ─── Validacion ────────────────────────────────────────────────────────────

    [Fact]
    public void Validacion_NoErrors_Returns1()
    {
        ConfidenceCalculator.Validacion(errores: 0, reglasRequeridas: 10).Should().Be(1.0);
    }

    [Fact]
    public void Validacion_AllErrors_Returns0()
    {
        ConfidenceCalculator.Validacion(errores: 5, reglasRequeridas: 5).Should().Be(0.0);
    }

    [Fact]
    public void Validacion_ZeroRules_Returns1()
    {
        ConfidenceCalculator.Validacion(errores: 0, reglasRequeridas: 0).Should().Be(1.0);
    }

    // ─── Global ────────────────────────────────────────────────────────────────

    [Fact]
    public void Global_AllHigh_ReturnsMinimum()
    {
        ConfidenceCalculator.Global(clasif: 0.95, extraccion: 0.88, validacion: 0.92)
            .Should().Be(0.88);
    }

    [Fact]
    public void Global_ExtraccionNull_SkipsExtraction()
    {
        ConfidenceCalculator.Global(clasif: 0.90, extraccion: null, validacion: 0.80)
            .Should().Be(0.80);
    }

    [Fact]
    public void Global_ValidacionIsBottleneck()
    {
        ConfidenceCalculator.Global(clasif: 0.95, extraccion: 0.91, validacion: 0.30)
            .Should().Be(0.30);
    }

    // ─── EstadoCalidad ─────────────────────────────────────────────────────────

    [Fact]
    public void EstadoCalidad_AboveOkUmbral_ReturnsOK()
    {
        ConfidenceCalculator.EstadoCalidad(0.90, DefaultCfg).Should().Be("OK");
    }

    [Fact]
    public void EstadoCalidad_BetweenUmbrals_ReturnsRevision()
    {
        ConfidenceCalculator.EstadoCalidad(0.75, DefaultCfg).Should().Be("REVISION");
    }

    [Fact]
    public void EstadoCalidad_BelowRevisionUmbral_ReturnsError()
    {
        ConfidenceCalculator.EstadoCalidad(0.50, DefaultCfg).Should().Be("ERROR");
    }

    [Fact]
    public void EstadoCalidad_Exactly0_85_ReturnsOK()
    {
        ConfidenceCalculator.EstadoCalidad(0.85, DefaultCfg).Should().Be("OK");
    }

    [Fact]
    public void EstadoCalidad_Exactly0_70_ReturnsRevision()
    {
        ConfidenceCalculator.EstadoCalidad(0.70, DefaultCfg).Should().Be("REVISION");
    }

    [Fact]
    public void EstadoCalidad_NullConfig_UsesDefaults()
    {
        ConfidenceCalculator.EstadoCalidad(0.80, null).Should().Be("REVISION");
    }
}
