using System;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;

namespace DocumentIA.Core.Services
{
    /// <summary>
    /// Valida el certificado del servidor del GDC contra una raíz de confianza propia
    /// (CA interna), sin confiar nunca en las raíces del sistema.
    /// </summary>
    public sealed class GdcServerCertificateValidator
    {
        private readonly X509Certificate2Collection _certificadosConfianza;
        private readonly ILogger _logger;

        public GdcServerCertificateValidator(X509Certificate2Collection certificadosConfianza, ILogger logger)
        {
            _certificadosConfianza = certificadosConfianza ?? throw new ArgumentNullException(nameof(certificadosConfianza));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Carga las CA de confianza. El PEM del setting tiene prioridad sobre el fichero;
        /// la ruta relativa se resuelve contra <see cref="AppContext.BaseDirectory"/>.
        /// </summary>
        public static X509Certificate2Collection LoadTrustStore(string? pem, string ruta)
        {
            var texto = pem?.Trim();
            if (string.IsNullOrEmpty(texto))
            {
                var resuelta = Path.IsPathRooted(ruta) ? ruta : Path.Combine(AppContext.BaseDirectory, ruta);
                if (!File.Exists(resuelta))
                {
                    throw new FileNotFoundException(
                        $"No se encontró el fichero de certificados de confianza del GDC: {resuelta}", resuelta);
                }

                texto = File.ReadAllText(resuelta).Trim();
            }

            if (!texto.Contains("-----BEGIN CERTIFICATE-----", StringComparison.Ordinal))
            {
                throw new CryptographicException("El PEM de confianza del GDC no contiene ningún certificado.");
            }

            var coleccion = new X509Certificate2Collection();
            coleccion.ImportFromPem(texto);
            if (coleccion.Count == 0)
            {
                throw new InvalidOperationException("El almacén de confianza del GDC está vacío.");
            }

            return coleccion;
        }

        public bool Validate(X509Certificate2? certificado, X509Chain? cadena, SslPolicyErrors errores)
        {
            try
            {
                if (certificado is null || errores.HasFlag(SslPolicyErrors.RemoteCertificateNotAvailable))
                {
                    _logger.LogWarning("Certificado del GDC rechazado: el servidor no presentó certificado (errores: {Errores}).", errores);
                    return false;
                }

                if (errores.HasFlag(SslPolicyErrors.RemoteCertificateNameMismatch))
                {
                    _logger.LogWarning(
                        "Certificado del GDC rechazado: el nombre no coincide. Sujeto: {Sujeto}; emisor: {Emisor}.",
                        certificado.Subject, certificado.Issuer);
                    return false;
                }

                using var propia = new X509Chain();
                propia.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                propia.ChainPolicy.CustomTrustStore.AddRange(_certificadosConfianza);
                propia.ChainPolicy.DisableCertificateDownloads = true;
                propia.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                propia.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;

                if (cadena is not null)
                {
                    propia.ChainPolicy.ExtraStore.AddRange(cadena.ChainPolicy.ExtraStore);
                    foreach (var elemento in cadena.ChainElements)
                    {
                        propia.ChainPolicy.ExtraStore.Add(elemento.Certificate);
                    }
                }

                if (propia.Build(certificado))
                {
                    return true;
                }

                var estados = string.Join("; ", propia.ChainStatus.Select(s => $"{s.Status}: {s.StatusInformation?.Trim()}"));
                _logger.LogWarning(
                    "Certificado del GDC rechazado: la cadena no llega a una raíz de confianza. Sujeto: {Sujeto}; emisor: {Emisor}; errores de política: {Errores}; estado de la cadena: {Estados}.",
                    certificado.Subject, certificado.Issuer, errores, estados);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Certificado del GDC rechazado: error al validar la cadena.");
                return false;
            }
        }
    }
}
