using System.Threading;
using System.Threading.Tasks;
using DocumentIA.Core.Models;

namespace DocumentIA.Core.Services
{
    public interface IGdcService
    {
        /// <summary>
        /// Consulta si existe un documento para el IdActivo y huella MD5 indicada.
        /// Devuelve un tuple (Exists, ObjectId) donde ObjectId es null si no existe.
        /// </summary>
        Task<(bool Exists, string? ObjectId)> ConsultarDocumentoAsync(string idActivo, string md5, string matricula, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sube un documento al GDC y devuelve el resultado detallado.
        /// </summary>
        Task<ResultadoGDC> SubirDocumentoAsync(SubirGDCInput input, CancellationToken cancellationToken = default);

        /// <summary>
        /// Obtiene metadatos de un documento en GDC por object id sin descargar contenido.
        /// </summary>
        Task<GdcDocumentoMetadatos> ObtenerMetadatosDocumentoAsync(string objectId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Descarga un documento de GDC por object id incluyendo el contenido Base64.
        /// </summary>
        Task<ObtenerDocumentoGDCResult> ObtenerDocumentoAsync(string objectId, CancellationToken cancellationToken = default);
    }
}
