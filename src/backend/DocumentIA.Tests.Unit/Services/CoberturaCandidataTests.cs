// src/backend/DocumentIA.Tests.Unit/Services/CoberturaCandidataTests.cs
#nullable enable
using DocumentIA.Core.Models;
using DocumentIA.Core.Services;
using FluentAssertions;

namespace DocumentIA.Tests.Unit.Services;

/// <summary>
/// Una historica sirve si procesó al menos las paginas que pide la peticion (spec, seccion 10).
/// Con cobertura de markdown conocida manda la regla del resolutor; sin ella, el recorte de
/// clasificacion es la unica cobertura que las filas antiguas registran.
/// </summary>
public class CoberturaCandidataTests
{
    [Fact]
    public void Cubre_MarkdownCompleto_SirveParaCualquierNecesidad()
    {
        var det = new DetalleEjecucion { MarkdownCompleto = true, MarkdownPaginas = 12 };

        CoberturaCandidata.Cubre(det, NecesidadMarkdown.Completo()).Should().BeTrue();
        CoberturaCandidata.Cubre(det, NecesidadMarkdown.Paginas(40)).Should().BeTrue();
    }

    [Fact]
    public void Cubre_MarkdownParcial_SirveSoloSiLlegaALasPaginasPedidas()
    {
        var det = new DetalleEjecucion { MarkdownCompleto = false, MarkdownPaginas = 3 };

        CoberturaCandidata.Cubre(det, NecesidadMarkdown.Paginas(3)).Should().BeTrue();
        CoberturaCandidata.Cubre(det, NecesidadMarkdown.Paginas(6)).Should().BeFalse();
        CoberturaCandidata.Cubre(det, NecesidadMarkdown.Completo()).Should().BeFalse();
    }

    [Fact]
    public void Cubre_ParcialConRecorteMenorQueLoPedido_NoSirveAunqueElMarkdownLlegue()
    {
        var det = new DetalleEjecucion { MarkdownCompleto = false, MarkdownPaginas = 6, RecorteAplicado = true, PaginasIncluidas = 3 };

        CoberturaCandidata.Cubre(det, NecesidadMarkdown.Paginas(6)).Should().BeFalse();
    }

    [Fact]
    public void Cubre_CoberturaDesconocidaSinRecorte_Sirve()
    {
        var det = new DetalleEjecucion { MarkdownCompleto = false, MarkdownPaginas = 0, RecorteAplicado = false, PaginasIncluidas = 0 };

        CoberturaCandidata.Cubre(det, NecesidadMarkdown.Completo()).Should().BeTrue();
        CoberturaCandidata.Cubre(det, NecesidadMarkdown.Paginas(3)).Should().BeTrue();
    }

    [Fact]
    public void Cubre_CoberturaDesconocidaConRecorte_SirveSoloHastaLasPaginasDelRecorte()
    {
        var det = new DetalleEjecucion { MarkdownCompleto = false, MarkdownPaginas = 0, RecorteAplicado = true, PaginasIncluidas = 3 };

        CoberturaCandidata.Cubre(det, NecesidadMarkdown.Paginas(3)).Should().BeTrue();
        CoberturaCandidata.Cubre(det, NecesidadMarkdown.Paginas(4)).Should().BeFalse();
        CoberturaCandidata.Cubre(det, NecesidadMarkdown.Completo()).Should().BeFalse();
    }
}
