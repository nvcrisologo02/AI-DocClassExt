#nullable enable
using DocumentIA.Functions.Services;
using FluentAssertions;

namespace DocumentIA.Tests.Unit.Services;

/// <summary>
/// AB#101428: corte de la plantilla de usuario antes de <c>{DOCUMENT_TEXT}</c> para poner el breakpoint de
/// caché de prompt tras el tramo estable (instrucciones y catálogo).
/// </summary>
public class UserPromptSplitterTests
{
    [Fact]
    public void Partir_CortaAntesDelDocumento()
    {
        var (estable, resto) = UserPromptSplitter.Partir(
            "A {CONTEXT_PROMPT} B {TDN1_CATALOG}\n\nDOC:\n{DOCUMENT_TEXT}",
            [new("{CONTEXT_PROMPT}", "ctx"), new("{TDN1_CATALOG}", "cat"), new("{DOCUMENT_TEXT}", "texto")]);

        estable.Should().Be("A ctx B cat\n\nDOC:\n");
        resto.Should().Be("texto");
    }

    [Fact]
    public void Partir_ConcatenadoIgualAlReplaceEncadenado()
    {
        const string plantilla =
            "Prompt adicional de instrucciones (si aplica):\n{CONTEXT_PROMPT}\n\nFamilias TDN1 disponibles:\n{TDN1_CATALOG}\n\n" +
            "Si no puedes resolver una familia, devuelve tdn1=null.\n\nCONTENIDO DEL DOCUMENTO (texto/markdown):\n{DOCUMENT_TEXT}";
        const string contexto = "(sin prompt adicional)";
        const string catalogo = "- ESCR: Escrituras\n- NOTA: Notas simples";
        const string documento = "# Escritura de compraventa\n\nEn Madrid, a 3 de mayo...";

        var (estable, resto) = UserPromptSplitter.Partir(
            plantilla,
            [new("{CONTEXT_PROMPT}", contexto), new("{TDN1_CATALOG}", catalogo), new("{DOCUMENT_TEXT}", documento)]);

        var esperado = plantilla
            .Replace("{CONTEXT_PROMPT}", contexto)
            .Replace("{TDN1_CATALOG}", catalogo)
            .Replace("{DOCUMENT_TEXT}", documento);
        (estable + resto).Should().Be(esperado);
        resto.Should().Be(documento);
    }

    [Fact]
    public void Partir_SinMarcador_TodoEstableYRestoVacio()
    {
        var (estable, resto) = UserPromptSplitter.Partir(
            "X {CONTEXT_PROMPT}",
            [new("{CONTEXT_PROMPT}", "ctx"), new("{DOCUMENT_TEXT}", "texto")]);

        estable.Should().Be("X ctx");
        resto.Should().BeEmpty();
    }

    [Fact]
    public void Partir_MarcadorRepetido_CortaEnElPrimero()
    {
        var (estable, resto) = UserPromptSplitter.Partir(
            "A {DOCUMENT_TEXT} B {DOCUMENT_TEXT}",
            [new("{DOCUMENT_TEXT}", "texto")]);

        estable.Should().Be("A ");
        resto.Should().Be("texto B texto");
    }
}
