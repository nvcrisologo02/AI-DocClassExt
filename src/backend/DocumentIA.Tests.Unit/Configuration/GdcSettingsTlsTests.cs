using System.Collections.Generic;
using DocumentIA.Core.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace DocumentIA.Tests.Unit.Configuration
{
    public class GdcSettingsTlsTests
    {
        private static GdcSettings Enlazar(params (string Clave, string Valor)[] valores)
        {
            var datos = new Dictionary<string, string?>();
            foreach (var (clave, valor) in valores)
            {
                datos[clave] = valor;
            }

            return new ConfigurationBuilder()
                .AddInMemoryCollection(datos)
                .Build()
                .GetSection("GDC")
                .Get<GdcSettings>() ?? new GdcSettings();
        }

        [Fact]
        public void GdcSettings_BypassSslValidation_PorDefectoFalse()
        {
            Enlazar(("GDC:Endpoint", "https://gdc.invalid")).BypassSslValidation.Should().BeFalse();
        }

        [Fact]
        public void GdcSettings_BypassSslValidation_SeEnlazaSinDistinguirMayusculas()
        {
            Enlazar(("GDC:BypassSslValidation", "True")).BypassSslValidation.Should().BeTrue();
        }

        [Fact]
        public void GdcSettings_TrustedRootCertificatePath_PorDefectoEsElPemDelRepo()
        {
            Enlazar(("GDC:Endpoint", "https://gdc.invalid")).TrustedRootCertificatePath
                .Should().Be("config/certs/sareb-ca-chain.pem");
        }
    }
}
