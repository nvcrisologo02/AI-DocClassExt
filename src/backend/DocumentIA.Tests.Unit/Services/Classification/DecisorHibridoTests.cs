using DocumentIA.Core.Models;
using DocumentIA.Core.Services.Classification;
using FluentAssertions;

namespace DocumentIA.Tests.Unit.Services.Classification;

public class DecisorHibridoTests
{
    // Catalogo con la grafia real de BD (TDN2 en minusculas): las comparaciones no distinguen mayusculas.
    private static readonly IReadOnlyDictionary<string, TipologiaPar> Catalogo = new[]
    {
        new TipologiaPar("a.01", "AAAA", "aaaa-01"),
        new TipologiaPar("a.02", "AAAA", "aaaa-02"),
        new TipologiaPar("b.01", "BBBB", "bbbb-01"),
        new TipologiaPar("c.01", "CCCC", "cccc-01"),
    }.ToDictionary(p => p.ClavePar, p => p, StringComparer.OrdinalIgnoreCase);

    private static ManifiestoModeloEmbeddings Manifiesto(bool calibrado = false) => new()
    {
        Version = "v1",
        Dimensiones = 8,
        Calibrado = calibrado,
        Tipologias = new List<TipologiaManifiesto>
        {
            new() { Codigo = "a.01", Tdn1 = "AAAA", Tdn2 = "AAAA-01" },
            new() { Codigo = "a.02", Tdn1 = "AAAA", Tdn2 = "AAAA-02" },
            new() { Codigo = "b.01", Tdn1 = "BBBB", Tdn2 = "BBBB-01" },
            new() { Codigo = "c.01", Tdn1 = "CCCC", Tdn2 = "CCCC-01" },
            // d.01 comparte par con a.01: dispara la puerta par_compartido
            new() { Codigo = "d.01", Tdn1 = "AAAA", Tdn2 = "AAAA-01" },
        }
    };

