using DocumentIA.Core.Services.Classification;
using FluentAssertions;

namespace DocumentIA.Tests.Unit.Services.Classification;

public class ModeloEmbeddingsTests
{
    public static string RutaFixture(string nombre) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "clasificador-embeddings", nombre);

    [Fact]
    public void Parse_FixtureSintetico_CargaManifiestoYFamilias()
    {
        var modelo = ModeloEmbeddings.Parse(File.ReadAllText(RutaFixture("clasificador-embeddings-fixture.json")));

        modelo.Manifiesto.Version.Should().Be("fixture");
        modelo.Manifiesto.Dimensiones.Should().Be(8);
        modelo.Manifiesto.Calibrado.Should().BeFalse();
        modelo.Tdn1.Clases.Should().Contain(new[] { "AAAA", "BBBB", "CCCC", "DDDD" });
        modelo.Tdn2["cccc"].Constante.Should().Be("CCCC-01", "el diccionario de familias no distingue mayusculas");
        modelo.Tdn2["BBBB"].Coef.Should().HaveCount(1, "sklearn guarda una sola fila en el caso binario");
        modelo.Tdn2["AAAA"].Coef.Should().HaveCount(3);
    }

    [Fact]
    public void Parse_JsonInvalido_LanzaInvalidData()
    {
        var act = () => ModeloEmbeddings.Parse("{ esto no es json");
        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void Parse_CoefConDimensionesDistintas_LanzaInvalidData()
    {
        const string json = """
        {"manifiesto":{"version":"x","dimensiones":3},
         "tdn1":{"clases":["A","B","C"],"coef":[[1,2],[1,2],[1,2]],"intercept":[0,0,0]},
         "tdn2":{}}
        """;
        var act = () => ModeloEmbeddings.Parse(json);
        act.Should().Throw<InvalidDataException>().WithMessage("*dimensiones*");
    }

    [Fact]
    public void Parse_FamiliaSinConstanteNiCoef_LanzaInvalidData()
    {
        const string json = """
        {"manifiesto":{"version":"x","dimensiones":2},
         "tdn1":{"clases":["A","B"],"coef":[[1,2]],"intercept":[0]},
         "tdn2":{"A":{"clases":["A-01","A-02"]}}}
        """;
        var act = () => ModeloEmbeddings.Parse(json);
        act.Should().Throw<InvalidDataException>().WithMessage("*familia 'A'*");
    }
}
