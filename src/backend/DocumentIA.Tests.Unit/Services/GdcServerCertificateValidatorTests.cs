using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using DocumentIA.Core.Services;
using DocumentIA.Tests.Unit.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace DocumentIA.Tests.Unit.Services
{
    public class GdcServerCertificateValidatorTests
    {
        private static GdcServerCertificateValidator Crear(params X509Certificate2[] confianza)
            => new(new X509Certificate2Collection(confianza), NullLogger.Instance);

        private static X509Chain CadenaConExtra(params X509Certificate2[] extra)
        {
            var c = new X509Chain();
            c.ChainPolicy.ExtraStore.AddRange(extra);
            return c;
        }

        [Fact]
        public void Validate_CadenaCorrectaConRaizConfiada_Acepta()
        {
            var (raiz, emisora, hoja) = PkiDePrueba.Crear();
            var validador = Crear(raiz);

            var ok = validador.Validate(PkiDePrueba.SinClavePrivada(hoja), CadenaConExtra(emisora), SslPolicyErrors.RemoteCertificateChainErrors);

            ok.Should().BeTrue();
        }

        [Fact]
        public void Validate_SinIntermediaEnHandshake_AceptaSiLaEmisoraEstaEnConfianza()
        {
            var (raiz, emisora, hoja) = PkiDePrueba.Crear();
            var validador = Crear(raiz, emisora);

            var ok = validador.Validate(PkiDePrueba.SinClavePrivada(hoja), new X509Chain(), SslPolicyErrors.RemoteCertificateChainErrors);

            ok.Should().BeTrue();
        }

        [Fact]
        public void Validate_SinIntermediaNiEmisoraConfiada_Rechaza()
        {
            var (raiz, _, hoja) = PkiDePrueba.Crear();
            var validador = Crear(raiz);

            var ok = validador.Validate(PkiDePrueba.SinClavePrivada(hoja), new X509Chain(), SslPolicyErrors.RemoteCertificateChainErrors);

            ok.Should().BeFalse();
        }

        [Fact]
        public void Validate_HojaDeOtraRaiz_Rechaza()
        {
            var (raizA, _, _) = PkiDePrueba.Crear();
            var (_, emisoraB, hojaB) = PkiDePrueba.Crear();
            var validador = Crear(raizA);

            var ok = validador.Validate(PkiDePrueba.SinClavePrivada(hojaB), CadenaConExtra(emisoraB), SslPolicyErrors.RemoteCertificateChainErrors);

            ok.Should().BeFalse();
        }

        [Fact]
        public void Validate_NombreNoCoincide_Rechaza()
        {
            var (raiz, emisora, hoja) = PkiDePrueba.Crear();
            var validador = Crear(raiz);

            var ok = validador.Validate(PkiDePrueba.SinClavePrivada(hoja), CadenaConExtra(emisora), SslPolicyErrors.RemoteCertificateNameMismatch);

            ok.Should().BeFalse();
        }

        [Fact]
        public void Validate_SinCertificado_Rechaza()
        {
            var (raiz, _, _) = PkiDePrueba.Crear();
            var validador = Crear(raiz);

            var ok = validador.Validate(null, null, SslPolicyErrors.RemoteCertificateNotAvailable);

            ok.Should().BeFalse();
        }

        [Fact]
        public void Validate_HojaCaducada_Rechaza()
        {
            var (raiz, emisora, hoja) = PkiDePrueba.Crear(hojaNotAfter: DateTimeOffset.UtcNow.AddDays(-1));
            var validador = Crear(raiz);

            var ok = validador.Validate(PkiDePrueba.SinClavePrivada(hoja), CadenaConExtra(emisora), SslPolicyErrors.RemoteCertificateChainErrors);

            ok.Should().BeFalse();
        }

        [Fact]
        public void Validate_HojaSinEkuDeServidor_Rechaza()
        {
            var (raiz, emisora, hoja) = PkiDePrueba.Crear(conEkuServidor: false);
            var validador = Crear(raiz);

            var ok = validador.Validate(PkiDePrueba.SinClavePrivada(hoja), CadenaConExtra(emisora), SslPolicyErrors.RemoteCertificateChainErrors);

            ok.Should().BeFalse();
        }

        [Fact]
        public void Validate_SinErroresDePolitica_SigueExigiendoLaRaizConfiada()
        {
            var (raizA, _, _) = PkiDePrueba.Crear();
            var (_, emisoraB, hojaB) = PkiDePrueba.Crear();
            var validador = Crear(raizA);

            var ok = validador.Validate(PkiDePrueba.SinClavePrivada(hojaB), CadenaConExtra(emisoraB), SslPolicyErrors.None);

            ok.Should().BeFalse();
        }

        [Fact]
        public void Validate_Rechazo_TrazaWarningConSujetoYEmisor()
        {
            var (raizA, _, _) = PkiDePrueba.Crear();
            var (_, emisoraB, hojaB) = PkiDePrueba.Crear();
            var hoja = PkiDePrueba.SinClavePrivada(hojaB);
            var logger = new Mock<ILogger>();
            var validador = new GdcServerCertificateValidator(new X509Certificate2Collection(raizA), logger.Object);

            var ok = validador.Validate(hoja, CadenaConExtra(emisoraB), SslPolicyErrors.RemoteCertificateChainErrors);

            ok.Should().BeFalse();
            logger.Verify(l => l.Log(
                It.Is<LogLevel>(n => n == LogLevel.Warning),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains(hoja.Subject) && o.ToString()!.Contains(hoja.Issuer)),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        private static string PemDe(params X509Certificate2[] certs)
            => string.Concat(Array.ConvertAll(certs, c => c.ExportCertificatePem() + "\n"));

        [Fact]
        public void LoadTrustStore_DesdePemEnSetting_CargaTodosLosCertificados()
        {
            var (raiz, emisora, _) = PkiDePrueba.Crear();

            var almacen = GdcServerCertificateValidator.LoadTrustStore(PemDe(raiz, emisora), "no-se-usa.pem");

            almacen.Count.Should().Be(2);
        }

        [Fact]
        public void LoadTrustStore_PemConCrlf_Carga()
        {
            var (raiz, emisora, _) = PkiDePrueba.Crear();
            var pem = "  " + PemDe(raiz, emisora).Replace("\n", "\r\n");

            var almacen = GdcServerCertificateValidator.LoadTrustStore(pem, "no-se-usa.pem");

            almacen.Count.Should().Be(2);
        }

        [Fact]
        public void LoadTrustStore_PemSinSaltosDeLinea_CargaPorqueImportFromPemLoTolera()
        {
            var (raiz, _, _) = PkiDePrueba.Crear();
            var pem = raiz.ExportCertificatePem().Replace("\r", string.Empty).Replace("\n", string.Empty);

            var almacen = GdcServerCertificateValidator.LoadTrustStore(pem, "no-se-usa.pem");

            almacen.Count.Should().Be(1);
        }

        [Fact]
        public void LoadTrustStore_PemConSaltosEscapados_LanzaConMensajeDeOrigen()
        {
            var (raiz, _, _) = PkiDePrueba.Crear();
            var pem = raiz.ExportCertificatePem().Replace("\r", string.Empty).Replace("\n", "\\n");

            Action accion = () => GdcServerCertificateValidator.LoadTrustStore(pem, "no-se-usa.pem");

            accion.Should().Throw<InvalidOperationException>().Where(e => e.Message.Contains("GDC__TrustedRootCertificate"));
        }

        [Fact]
        public void LoadTrustStore_PemEnSettingTienePrioridadSobreElFichero()
        {
            var (raiz, emisora, _) = PkiDePrueba.Crear();
            var ruta = Path.Combine(Path.GetTempPath(), $"gdc-ca-{Guid.NewGuid():N}.pem");
            File.WriteAllText(ruta, PemDe(raiz, emisora));
            try
            {
                var almacen = GdcServerCertificateValidator.LoadTrustStore(raiz.ExportCertificatePem(), ruta);

                almacen.Count.Should().Be(1);
            }
            finally
            {
                File.Delete(ruta);
            }
        }

        [Fact]
        public void LoadTrustStore_DesdeFichero_Carga()
        {
            var (raiz, _, _) = PkiDePrueba.Crear();
            var ruta = Path.Combine(Path.GetTempPath(), $"gdc-ca-{Guid.NewGuid():N}.pem");
            File.WriteAllText(ruta, raiz.ExportCertificatePem());
            try
            {
                var almacen = GdcServerCertificateValidator.LoadTrustStore(string.Empty, ruta);

                almacen.Count.Should().Be(1);
            }
            finally
            {
                File.Delete(ruta);
            }
        }

        [Fact]
        public void LoadTrustStore_FicheroInexistente_Lanza()
        {
            var ruta = Path.Combine(Path.GetTempPath(), $"no-existe-{Guid.NewGuid():N}.pem");

            Action accion = () => GdcServerCertificateValidator.LoadTrustStore(null, ruta);

            accion.Should().Throw<FileNotFoundException>().Where(e => e.Message.Contains(ruta));
        }

        [Fact]
        public void LoadTrustStore_PemInvalido_Lanza()
        {
            Action accion = () => GdcServerCertificateValidator.LoadTrustStore("no soy un pem", "no-se-usa.pem");

            accion.Should().Throw<CryptographicException>();
        }

        [Fact]
        public void LoadTrustStore_FicheroDelRepo_ContieneLaRaizDeSareb()
        {
            var almacen = GdcServerCertificateValidator.LoadTrustStore(null, "config/certs/sareb-ca-chain.pem");

            almacen.Count.Should().Be(2);
            almacen.Cast<X509Certificate2>()
                .Select(c => c.GetCertHashString(HashAlgorithmName.SHA256))
                .Should().Contain("EA0A8EAB540A7390B86E0763D368B0D5E204F0205B35640313A6B2DBCE43A367");
        }
    }
}
