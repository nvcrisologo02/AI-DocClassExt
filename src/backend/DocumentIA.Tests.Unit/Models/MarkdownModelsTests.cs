using DocumentIA.Core.Models;
using FluentAssertions;

namespace DocumentIA.Tests.Unit.Models;

public class MarkdownModelsTests
{
    private static ResultadoMarkdown Con(string? md, int paginas, bool completo) => new()
    {
        Markdown = md, Paginas = paginas, Completo = completo, Fuente = FuenteMarkdown.Layout
    };

    [Fact]
    public void Cubre_CompletoSirveParaCualquierNecesidad()
    {
        Con("# x", 14, completo: true).Cubre(NecesidadMarkdown.Completo()).Should().BeTrue();
        Con("# x", 14, completo: true).Cubre(NecesidadMarkdown.Paginas(3)).Should().BeTrue();
    }

    [Fact]
    public void Cubre_ParcialSirveSoloSiLlegaALasPaginasPedidas()
    {
        Con("# x", 5, completo: false).Cubre(NecesidadMarkdown.Paginas(3)).Should().BeTrue();
        Con("# x", 5, completo: false).Cubre(NecesidadMarkdown.Paginas(5)).Should().BeTrue();
        Con("# x", 2, completo: false).Cubre(NecesidadMarkdown.Paginas(3)).Should().BeFalse();
        Con("# x", 5, completo: false).Cubre(NecesidadMarkdown.Completo()).Should().BeFalse();
    }

    [Fact]
    public void Cubre_SinContenidoNuncaCubre()
    {
        Con(null, 99, completo: true).Cubre(NecesidadMarkdown.Paginas(1)).Should().BeFalse();
        Con("   ", 99, completo: true).Cubre(NecesidadMarkdown.Completo()).Should().BeFalse();
    }

    [Fact]
    public void Paginas_NuncaBajaDeUna()
    {
        NecesidadMarkdown.Paginas(0).PaginasMinimas.Should().Be(1);
        NecesidadMarkdown.Paginas(-4).PaginasMinimas.Should().Be(1);
        NecesidadMarkdown.Completo().DocumentoCompleto.Should().BeTrue();
    }
}
