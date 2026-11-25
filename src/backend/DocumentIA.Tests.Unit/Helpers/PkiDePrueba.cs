using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace DocumentIA.Tests.Unit.Helpers
{
    /// <summary>
    /// Mini PKI de pruebas: raíz autofirmada, emisora intermedia y certificado hoja de servidor TLS.
    /// </summary>
    public static class PkiDePrueba
    {
        public static (X509Certificate2 Raiz, X509Certificate2 Emisora, X509Certificate2 Hoja) Crear(
            string dnsHoja = "gdc.pruebas.local",
            DateTimeOffset? hojaNotAfter = null,
            bool conEkuServidor = true)
        {
            var ahora = DateTimeOffset.UtcNow;
            var notBefore = ahora.AddDays(-3);
            var notAfter = ahora.AddYears(1);

            // Raíz autofirmada
            var claveRaiz = RSA.Create(2048);
            var reqRaiz = new CertificateRequest($"CN=Raiz Pruebas {Guid.NewGuid():N}", claveRaiz, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            reqRaiz.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
            reqRaiz.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
            var raiz = reqRaiz.CreateSelfSigned(notBefore, notAfter);

            // Emisora intermedia firmada por la raíz
            var claveEmisora = RSA.Create(2048);
            var reqEmisora = new CertificateRequest($"CN=Emisora Pruebas {Guid.NewGuid():N}", claveEmisora, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            reqEmisora.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, true, 0, true));
            reqEmisora.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
            using var emisoraSinClave = reqEmisora.Create(raiz, notBefore, notAfter, NuevoSerie());
            var emisora = emisoraSinClave.CopyWithPrivateKey(claveEmisora);

            // Hoja de servidor TLS firmada por la emisora
            var claveHoja = RSA.Create(2048);
            var reqHoja = new CertificateRequest($"CN={dnsHoja}", claveHoja, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            reqHoja.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
            reqHoja.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
            // Sin serverAuth la hoja lleva solo clientAuth: omitir la extensión la haría válida para cualquier uso.
            var ekuOid = conEkuServidor ? "1.3.6.1.5.5.7.3.1" : "1.3.6.1.5.5.7.3.2";
            reqHoja.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
                new OidCollection { new Oid(ekuOid) }, false));
            var san = new SubjectAlternativeNameBuilder();
            san.AddDnsName(dnsHoja);
            reqHoja.CertificateExtensions.Add(san.Build());

            var hojaNotBefore = hojaNotAfter.HasValue ? ahora.AddDays(-2) : ahora.AddDays(-1);
            using var hojaSinClave = reqHoja.Create(emisora, hojaNotBefore, hojaNotAfter ?? notAfter, NuevoSerie());
            var hoja = hojaSinClave.CopyWithPrivateKey(claveHoja);

            return (raiz, emisora, hoja);
        }

        /// <summary>Devuelve el certificado sin clave privada, como llega del servidor en el handshake.</summary>
        public static X509Certificate2 SinClavePrivada(X509Certificate2 c)
            => X509CertificateLoader.LoadCertificate(c.RawData);

        private static byte[] NuevoSerie()
        {
            var serie = new byte[8];
            RandomNumberGenerator.Fill(serie);
            serie[0] &= 0x7F;
            return serie;
        }
    }
}
