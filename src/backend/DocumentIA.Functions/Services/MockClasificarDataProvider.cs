using DocumentIA.Core.Models;
using DocumentIA.Functions.Abstractions;

namespace DocumentIA.Functions.Services;

public class MockClasificarDataProvider : IClasificarDataProvider
{
    public Task<ResultadoClasificacion> ClasificarAsync(ClasificacionInput input, CancellationToken cancellationToken = default)
    {
        var resultado = new ResultadoClasificacion
        {
            Modelo = "mock-classifier-v1",
            Confianza = 0.95,
            FallbackLLM = false,
            TipologiaDetectada = "Tasacion"
        };

        return Task.FromResult(resultado);
    }
}
