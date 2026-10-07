using System.Text;
using System.Text.Json;
using DocumentIA.Core.Models;
using DocumentIA.Functions.Serialization;
using FluentAssertions;

namespace DocumentIA.Tests.Unit.Serialization;

public class ContenidoDocumentoIngestaConverterTests
{
    private static readonly JsonSerializerOptions Opciones = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new ContenidoDocumentoIngestaConverter() }
    };

    [Fact]
    public void Read_Base64Valido_DecodificaABytesYNoGuardaElString()
    {
        var json = """{ "base64": "aG9sYQ==" }""";

        var contenido = JsonSerializer.Deserialize<ContenidoDocumento>(json, Opciones);

        contenido.Should().NotBeNull();
        contenido!.Bytes.Should().Equal(Encoding.UTF8.GetBytes("hola"));
        contenido.Base64.Should().BeNull();
    }

    [Fact]
    public void Read_Base64ConSaltosDeLinea_DecodificaPorElFallback()
    {
        // Convert.ToBase64String con InsertLineBreaks produce "\r\n" cada 76 caracteres.
        var largo = Convert.ToBase64String(new byte[100], Base64FormattingOptions.InsertLineBreaks);
        var json = JsonSerializer.Serialize(new { base64 = largo });

        var contenido = JsonSerializer.Deserialize<ContenidoDocumento>(json, Opciones);

        contenido!.Bytes.Should().Equal(new byte[100]);
        contenido.Base64.Should().BeNull();
    }

    [Fact]
    public void Read_Base64Invalido_DejaElStringParaQueElTriggerResponda400()
    {
        var json = """{ "base64": "@@no-base64@@" }""";

        var contenido = JsonSerializer.Deserialize<ContenidoDocumento>(json, Opciones);

        contenido!.Bytes.Should().BeNull();
        contenido.Base64.Should().Be("@@no-base64@@");
    }

    [Fact]
    public void Read_Base64NuloOAusente_DejaTodoANull()
    {
        JsonSerializer.Deserialize<ContenidoDocumento>("""{ "base64": null }""", Opciones)!.Bytes.Should().BeNull();
        JsonSerializer.Deserialize<ContenidoDocumento>("""{ }""", Opciones)!.Base64.Should().BeNull();
    }

    [Fact]
    public void Read_PropiedadDesconocida_SeIgnora()
    {
        var json = """{ "otra": { "x": 1 }, "base64": "aG9sYQ==" }""";

        var contenido = JsonSerializer.Deserialize<ContenidoDocumento>(json, Opciones);

        contenido!.Bytes.Should().Equal(Encoding.UTF8.GetBytes("hola"));
    }

    [Fact]
    public void Write_SerializaBase64ComoStringYNuncaBytes()
    {
        var contenido = new ContenidoDocumento { Base64 = "aG9sYQ==", Bytes = new byte[] { 1, 2, 3 } };

        var json = JsonSerializer.Serialize(contenido, Opciones);

        json.Should().Be("""{"base64":"aG9sYQ=="}""");
    }

    [Fact]
    public void Serializacion_PorDefecto_IgnoraBytes()
    {
        // La orquestacion serializa ContratoEntrada con las opciones por defecto: Bytes no viaja.
        var contenido = new ContenidoDocumento { Bytes = new byte[] { 1, 2, 3 } };

        var json = JsonSerializer.Serialize(contenido);

        json.Should().NotContain("Bytes").And.NotContain("bytes");
    }
}
