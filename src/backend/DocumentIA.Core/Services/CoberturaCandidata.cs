// src/backend/DocumentIA.Core/Services/CoberturaCandidata.cs
using DocumentIA.Core.Models;

namespace DocumentIA.Core.Services;

/// <summary>
/// Cobertura de paginas de una ejecucion historica frente a la necesidad de la peticion
/// (AB#100930, spec seccion 10). Con cobertura de markdown conocida delega en la regla unica
/// del resolutor (ResultadoMarkdown.Cubre); con cobertura desconocida (filas anteriores a
/// AB#100245) solo se puede juzgar por el recorte de clasificacion.
/// </summary>
public static class CoberturaCandidata
{
    public static bool Cubre(DetalleEjecucion detalle, NecesidadMarkdown necesidad)
    {
        ArgumentNullException.ThrowIfNull(detalle);
        ArgumentNullException.ThrowIfNull(necesidad);

        var coberturaConocida = detalle.MarkdownCompleto || detalle.MarkdownPaginas > 0;
        if (coberturaConocida)
        {
            var markdown = new ResultadoMarkdown
            {
                Markdown = "-", // solo para que TieneContenido sea true: aqui se juzga cobertura, no texto
                Paginas = detalle.MarkdownPaginas,
                Completo = detalle.MarkdownCompleto
            };

            if (!markdown.Cubre(necesidad))
            {
                return false;
            }

            return CubreRecorte(detalle, necesidad);
        }

        if (!detalle.RecorteAplicado)
        {
            return true;
        }

        return CubreRecorte(detalle, necesidad);
    }

    /// <summary>La clasificacion historica tiene que haber leido al menos las paginas pedidas.</summary>
    private static bool CubreRecorte(DetalleEjecucion detalle, NecesidadMarkdown necesidad)
    {
        if (!detalle.RecorteAplicado || detalle.PaginasIncluidas <= 0)
        {
            return true;
        }

        if (necesidad.DocumentoCompleto)
        {
            return detalle.MarkdownCompleto;
        }

        return detalle.PaginasIncluidas >= necesidad.PaginasMinimas;
    }
}
