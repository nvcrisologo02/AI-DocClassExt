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
