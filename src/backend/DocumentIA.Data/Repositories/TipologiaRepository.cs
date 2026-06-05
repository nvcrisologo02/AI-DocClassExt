using Microsoft.EntityFrameworkCore;
using DocumentIA.Data.Context;
using DocumentIA.Data.Entities;
using System.Text.Json;

namespace DocumentIA.Data.Repositories;

public class TipologiaRepository : ITipologiaRepository
{
    private readonly DocumentIADbContext _context;
    private readonly ITipologiaConfigAuditRepository _auditRepository;

    public TipologiaRepository(DocumentIADbContext context, ITipologiaConfigAuditRepository auditRepository)
    {
        _context = context;
        _auditRepository = auditRepository;
    }

    public async Task<TipologiaEntity?> GetByIdAsync(int id)
    {
        return await _context.Tipologias.FindAsync(id);
    }

    public async Task<TipologiaEntity?> GetByCodigoAsync(string codigo)
    {
        return await _context.Tipologias
            .FirstOrDefaultAsync(t => t.Codigo == codigo);
    }

    public async Task<IReadOnlyCollection<TipologiaEntity>> GetAllPublishedAsync()
    {
        return await _context.Tipologias
            .Where(t => t.Estado == EstadoTipologia.Published && t.Activa)
            .OrderBy(t => t.Nombre)
            .ToListAsync();
    }

    public async Task<IEnumerable<TipologiaEntity>> GetAllActivasAsync()
    {
        return await _context.Tipologias
            .Where(t => t.Activa)
            .OrderBy(t => t.Nombre)
            .ToListAsync();
    }

    public async Task<TipologiaEntity> AddAsync(TipologiaEntity tipologia, string? usuario = null)
    {
        _context.Tipologias.Add(tipologia);
        await _context.SaveChangesAsync();

        await AddAuditAsync(tipologia.Id, "Created", usuario, before: null, after: tipologia);
        return tipologia;
    }

    public async Task UpdateAsync(TipologiaEntity tipologia, string? usuario = null, string accion = "Updated")
    {
        var before = await _context.Tipologias.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tipologia.Id);
        tipologia.FechaActualizacion = DateTime.UtcNow;
        _context.Tipologias.Update(tipologia);
        await _context.SaveChangesAsync();

        await AddAuditAsync(tipologia.Id, accion, usuario, before, tipologia);
    }

    public async Task PublicarAsync(int id, string publicadaPor)
    {
        var tipologia = await _context.Tipologias.FindAsync(id)
            ?? throw new KeyNotFoundException($"No se encontro la tipologia con id '{id}'.");

        var before = CloneForAudit(tipologia);

        tipologia.Estado = EstadoTipologia.Published;
        tipologia.PublicadaEn = DateTime.UtcNow;
        tipologia.PublicadaPor = publicadaPor;
        tipologia.VersionPublicada = tipologia.Version;
        tipologia.FechaActualizacion = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        await AddAuditAsync(tipologia.Id, "Published", publicadaPor, before, tipologia);
    }

    public async Task RetirarAsync(int id, string? retiradaPor = null)
    {
        var tipologia = await _context.Tipologias.FindAsync(id)
            ?? throw new KeyNotFoundException($"No se encontro la tipologia con id '{id}'.");

        var before = CloneForAudit(tipologia);

        tipologia.Estado = EstadoTipologia.Retired;
        tipologia.FechaActualizacion = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        await AddAuditAsync(tipologia.Id, "Retired", retiradaPor, before, tipologia);
    }

    public async Task PasarADraftAsync(int id, string? usuario = null)
    {
        var tipologia = await _context.Tipologias.FindAsync(id)
            ?? throw new KeyNotFoundException($"No se encontro la tipologia con id '{id}'.");

        var before = CloneForAudit(tipologia);

        tipologia.Estado = EstadoTipologia.Draft;
        tipologia.FechaActualizacion = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        await AddAuditAsync(tipologia.Id, "Draft", usuario, before, tipologia);
    }

    public async Task DeleteAsync(int id)
    {
        var tipologia = await _context.Tipologias.FindAsync(id);
        if (tipologia != null)
        {
            var before = CloneForAudit(tipologia);
            _context.Tipologias.Remove(tipologia);
            await _context.SaveChangesAsync();
            await AddAuditAsync(id, "Deleted", null, before, null);
        }
    }

    private static object? Snapshot(TipologiaEntity? tipologia)
    {
        if (tipologia is null)
        {
            return null;
        }

        return new
        {
            tipologia.Id,
            tipologia.Codigo,
            tipologia.Nombre,
            tipologia.Version,
            tipologia.Estado,
            tipologia.ConfiguracionJson,
            tipologia.PublicadaEn,
            tipologia.PublicadaPor,
            tipologia.VersionPublicada,
            tipologia.FechaCreacion,
            tipologia.FechaActualizacion
        };
    }

    private static TipologiaEntity CloneForAudit(TipologiaEntity source)
    {
        #pragma warning disable CS0618
        return new TipologiaEntity
        {
            Id = source.Id,
            Codigo = source.Codigo,
            Nombre = source.Nombre,
            Version = source.Version,
            Activa = source.Activa,
            ModeloClasificacionDI = source.ModeloClasificacionDI,
            UmbralClasificacion = source.UmbralClasificacion,
            ModeloExtraccionDI = source.ModeloExtraccionDI,
            UmbralExtraccion = source.UmbralExtraccion,
            PromptGPT = source.PromptGPT,
            ConfiguracionJson = source.ConfiguracionJson,
            FechaCreacion = source.FechaCreacion,
            FechaActualizacion = source.FechaActualizacion,
            CreadoPor = source.CreadoPor,
            Estado = source.Estado,
            PublicadaEn = source.PublicadaEn,
            PublicadaPor = source.PublicadaPor,
            VersionPublicada = source.VersionPublicada
        };
        #pragma warning restore CS0618
    }

    private async Task AddAuditAsync(int tipologiaId, string accion, string? usuario, TipologiaEntity? before, TipologiaEntity? after)
    {
        var payload = JsonSerializer.Serialize(new
        {
            before = Snapshot(before),
            after = Snapshot(after)
        });

        await _auditRepository.AddAsync(new TipologiaConfigAuditEntity
        {
            TipologiaId = tipologiaId,
            Accion = accion,
            Usuario = string.IsNullOrWhiteSpace(usuario) ? "system" : usuario,
            FechaHora = DateTime.UtcNow,
            DetallesJson = payload
        });
    }
}
