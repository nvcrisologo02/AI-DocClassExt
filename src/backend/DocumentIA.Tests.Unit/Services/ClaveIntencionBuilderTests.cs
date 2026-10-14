#nullable enable
using System.Text.Json;
using DocumentIA.Core.Models;
using DocumentIA.Core.Services;
using FluentAssertions;

namespace DocumentIA.Tests.Unit.Services;

/// <summary>
/// La clave de intencion identifica "la misma peticion" (AB#100930). Dos peticiones con el
/// mismo objetivo tienen que dar la misma clave aunque difieran en orden, mayusculas o
/// espacios, y cualquier diferencia de objetivo tiene que dar claves distintas.
/// </summary>
public class ClaveIntencionBuilderTests
{
    private const string NivelDefault = "TDN1_TDN2";

    [Fact]
    public void Normalizar_OrdenaYPoneEnMayusculasLosCodigosDelUniverso()
    {
        var a = ClaveIntencionBuilder.Normalizar(false, "TDN1_TDN2", NivelDefault, new[] { " tasacion", "Nota.Simple", "tasacion" }, null, "gpt-4o-mini");
        var b = ClaveIntencionBuilder.Normalizar(false, "tdn1_tdn2", NivelDefault, new[] { "NOTA.SIMPLE", "TASACION" }, "", "GPT-4O-MINI");

        a.Universo.Should().Be("NOTA.SIMPLE,TASACION");
        a.Should().BeEquivalentTo(b);
        ClaveIntencionBuilder.Calcular(a).Should().Be(ClaveIntencionBuilder.Calcular(b));
    }

    [Fact]
    public void Normalizar_SinRestriccionDaUniversoCompleto()
    {
        var sinLista = ClaveIntencionBuilder.Normalizar(false, null, NivelDefault, null, null, "gpt-4o-mini");
        var listaVacia = ClaveIntencionBuilder.Normalizar(false, null, NivelDefault, new List<string>(), null, "gpt-4o-mini");

        sinLista.Universo.Should().Be(ClaveIntencionBuilder.UniversoCompleto);
        listaVacia.Universo.Should().Be(ClaveIntencionBuilder.UniversoCompleto);
    }

    [Fact]
    public void Normalizar_NivelVacioEquivaleAlDefault()
    {
        var vacio = ClaveIntencionBuilder.Normalizar(true, null, NivelDefault, null, null, "gpt-4o-mini");
        var explicito = ClaveIntencionBuilder.Normalizar(true, "TDN1_TDN2", NivelDefault, null, null, "gpt-4o-mini");

        vacio.Nivel.Should().Be("TDN1_TDN2");
        ClaveIntencionBuilder.Calcular(vacio).Should().Be(ClaveIntencionBuilder.Calcular(explicito));
    }

    [Fact]
    public void Normalizar_ModoDistintoDaClaveDistinta()
    {
        var solo = ClaveIntencionBuilder.Normalizar(true, "TDN1_TDN2", NivelDefault, null, null, "gpt-4o-mini");
        var completo = ClaveIntencionBuilder.Normalizar(false, "TDN1_TDN2", NivelDefault, null, null, "gpt-4o-mini");

        solo.Modo.Should().Be(ModosIntencion.SoloClasificacion);
        completo.Modo.Should().Be(ModosIntencion.Completo);
        ClaveIntencionBuilder.Calcular(solo).Should().NotBe(ClaveIntencionBuilder.Calcular(completo));
    }

    [Fact]
    public void Normalizar_ConExpectedTypeFijaElModeloAExpectedTypeInput()
    {
        var c = ClaveIntencionBuilder.Normalizar(false, "TDN1_TDN2", NivelDefault, null, " nota.simple ", "gpt-4o-mini");

        c.ExpectedType.Should().Be("NOTA.SIMPLE");
        c.ModeloClasificacion.Should().Be(ClaveIntencionBuilder.ModeloExpectedType);
    }

