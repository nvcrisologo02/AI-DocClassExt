using System.Text.Json;
using DocumentIA.Data.Context;
using DocumentIA.Data.Entities;
using DocumentIA.Data.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace DocumentIA.Tests.Unit.Repositories;

public class TipologiaRepositoryAuditTests
{
    [Fact]
    public async Task AddAsync_Should_Create_Created_Audit_Record()
    {
        await using var context = CreateContext();
        var auditRepository = new TipologiaConfigAuditRepository(context);
        var repository = new TipologiaRepository(context, auditRepository);

        var tipologia = new TipologiaEntity
        {
            Codigo = "audit-test-created",
            Nombre = "Audit Created",
            Version = "1.0",
            ConfiguracionJson = "{\"tipologiaId\":\"audit-test-created\",\"version\":\"1.0\"}",
            Estado = EstadoTipologia.Draft,
            Activa = true
        };

        var created = await repository.AddAsync(tipologia, "tester");

        var auditRows = await auditRepository.GetByTipologiaIdAsync(created.Id);
        auditRows.Should().ContainSingle();

        var audit = auditRows.Single();
        audit.Accion.Should().Be("Created");
        audit.Usuario.Should().Be("tester");

        var details = JsonDocument.Parse(audit.DetallesJson!);
        details.RootElement.GetProperty("before").ValueKind.Should().Be(JsonValueKind.Null);
        details.RootElement.GetProperty("after").GetProperty("Codigo").GetString().Should().Be("audit-test-created");
    }

    [Fact]
    public async Task PublicarAsync_And_RetirarAsync_Should_Create_Audit_Records()
    {
        await using var context = CreateContext();
        var auditRepository = new TipologiaConfigAuditRepository(context);
        var repository = new TipologiaRepository(context, auditRepository);

        var tipologia = await repository.AddAsync(new TipologiaEntity
        {
            Codigo = "audit-test-state",
            Nombre = "Audit State",
            Version = "2.1",
            ConfiguracionJson = "{\"tipologiaId\":\"audit-test-state\",\"version\":\"2.1\"}",
            Estado = EstadoTipologia.Draft,
            Activa = true
        }, "creator");

        await repository.PublicarAsync(tipologia.Id, "publisher");
        await repository.RetirarAsync(tipologia.Id, "retirer");

        var auditRows = await auditRepository.GetByTipologiaIdAsync(tipologia.Id);

        auditRows.Select(x => x.Accion).Should().ContainInOrder("Retired", "Published", "Created");
        auditRows.Should().Contain(x => x.Accion == "Published" && x.Usuario == "publisher");
        auditRows.Should().Contain(x => x.Accion == "Retired" && x.Usuario == "retirer");
    }

    [Fact]
    public async Task PasarADraftAsync_Should_Create_Draft_Audit_Record()
    {
        await using var context = CreateContext();
        var auditRepository = new TipologiaConfigAuditRepository(context);
        var repository = new TipologiaRepository(context, auditRepository);

        var tipologia = await repository.AddAsync(new TipologiaEntity
        {
            Codigo = "audit-test-draft",
            Nombre = "Audit Draft",
            Version = "3.0",
            ConfiguracionJson = "{\"tipologiaId\":\"audit-test-draft\",\"version\":\"3.0\"}",
            Estado = EstadoTipologia.Retired,
            Activa = true
        }, "creator");

        await repository.PasarADraftAsync(tipologia.Id, "drafter");

        var latest = (await auditRepository.GetByTipologiaIdAsync(tipologia.Id)).First();
        latest.Accion.Should().Be("Draft");
        latest.Usuario.Should().Be("drafter");
    }

    private static DocumentIADbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<DocumentIADbContext>()
            .UseInMemoryDatabase($"tipologia-audit-tests-{Guid.NewGuid()}")
            .Options;

        return new DocumentIADbContext(options);
    }
}
