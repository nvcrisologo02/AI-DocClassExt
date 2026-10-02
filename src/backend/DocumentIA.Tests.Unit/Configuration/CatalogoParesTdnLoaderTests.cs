#nullable enable
using DocumentIA.Core.Configuration;
using DocumentIA.Core.Services.Classification;
using DocumentIA.Data.Entities;
using FluentAssertions;

namespace DocumentIA.Tests.Unit.Configuration;

public class CatalogoParesTdnLoaderTests
{
    private static TipologiaEntity Tipologia(string codigo, string json, bool activa = true, EstadoTipologia estado = EstadoTipologia.Published) => new()
    {
        Codigo = codigo,
        Nombre = codigo,
        Activa = activa,
        Estado = estado,
        ConfiguracionJson = json
    };

    [Fact]
    public void Construir_IndexaPorParEnMayusculasYConservaLaGrafiaDelCatalogo()
    {
        var catalogo = CatalogoParesTdnLoader.Construir(new[]
        {
            Tipologia("nota.simple", """{"classification":{"tdn1":"NOTS","tdn2":"nots-01"}}"""),
            Tipologia("tasa.09", """{"Classification":{"Tdn1":"TASA","Tdn2":"TASA-09"}}"""),
        });

        catalogo.Should().HaveCount(2);
        catalogo["NOTS|NOTS-01"].Should().Be(new TipologiaPar("nota.simple", "NOTS", "nots-01"));
        catalogo.ContainsKey("nots|nots-01").Should().BeTrue("la busqueda no distingue mayusculas");
        catalogo["TASA|TASA-09"].Codigo.Should().Be("tasa.09");
    }

    [Fact]
    public void Construir_IgnoraSinParInactivasNoPublicadasYJsonInvalido()
    {
        var catalogo = CatalogoParesTdnLoader.Construir(new[]
        {
            Tipologia("sin.par", """{"gptDescripcion":"x"}"""),
            Tipologia("inactiva", """{"classification":{"tdn1":"CERA","tdn2":"CERA-16"}}""", activa: false),
            Tipologia("borrador", """{"classification":{"tdn1":"CERA","tdn2":"CERA-17"}}""", estado: EstadoTipologia.Draft),
            Tipologia("rota", "{ no json"),
            Tipologia("buena", """{"classification":{"tdn1":"CERA","tdn2":"CERA-18"}}"""),
        });

        catalogo.Should().ContainSingle().Which.Value.Codigo.Should().Be("buena");
    }

    [Fact]
    public void Construir_ParDuplicado_ConservaLaPrimera()
    {
        var catalogo = CatalogoParesTdnLoader.Construir(new[]
        {
            Tipologia("a.01", """{"classification":{"tdn1":"AAAA","tdn2":"AAAA-01"}}"""),
            Tipologia("a.01.bis", """{"classification":{"tdn1":"aaaa","tdn2":"aaaa-01"}}"""),
        });

        catalogo.Should().ContainSingle().Which.Value.Codigo.Should().Be("a.01");
    }
}
