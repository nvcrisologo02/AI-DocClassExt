using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using DocumentIA.Core.Services;
using FluentAssertions;

namespace DocumentIA.Tests.Unit.Services;

public class MarkdownCompressionTests
{
    [Fact]
    public void CompressThenDecompress_Roundtrip_ReturnsOriginalValue()
    {
        const string original = "# Titulo\n\nContenido de prueba con acentos: ñ, á, é, í, ó, ú.";

        var compressed = MarkdownCompression.CompressToBase64(original);
        compressed.Should().NotBeNullOrWhiteSpace();

        var decompressed = MarkdownCompression.DecompressFromBase64(compressed);

        decompressed.Should().Be(original);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CompressToBase64_NullOrWhitespace_ReturnsNull(string? value)
    {
        MarkdownCompression.CompressToBase64(value).Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void DecompressFromBase64_NullOrWhitespace_ReturnsNull(string? value)
    {
        MarkdownCompression.DecompressFromBase64(value).Should().BeNull();
    }

    [Fact]
    public void DecompressFromBase64_InvalidBase64_ReturnsNullWithoutThrowing()
    {
        var action = () => MarkdownCompression.DecompressFromBase64("esto-no-es-base64-valido-@@@");

        action.Should().NotThrow();
        MarkdownCompression.DecompressFromBase64("esto-no-es-base64-valido-@@@").Should().BeNull();
    }

    [Fact]
    public void DecompressFromBase64_ValidBase64ButNotGzip_ReturnsNullWithoutThrowing()
    {
        var notGzipBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes("texto plano, no comprimido"));

        var action = () => MarkdownCompression.DecompressFromBase64(notGzipBase64);

        action.Should().NotThrow();
        MarkdownCompression.DecompressFromBase64(notGzipBase64).Should().BeNull();
    }

    [Fact]
    public void DecompressFromBase64_ReadsFormatEquivalentToPersistirActivity()
    {
        // Reproduce exactamente el algoritmo historico de PersistirActivity.CompressToBase64
        // (UTF8 -> GZipStream Optimal -> Base64) para garantizar compatibilidad con datos ya persistidos.
        const string original = "# Markdown historico persistido antes del refactor";
        var rawBytes = Encoding.UTF8.GetBytes(original);
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            gzip.Write(rawBytes, 0, rawBytes.Length);
        }
        output.Position = 0;
        var legacyCompressed = Convert.ToBase64String(output.ToArray());

        var decompressed = MarkdownCompression.DecompressFromBase64(legacyCompressed);

        decompressed.Should().Be(original);
    }
}

public class MarkdownCompressionBinarioTests
{
    private const string Markdown = "# Nota simple\n\nTitular: Prueba\n\n| campo | valor |\n|---|---|\n| a | b |\n";

    [Fact]
    public void CompressDecompress_IdaYVuelta_DevuelveElOriginal()
    {
        var comprimido = MarkdownCompression.Compress(Markdown);

        comprimido.Should().NotBeNull();
        MarkdownCompression.Decompress(comprimido).Should().Be(Markdown);
    }

    [Fact]
    public void Compress_OcupaMenosQueLaVariantePreviaEnBaseDatos()
    {
        // La forma anterior era Base64 en nvarchar(max): +33% por Base64 y x2 por UTF-16.
        var binario = MarkdownCompression.Compress(Markdown)!;
        var base64 = MarkdownCompression.CompressToBase64(Markdown)!;
        var bytesEnBd = base64.Length * 2; // nvarchar almacena 2 bytes por caracter

        binario.Length.Should().BeLessThan(bytesEnBd);
    }

    [Fact]
    public void Compress_ProduceLosMismosBytesQueDecodificarLaVarianteBase64()
    {
        // La migracion del historico convierte Base64 -> bytes sin descomprimir. Este test
        // fija esa equivalencia: si dejara de cumplirse, la migracion corromperia datos.
        var binario = MarkdownCompression.Compress(Markdown)!;
        var desdeBase64 = Convert.FromBase64String(MarkdownCompression.CompressToBase64(Markdown)!);

        binario.Should().BeEquivalentTo(desdeBase64);
    }

    [Fact]
    public void Compress_NuloOVacio_DevuelveNull()
    {
        MarkdownCompression.Compress(null).Should().BeNull();
        MarkdownCompression.Compress("   ").Should().BeNull();
    }

    [Fact]
    public void Decompress_NuloVacioOCorrupto_DevuelveNullSinLanzar()
    {
        MarkdownCompression.Decompress(null).Should().BeNull();
        MarkdownCompression.Decompress(Array.Empty<byte>()).Should().BeNull();
        MarkdownCompression.Decompress(Encoding.UTF8.GetBytes("esto no es gzip")).Should().BeNull();
    }
}
