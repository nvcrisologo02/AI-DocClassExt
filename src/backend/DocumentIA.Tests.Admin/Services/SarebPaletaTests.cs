using DocumentIA.Admin.Services;
using FluentAssertions;

namespace DocumentIA.Tests.Admin.Services;

// Valores copiados del manual de identidad (1.3) y de la guia de estilo (6.6). El
// color 7 aparece impreso como "GF927C" en el manual: es una errata de 6F927C.
public class SarebPaletaTests
{
    [Fact]
    public void ColoresCorporativos_SonLosDelManual()
    {
        SarebPaleta.Verde.Should().Be("#00858C");
        SarebPaleta.AzulOscuro.Should().Be("#046E97");
        SarebPaleta.Azul.Should().Be("#0086AD");
        SarebPaleta.Turquesa.Should().Be("#00ABA8");
        SarebPaleta.VerdeClaro.Should().Be("#90CCB7");
        SarebPaleta.VerdeGris.Should().Be("#6F927C");
        SarebPaleta.Gris.Should().Be("#BBBEB4");
    }

    [Fact]
    public void ColoresAdicionales_SonLosDeLaGuiaDeEstilo()
    {
        SarebPaleta.GrisClaro.Should().Be("#AFB6AC");
        SarebPaleta.Naranja.Should().Be("#F39E48");
        SarebPaleta.Rojo.Should().Be("#EA6B43");
    }

    [Fact]
    public void Serie_VaDeMenosAMasIntensidad()
    {
        SarebPaleta.Serie.Should().Equal(
            "#AFB6AC", "#BBBEB4", "#90CCB7", "#00ABA8", "#0086AD", "#046E97", "#00858C");
    }

    [Fact]
    public void Estados_UsanTurquesaNaranjaRojoYGris()
    {
        SarebPaleta.Ok.Should().Be(SarebPaleta.Turquesa);
        SarebPaleta.Revision.Should().Be(SarebPaleta.Naranja);
        SarebPaleta.Error.Should().Be(SarebPaleta.Rojo);
        SarebPaleta.Neutro.Should().Be(SarebPaleta.Gris);
    }
}
