#nullable enable
using System.Security.Cryptography;
using DocumentIA.Core.Models;
using DocumentIA.Core.Services;
using DocumentIA.Functions.Activities;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DocumentIA.Tests.Unit.Activities;

public class NormalizarActivityTests
{
    private readonly NormalizarActivity _sut;

    public NormalizarActivityTests()
    {
        var mockBlobStorage = new Mock<IBlobStorageService>();
        _sut = new NormalizarActivity(new Mock<ILogger<NormalizarActivity>>().Object, mockBlobStorage.Object);
    }

    private static ContratoEntrada BuildEntrada(byte[] content, string nombre = "Test.PDF")
        => new()
        {
            Documento = new Documento
            {
                Name = nombre,
                Content = new ContenidoDocumento { Base64 = Convert.ToBase64String(content) }
            }
        };

    [Fact]
    public async Task Run_BytesConocidos_SHA256Correcto()
    {
        var bytes = "Hello DocumentIA"u8.ToArray();
        var expected = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        var result = await _sut.Run(BuildEntrada(bytes));

        result["SHA256"].Should().Be(expected);
    }

    [Fact]
    public async Task Run_BytesConocidos_MD5Correcto()
    {
        var bytes = "Hello DocumentIA"u8.ToArray();
        var expected = Convert.ToHexString(MD5.HashData(bytes)).ToLowerInvariant();

        var result = await _sut.Run(BuildEntrada(bytes));

        result["MD5"].Should().Be(expected);
    }

    [Fact]
    public async Task Run_ArrayVacio_CRC32EsCero()
    {
        // CRC32 de array vacío: ~0xFFFFFFFF = 0x00000000
        var bytes = Array.Empty<byte>();

        var result = await _sut.Run(BuildEntrada(bytes));

        result["CRC32"].Should().Be("00000000");
    }

    [Fact]
    public async Task Run_BytesConocidos_TamañoBytesEsCorrecto()
    {
        var bytes = new byte[] { 1, 2, 3, 4, 5 };

        var result = await _sut.Run(BuildEntrada(bytes));

        result["TamañoBytes"].Should().Be(5);
    }

    [Fact]
    public async Task Run_NombreConEspaciosYMayusculas_NombreNormalizadoEnMinusculasSinEspacios()
    {
        var bytes = new byte[] { 1 };

        var result = await _sut.Run(BuildEntrada(bytes, "  Mi Documento.PDF  "));

        result["NombreNormalizado"].Should().Be("mi documento.pdf");
    }

    [Fact]
    public async Task Run_NombreYaMinusculas_NombreNormalizadoSinCambios()
    {
        var bytes = new byte[] { 1 };

        var result = await _sut.Run(BuildEntrada(bytes, "documento.pdf"));

        result["NombreNormalizado"].Should().Be("documento.pdf");
    }

    [Fact]
    public async Task Run_Resultado_ContieneFechaNormalizacionReciente()
    {
        var bytes = new byte[] { 1 };
        var before = DateTime.UtcNow.AddSeconds(-1);

        var result = await _sut.Run(BuildEntrada(bytes));

        result.Should().ContainKey("FechaNormalizacion");
        var fecha = (DateTime)result["FechaNormalizacion"];
        fecha.Should().BeOnOrAfter(before);
    }

    [Fact]
    public async Task Run_Resultado_ContieneTodosLasClavesEsperadas()
    {
        var bytes = new byte[] { 0x01 };

        var result = await _sut.Run(BuildEntrada(bytes));

        result.Should().ContainKeys("SHA256", "MD5", "CRC32", "TamañoBytes", "NombreNormalizado", "FechaNormalizacion");
    }

    [Fact]
    public async Task Run_MismoContenido_RetornaMismosHashesDeterministas()
    {
        var bytes = new byte[] { 10, 20, 30, 40 };
        var entrada = BuildEntrada(bytes);

        var result1 = await _sut.Run(entrada);
        var result2 = await _sut.Run(entrada);

        result1["SHA256"].Should().Be(result2["SHA256"]);
        result1["MD5"].Should().Be(result2["MD5"]);
        result1["CRC32"].Should().Be(result2["CRC32"]);
    }

    [Fact]
    public async Task Run_ContenidoDiferente_RetornaHashesDiferentes()
    {
        var bytes1 = new byte[] { 1, 2, 3 };
        var bytes2 = new byte[] { 4, 5, 6 };

        var result1 = await _sut.Run(BuildEntrada(bytes1));
        var result2 = await _sut.Run(BuildEntrada(bytes2));

        result1["SHA256"].Should().NotBe(result2["SHA256"]);
        result1["MD5"].Should().NotBe(result2["MD5"]);
        result1["CRC32"].Should().NotBe(result2["CRC32"]);
    }
}
