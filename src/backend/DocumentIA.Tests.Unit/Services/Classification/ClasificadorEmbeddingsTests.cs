using System.Text.Json;
using DocumentIA.Core.Services.Classification;
using FluentAssertions;

namespace DocumentIA.Tests.Unit.Services.Classification;

public class ClasificadorEmbeddingsTests
{
    private sealed class CasoParidad
    {
        public string Sha256 { get; set; } = string.Empty;
        public float[] Vector { get; set; } = Array.Empty<float>();
        public Dictionary<string, double> ProbTdn1 { get; set; } = new();
        public string Tdn1 { get; set; } = string.Empty;
        public string Tdn2 { get; set; } = string.Empty;
        public Dictionary<string, double> ProbTdn2 { get; set; } = new();
    }

    private sealed class Paridad
    {
        public List<CasoParidad> Casos { get; set; } = new();
    }

    private static readonly JsonSerializerOptions Opciones = new() { PropertyNameCaseInsensitive = true };

    private static (ModeloEmbeddings Modelo, Paridad Paridad) Cargar()
    {
        var modelo = ModeloEmbeddings.Parse(File.ReadAllText(ModeloEmbeddingsTests.RutaFixture("clasificador-embeddings-fixture.json")));
        var paridad = JsonSerializer.Deserialize<Paridad>(File.ReadAllText(ModeloEmbeddingsTests.RutaFixture("paridad-fixture.json")), Opciones)!;
        return (modelo, paridad);
    }

    [Fact]
    public void Inferir_ReproduceSklearnEnTdn1YTdn2()
    {
        var (modelo, paridad) = Cargar();
        paridad.Casos.Should().HaveCount(7);

        foreach (var caso in paridad.Casos)
        {
            var d = ClasificadorEmbeddings.Inferir(modelo, caso.Vector);

            d.Tdn1.Should().Be(caso.Tdn1, caso.Sha256);
            d.Tdn2.Should().Be(caso.Tdn2, caso.Sha256);
            foreach (var (clase, p) in caso.ProbTdn1)
            {
                d.ProbTdn1[clase].Should().BeApproximately(p, 1e-6, $"{caso.Sha256} TDN1 {clase}");
            }
            foreach (var (sub, p) in caso.ProbTdn2)
            {
                d.ProbTdn2[caso.Tdn1][sub].Should().BeApproximately(p, 1e-6, $"{caso.Sha256} TDN2 {sub}");
            }
            d.ConfianzaTdn1.Should().BeApproximately(caso.ProbTdn1.Values.Max(), 1e-6);
        }
    }

    [Fact]
    public void Inferir_CubreCasosMulticlaseBinarioYConstante()
    {
        var (modelo, paridad) = Cargar();
        var familias = paridad.Casos.Select(c => c.Tdn1).Distinct().ToList();

        familias.Should().Contain("AAAA").And.Contain("BBBB").And.Contain("CCCC");
        var constante = ClasificadorEmbeddings.Inferir(modelo, paridad.Casos.First(c => c.Tdn1 == "CCCC").Vector);
        constante.ProbTdn2["CCCC"].Should().ContainSingle().Which.Key.Should().Be("CCCC-01");
    }

    [Fact]
    public void Inferir_DimensionesDistintas_Lanza()
    {
        var (modelo, _) = Cargar();
        var act = () => ClasificadorEmbeddings.Inferir(modelo, new float[3]);
        act.Should().Throw<ArgumentException>().WithMessage("*3*8*");
    }

    [Fact]
    public void ProbabilidadPar_YTop_SonCoherentes()
    {
        var (modelo, paridad) = Cargar();
        var caso = paridad.Casos[0];
        var d = ClasificadorEmbeddings.Inferir(modelo, caso.Vector);

        d.ProbabilidadPar(d.Tdn1, d.Tdn2).Should().BeApproximately(d.ProbTdn1[d.Tdn1] * d.ProbTdn2[d.Tdn1][d.Tdn2], 1e-12);
        d.ProbabilidadPar("ZZZZ", "ZZZZ-01").Should().Be(0);
        d.ProbabilidadPar(d.Tdn1.ToLowerInvariant(), d.Tdn2.ToLowerInvariant()).Should().BeGreaterThan(0, "no distingue mayusculas");
        var top = d.Top(3);
        top.Should().HaveCount(3);
        top[0].Tdn1.Should().Be(d.Tdn1);
        top.Select(t => t.Probabilidad).Should().BeInDescendingOrder();
    }
}
