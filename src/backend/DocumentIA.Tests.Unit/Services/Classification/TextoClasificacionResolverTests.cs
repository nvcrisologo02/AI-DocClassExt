using System.Text.Json;
using DocumentIA.Functions.Services.Classification;
using FluentAssertions;

namespace DocumentIA.Tests.Unit.Services.Classification;

public class TextoClasificacionResolverTests
{
    [Fact]
    public void Obtener_PrefiereMarkdownYAceptaStringOJsonElement()
    {
        TextoClasificacionResolver.Obtener(new Dictionary<string, object> { ["Markdown"] = "# md", ["Texto"] = "txt" }).Should().Be("# md");
        TextoClasificacionResolver.Obtener(new Dictionary<string, object> { ["contentText"] = JsonDocument.Parse("\"desde json\"").RootElement }).Should().Be("desde json");
        TextoClasificacionResolver.Obtener(new Dictionary<string, object> { ["Markdown"] = "   ", ["texto"] = "t" }).Should().Be("t", "un valor en blanco no cuenta");
    }

    [Fact]
    public void Obtener_SinClaves_DevuelveNulo()
    {
        TextoClasificacionResolver.Obtener(new Dictionary<string, object> { ["SHA256"] = "x" }).Should().BeNull();
    }

    [Fact]
    public void Preprocesar_ColapsaEspaciosYRecorta()
    {
        TextoClasificacionResolver.Preprocesar("  a\n\n b\t\tc  ", 100).Should().Be("a b c");
        TextoClasificacionResolver.Preprocesar("abcdefghij", 4).Should().Be("abcd");
        TextoClasificacionResolver.Preprocesar(null, 4).Should().BeEmpty();
    }
}
