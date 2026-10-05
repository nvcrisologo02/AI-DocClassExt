#nullable enable
using DocumentIA.Data.Context;
using DocumentIA.Data.Entities;
using DocumentIA.Data.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace DocumentIA.Tests.Unit.Repositories;

/// <summary>
/// AB#100863: la verificacion de duplicados por MD5 solo necesita Id y SHA256 del documento.
/// El repositorio los proyecta sin cargar la entidad ni su Resultado.
/// </summary>
public class DocumentoRepositoryMd5Tests : IDisposable
{
    private readonly DocumentIADbContext _context;
    private readonly DocumentoRepository _sut;

    public DocumentoRepositoryMd5Tests()
    {
        var options = new DbContextOptionsBuilder<DocumentIADbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new DocumentIADbContext(options);
        _sut = new DocumentoRepository(_context);
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task GetDuplicadoByMD5Async_ConDocumentoExistente_DevuelveIdYSha256()
    {
        var doc = NuevoDocumento(md5: "md5-existente", sha256: "sha-existente");
        _context.Documentos.Add(doc);
        _context.Documentos.Add(NuevoDocumento(md5: "md5-otro", sha256: "sha-otro"));
        await _context.SaveChangesAsync();

        var resultado = await _sut.GetDuplicadoByMD5Async("md5-existente");

        resultado.Should().NotBeNull();
        resultado!.Id.Should().Be(doc.Id);
        resultado.SHA256.Should().Be("sha-existente");
    }

    [Fact]
    public async Task GetDuplicadoByMD5Async_SinDocumento_DevuelveNull()
    {
        _context.Documentos.Add(NuevoDocumento(md5: "md5-otro", sha256: "sha-otro"));
        await _context.SaveChangesAsync();

        var resultado = await _sut.GetDuplicadoByMD5Async("md5-inexistente");

        resultado.Should().BeNull();
    }

    private static DocumentoEntity NuevoDocumento(string md5, string sha256) => new()
    {
        Guid = Guid.NewGuid().ToString(),
        NombreArchivo = "doc.pdf",
        SHA256 = sha256,
        MD5 = md5,
        CRC32 = "crc",
        TamanoBytes = 10,
        Tipologia = "nota.simple.1_0",
        Estado = "OK",
        Paginas = 1
    };
}