    /// <summary>Distribucion sintetica: AAAA gana con pA; dentro de AAAA, AAAA-01 con 0.7.</summary>
    private static DistribucionEmbeddings Dist(double pA = 0.8, double pB = 0.15, double pC = 0.05, string tdn2A = "AAAA-01")
    {
        var p1 = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["AAAA"] = pA, ["BBBB"] = pB, ["CCCC"] = pC };
        var p2 = new Dictionary<string, IReadOnlyDictionary<string, double>>(StringComparer.OrdinalIgnoreCase)
        {
            ["AAAA"] = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["AAAA-01"] = 0.7, ["AAAA-02"] = 0.3 },
            ["BBBB"] = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["BBBB-01"] = 1.0 },
            ["CCCC"] = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["CCCC-01"] = 1.0 },
        };
        var ganadora = p1.OrderByDescending(kv => kv.Value).First().Key;
        return new DistribucionEmbeddings
        {
            ProbTdn1 = p1,
            ProbTdn2 = p2,
            Tdn1 = ganadora,
            ConfianzaTdn1 = p1[ganadora],
            Tdn2 = ganadora == "AAAA" ? tdn2A : $"{ganadora}-01"
        };
    }

    private static ParametrosDecision Params(string modo, double umbral = 0.6, string modoRestringido = "sombra",
        IReadOnlyList<string>? restriccion = null, bool expectedType = false) => new()
        {
            Modo = modo,
            UmbralConfianza = umbral,
            ModoRestringido = modoRestringido,
            UmbralMasa = 0.5,
            UmbralConfianzaCondicionada = 0.8,
            ExpectedTypeInformado = expectedType,
            RestriccionCodigos = restriccion
        };

    [Fact]
    public void ModoEfectivo_UsaElModoRestringidoSoloConRestriccion()
    {
        DecisorHibrido.ModoEfectivo("hibrido", "off", restringida: false).Should().Be("hibrido");
        DecisorHibrido.ModoEfectivo("hibrido", "off", restringida: true).Should().Be("off");
        DecisorHibrido.ModoEfectivo("off", "sombra", restringida: true).Should().Be("sombra");
    }

    [Fact]
    public void Off_DevuelveOmitido()
    {
        var d = DecisorHibrido.Decidir(Dist(), Manifiesto(), Params("off"), Catalogo);
        d.Decision.Should().Be(DecisionesEmbeddings.Omitido);
        d.Motivo.Should().Be(MotivosEmbeddings.Off);
    }

    [Fact]
    public void Sombra_DerivaAlGptYConservaLaPrediccion()
    {
        var d = DecisorHibrido.Decidir(Dist(), Manifiesto(), Params("sombra"), Catalogo);

        d.Decision.Should().Be(DecisionesEmbeddings.DerivarGpt);
        d.Motivo.Should().Be(MotivosEmbeddings.Sombra);
        d.Tdn1.Should().Be("AAAA");
        d.Tdn2.Should().Be("aaaa-01", "la grafia es la del catalogo");
        d.Tipologia.Should().Be("a.01");
        d.Confianza.Should().BeApproximately(0.8, 1e-12);
        d.Restringido.Should().BeNull();
    }

    [Fact]
    public void Hibrido_ConfianzaSobreElUmbral_Contesta()
    {
        var d = DecisorHibrido.Decidir(Dist(pA: 0.8), Manifiesto(), Params("hibrido", umbral: 0.6), Catalogo);

        d.Decision.Should().Be(DecisionesEmbeddings.Contesta);
        d.Motivo.Should().Be(MotivosEmbeddings.Umbral);
        d.Tipologia.Should().Be("a.01");
    }

    [Fact]
    public void Hibrido_ConfianzaBajoElUmbral_Deriva()
    {
        var d = DecisorHibrido.Decidir(Dist(pA: 0.5, pB: 0.45), Manifiesto(), Params("hibrido", umbral: 0.6), Catalogo);

        d.Decision.Should().Be(DecisionesEmbeddings.DerivarGpt);
        d.Motivo.Should().Be(MotivosEmbeddings.ConfianzaBaja);
        d.Tipologia.Should().Be("a.01", "la prediccion se persiste aunque derive");
    }

    [Fact]
    public void Hibrido_ConExpectedType_DerivaPorqueElCallerManda()
    {
        var d = DecisorHibrido.Decidir(Dist(pA: 0.99), Manifiesto(), Params("hibrido", expectedType: true), Catalogo);

        d.Decision.Should().Be(DecisionesEmbeddings.DerivarGpt);
        d.Motivo.Should().Be(MotivosEmbeddings.ExpectedType);
    }

    [Fact]
    public void Hibrido_ParSinTipologiaActiva_Deriva()
    {
        var sinC = Catalogo.Where(kv => kv.Value.Codigo != "c.01").ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
        var d = DecisorHibrido.Decidir(Dist(pA: 0.05, pB: 0.05, pC: 0.9), Manifiesto(), Params("hibrido"), sinC);

        d.Decision.Should().Be(DecisionesEmbeddings.DerivarGpt);
        d.Motivo.Should().Be(MotivosEmbeddings.SinTipologia);
        d.Tdn1.Should().Be("CCCC");
        d.Tdn2.Should().Be("CCCC-01", "sin catalogo se persiste la grafia del modelo");
        d.Tipologia.Should().BeNull();
    }

    [Fact]
    public void RestringidoOff_Omitido()
    {
        var d = DecisorHibrido.Decidir(Dist(), Manifiesto(), Params("hibrido", modoRestringido: "off", restriccion: new[] { "a.01" }), Catalogo);
        d.Decision.Should().Be(DecisionesEmbeddings.Omitido);
    }

    [Fact]
    public void RestringidoSombra_CalculaMasaYCondicionadaYDeriva()
    {
        var d = DecisorHibrido.Decidir(Dist(pA: 0.8, pB: 0.15, pC: 0.05), Manifiesto(),
            Params("hibrido", modoRestringido: "sombra", restriccion: new[] { "a.01", "b.01" }), Catalogo);

        d.Decision.Should().Be(DecisionesEmbeddings.DerivarGpt);
        d.Motivo.Should().Be(MotivosEmbeddings.Sombra);
        d.Restringido.Should().NotBeNull();
        // masa = p(AAAA)*p(AAAA-01) + p(BBBB)*p(BBBB-01) = 0.8*0.7 + 0.15*1.0 = 0.71
        d.Restringido!.Masa.Should().BeApproximately(0.71, 1e-9);
        d.Restringido.ConfianzaCondicionada.Should().BeApproximately(0.56 / 0.71, 1e-9);
        d.Restringido.PrediccionSinRestringir.Should().Be("a.01");
        d.Restringido.Puerta.Should().BeNull();
    }

    [Fact]
    public void RestringidoHibrido_SinCalibrar_SeComportaComoSombra()
    {
        var d = DecisorHibrido.Decidir(Dist(), Manifiesto(calibrado: false),
            Params("hibrido", modoRestringido: "hibrido", restriccion: new[] { "a.01", "b.01" }), Catalogo);

        d.Decision.Should().Be(DecisionesEmbeddings.DerivarGpt);
        d.Motivo.Should().Be(MotivosEmbeddings.SinCalibracion);
        d.Restringido!.Puerta.Should().Be(MotivosEmbeddings.SinCalibracion);
        d.Restringido.Masa.Should().BeGreaterThan(0, "las cifras se persisten igualmente");
    }

    [Fact]
    public void RestringidoHibrido_CodigoFueraDelManifiesto_PuertaCobertura()
    {
        var d = DecisorHibrido.Decidir(Dist(), Manifiesto(calibrado: true),
            Params("hibrido", modoRestringido: "hibrido", restriccion: new[] { "a.01", "zz.99" }), Catalogo);

        d.Decision.Should().Be(DecisionesEmbeddings.DerivarGpt);
        d.Motivo.Should().Be(MotivosEmbeddings.Cobertura);
        d.Restringido!.Puerta.Should().Be(MotivosEmbeddings.Cobertura);
    }

    [Fact]
    public void RestringidoHibrido_DosCodigosConElMismoPar_PuertaParCompartido()
    {
        var d = DecisorHibrido.Decidir(Dist(), Manifiesto(calibrado: true),
            Params("hibrido", modoRestringido: "hibrido", restriccion: new[] { "a.01", "d.01" }), Catalogo);

        d.Decision.Should().Be(DecisionesEmbeddings.DerivarGpt);
        d.Motivo.Should().Be(MotivosEmbeddings.ParCompartido);
    }

    [Fact]
    public void RestringidoHibrido_MasaInsuficiente_ContestaDesconocidoConPropuesta()
    {
        // Permitidas solo c.01: masa = 0.05 < 0.5
        var d = DecisorHibrido.Decidir(Dist(pA: 0.8, pB: 0.15, pC: 0.05), Manifiesto(calibrado: true),
            Params("hibrido", modoRestringido: "hibrido", restriccion: new[] { "c.01" }), Catalogo);

        d.Decision.Should().Be(DecisionesEmbeddings.Contesta);
        d.Motivo.Should().Be(MotivosEmbeddings.MasaInsuficiente);
        d.Tipologia.Should().Be("Desconocido");
        d.Restringido!.PrediccionSinRestringir.Should().Be("a.01");
    }

    [Fact]
    public void RestringidoHibrido_CondicionadaSobreElUmbral_ContestaLaMejorPermitida()
    {
        // Permitidas a.02 y b.01 con B dominante.
        var d = DecisorHibrido.Decidir(Dist(pA: 0.1, pB: 0.85, pC: 0.05), Manifiesto(calibrado: true),
            Params("hibrido", modoRestringido: "hibrido", restriccion: new[] { "a.02", "b.01" }), Catalogo);

        // masa = 0.1*0.3 + 0.85*1.0 = 0.88; condicionada = 0.85/0.88 = 0.966 >= 0.8
        d.Decision.Should().Be(DecisionesEmbeddings.Contesta);
        d.Motivo.Should().Be(MotivosEmbeddings.Umbral);
        d.Tipologia.Should().Be("b.01");
        d.Tdn2.Should().Be("bbbb-01");
        d.Confianza.Should().BeApproximately(0.85 / 0.88, 1e-9);
    }

    [Fact]
    public void RestringidoHibrido_CondicionadaBajoElUmbral_Deriva()
    {
        // Permitidas a.01 y a.02: masa = 0.8; condicionada = 0.56/0.8 = 0.7 < 0.8
        var d = DecisorHibrido.Decidir(Dist(pA: 0.8, pB: 0.15, pC: 0.05), Manifiesto(calibrado: true),
            Params("hibrido", modoRestringido: "hibrido", restriccion: new[] { "a.01", "a.02" }), Catalogo);

        d.Decision.Should().Be(DecisionesEmbeddings.DerivarGpt);
        d.Motivo.Should().Be(MotivosEmbeddings.ConfianzaCondicionadaBaja);
    }

    [Fact]
    public void RestringidoHibrido_ConExpectedType_Deriva()
    {
        var d = DecisorHibrido.Decidir(Dist(), Manifiesto(calibrado: true),
            Params("hibrido", modoRestringido: "hibrido", restriccion: new[] { "a.01" }, expectedType: true), Catalogo);

        d.Decision.Should().Be(DecisionesEmbeddings.DerivarGpt);
        d.Motivo.Should().Be(MotivosEmbeddings.ExpectedType);
    }

    [Fact]
    public void RestringidoHibrido_FamiliaSinModeloTdn2_DerivaPorCobertura()
    {
        // Inferir devuelve Tdn2 = "" y ProbTdn2[familia] = {"": 1.0} cuando la familia no tiene modelo TDN2.
        var p1 = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["ZZZZ"] = 0.9, ["AAAA"] = 0.1 };
        var p2 = new Dictionary<string, IReadOnlyDictionary<string, double>>(StringComparer.OrdinalIgnoreCase)
        {
            ["ZZZZ"] = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { [string.Empty] = 1.0 },
            ["AAAA"] = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["AAAA-01"] = 1.0 },
        };
        var dist = new DistribucionEmbeddings { ProbTdn1 = p1, ProbTdn2 = p2, Tdn1 = "ZZZZ", ConfianzaTdn1 = 0.9, Tdn2 = string.Empty };

        var d = DecisorHibrido.Decidir(dist, Manifiesto(calibrado: true),
            Params("hibrido", modoRestringido: "hibrido", restriccion: new[] { "a.01", "zz.99" }), Catalogo);

        d.Decision.Should().Be(DecisionesEmbeddings.DerivarGpt);
        d.Motivo.Should().Be(MotivosEmbeddings.Cobertura);
        d.Tipologia.Should().BeNull();

        var sinRestriccion = DecisorHibrido.Decidir(dist, Manifiesto(calibrado: true), Params("hibrido"), Catalogo);
        sinRestriccion.Decision.Should().Be(DecisionesEmbeddings.DerivarGpt);
        sinRestriccion.Motivo.Should().Be(MotivosEmbeddings.SinTipologia);
    }
}
