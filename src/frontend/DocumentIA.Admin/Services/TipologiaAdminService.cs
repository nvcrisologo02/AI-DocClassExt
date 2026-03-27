using System.Text.Json;
using DocumentIA.Core.Configuration;
using DocumentIA.Data.Context;
using DocumentIA.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace DocumentIA.Admin.Services;

public class TipologiaAdminService
{
    private readonly DocumentIADbContext _dbContext;

    public TipologiaAdminService(DocumentIADbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<TipologiaEntity>> GetTipologiasAsync()
    {
        return await _dbContext.Tipologias
            .OrderBy(t => t.Nombre)
            .ToListAsync();
    }

    public async Task<TipologiaEntity?> GetTipologiaAsync(int id)
    {
        return await _dbContext.Tipologias.FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<TipologiaEntity> SaveTipologiaAsync(TipologiaEntity tipologia)
    {
        tipologia.FechaActualizacion = DateTime.UtcNow;

        if (tipologia.Id == 0)
        {
            tipologia.FechaCreacion = DateTime.UtcNow;
            tipologia.Estado = EstadoTipologia.Draft;
            _dbContext.Tipologias.Add(tipologia);
        }
        else
        {
            _dbContext.Tipologias.Update(tipologia);
        }

        await _dbContext.SaveChangesAsync();
        return tipologia;
    }

    public async Task PublishTipologiaAsync(int id, string usuario)
    {
        var tipologia = await _dbContext.Tipologias.FirstOrDefaultAsync(t => t.Id == id)
            ?? throw new InvalidOperationException($"No existe tipologia con id {id}.");

        tipologia.Estado = EstadoTipologia.Published;
        tipologia.PublicadaEn = DateTime.UtcNow;
        tipologia.PublicadaPor = usuario;
        tipologia.VersionPublicada = tipologia.Version;
        tipologia.FechaActualizacion = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();
    }

    public async Task RetireTipologiaAsync(int id)
    {
        var tipologia = await _dbContext.Tipologias.FirstOrDefaultAsync(t => t.Id == id)
            ?? throw new InvalidOperationException($"No existe tipologia con id {id}.");

        tipologia.Estado = EstadoTipologia.Retired;
        tipologia.FechaActualizacion = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();
    }

    public async Task<IReadOnlyCollection<ModeloConfigEntity>> GetModelosByTipoAsync(TipoModelo tipo)
    {
        return await _dbContext.ModeloConfigs
            .Where(m => m.Tipo == tipo)
            .OrderBy(m => m.Key)
            .ToListAsync();
    }

    public async Task<ModeloConfigEntity?> GetModeloByIdAsync(int id)
    {
        return await _dbContext.ModeloConfigs.FirstOrDefaultAsync(m => m.Id == id);
    }

    public async Task<ModeloConfigEntity> SaveModeloAsync(ModeloConfigEntity modelo)
    {
        modelo.FechaActualizacion = DateTime.UtcNow;

        if (modelo.Id == 0)
        {
            modelo.FechaCreacion = DateTime.UtcNow;
            _dbContext.ModeloConfigs.Add(modelo);
        }
        else
        {
            _dbContext.ModeloConfigs.Update(modelo);
        }

        await _dbContext.SaveChangesAsync();
        return modelo;
    }

    public static IReadOnlyCollection<string> ValidarConfiguracionJson(string json)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(json))
        {
            errors.Add("ConfiguracionJson no puede estar vacío.");
            return errors;
        }

        try
        {
            var config = JsonSerializer.Deserialize<TipologiaValidationConfig>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (config is null)
            {
                errors.Add("No se pudo deserializar la configuración.");
                return errors;
            }

            if (string.IsNullOrWhiteSpace(config.TipologiaId))
            {
                errors.Add("tipologiaId es obligatorio.");
            }

            if (string.IsNullOrWhiteSpace(config.Version))
            {
                errors.Add("version es obligatorio.");
            }
        }
        catch (Exception ex)
        {
            errors.Add($"JSON inválido: {ex.Message}");
        }

        return errors;
    }
}