    [Fact]
    public void Normalizar_ExpectedTypeDesconocidoEquivaleAVacio()
    {
        var c = ClaveIntencionBuilder.Normalizar(false, "TDN1_TDN2", NivelDefault, null, "Desconocido", "gpt-4o-mini");

        c.ExpectedType.Should().BeEmpty();
        c.ModeloClasificacion.Should().Be("gpt-4o-mini");
    }

    [Fact]
    public void Calcular_Devuelve32Hexadecimales()
    {
        var clave = ClaveIntencionBuilder.Calcular(ClaveIntencionBuilder.Normalizar(false, null, NivelDefault, null, null, "gpt-4o-mini"));

        clave.Should().HaveLength(32).And.MatchRegex("^[0-9a-f]{32}$");
    }

    [Fact]
    public void Serializar_UsaCamelCaseYConservaLosCincoComponentes()
    {
        var c = ClaveIntencionBuilder.Normalizar(true, "TDN1", NivelDefault, new[] { "tasacion" }, null, "gpt-4o-mini");

        using var doc = JsonDocument.Parse(ClaveIntencionBuilder.Serializar(c));
        doc.RootElement.GetProperty("modo").GetString().Should().Be("solo-clasificacion");
        doc.RootElement.GetProperty("nivel").GetString().Should().Be("TDN1");
        doc.RootElement.GetProperty("universo").GetString().Should().Be("TASACION");
        doc.RootElement.GetProperty("expectedType").GetString().Should().BeEmpty();
        doc.RootElement.GetProperty("modeloClasificacion").GetString().Should().Be("gpt-4o-mini");
    }

    // Vectores compartidos con scripts/database/backfill-clave-intencion.ps1 (-SelfTest). Si
    // cambia la forma canonica hay que cambiar los dos sitios a la vez: un backfill con otra
    // canonica escribiria claves que nunca coinciden con las vivas. Los hashes son literales
    // (sha256 de la forma canonica, 32 primeros hex) y replican el -SelfTest del backfill.
    [Theory]
    [InlineData(false, "TDN1_TDN2", "*", "", "gpt-4o-mini", "v1|completo|TDN1_TDN2|*||gpt-4o-mini", "ae16c9e79cf0dac934cb460c80397c65")]
    [InlineData(true, "TDN1", "NOTA.SIMPLE,TASACION", "", "gpt-4o-mini", "v1|solo-clasificacion|TDN1|NOTA.SIMPLE,TASACION||gpt-4o-mini", "4fc9510621a5ba0162368b8f72f869cc")]
    [InlineData(false, "TDN1_TDN2", "*", "NOTA.SIMPLE", "expectedtype-input", "v1|completo|TDN1_TDN2|*|NOTA.SIMPLE|expectedtype-input", "ee58c9392080836d32c11865bd8b8f53")]
    public void FormaCanonica_EsLaQueReplicaElBackfill(bool solo, string nivel, string universo, string expected, string modelo, string canonicaEsperada, string claveEsperada)
    {
        var c = new ClaveIntencionComponentes
        {
            Modo = solo ? ModosIntencion.SoloClasificacion : ModosIntencion.Completo,
            Nivel = nivel,
            Universo = universo,
            ExpectedType = expected,
            ModeloClasificacion = modelo
        };

        ClaveIntencionBuilder.FormaCanonica(c).Should().Be(canonicaEsperada);
        ClaveIntencionBuilder.Calcular(c).Should().Be(claveEsperada);
    }

    [Fact]
    public void VectorDeReferencia_FijaElHashParaElBackfill()
    {
        // Hash de "v1|completo|TDN1_TDN2|*||gpt-4o-mini", copiado tambien en el -SelfTest del
        // backfill. Si cambia hay que cambiar los dos sitios a la vez.
        var c = ClaveIntencionBuilder.Normalizar(false, null, NivelDefault, null, null, "gpt-4o-mini");

        ClaveIntencionBuilder.Calcular(c).Should().Be(VectorReferencia);
    }

    internal const string VectorReferencia = "ae16c9e79cf0dac934cb460c80397c65";
}
