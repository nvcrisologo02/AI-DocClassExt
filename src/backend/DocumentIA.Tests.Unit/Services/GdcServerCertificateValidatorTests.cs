using System;
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
    }
}
