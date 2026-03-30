using DocumentIA.Core.Models;

namespace DocumentIA.Functions.Abstractions;

public interface IClasificarDataProvider
{
    Task<ResultadoClasificacion> ClasificarAsync(ClasificacionInput input, CancellationToken cancellationToken = default);
}
