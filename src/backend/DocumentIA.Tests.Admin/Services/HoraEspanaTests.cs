using DocumentIA.Admin.Services;
using FluentAssertions;

namespace DocumentIA.Tests.Admin.Services;

// El backend serializa las fechas sin sufijo Z ni offset (p. ej.
// "2026-08-10T09:11:49.226798"), asi que llegan con Kind=Unspecified aunque el
// valor sea UTC. Con el contenedor del App Service corriendo en UTC,
// ToLocalTime() no aplicaba conversion alguna y la pagina mostraba UTC: dos
// horas menos que la hora local en verano.
public class HoraEspanaTests
{
    [Fact]
    public void Desde_EnVerano_AplicaDosHorasDeDiferencia()
    {
        var utc = new DateTime(2026, 8, 10, 9, 11, 49, DateTimeKind.Utc);

        var local = HoraEspana.Desde(utc);

        local.Hour.Should().Be(11, "en agosto la peninsula esta en CEST (UTC+2)");
        local.Minute.Should().Be(11);
    }

    [Fact]
    public void Desde_EnInvierno_AplicaUnaHoraDeDiferencia()
    {
        var utc = new DateTime(2026, 1, 15, 9, 0, 0, DateTimeKind.Utc);

        var local = HoraEspana.Desde(utc);

        local.Hour.Should().Be(10, "en enero la peninsula esta en CET (UTC+1)");
    }

    // El caso real: el valor viene del backend sin marca de zona.
    [Fact]
    public void Desde_ConKindUnspecified_LoInterpretaComoUtc()
    {
        var sinMarca = new DateTime(2026, 8, 10, 9, 11, 49, DateTimeKind.Unspecified);

        var local = HoraEspana.Desde(sinMarca);

        local.Hour.Should().Be(11,
            "el backend genera las fechas con UtcNow y las serializa sin Z; asumir lo contrario desplaza la hora");
    }

    // Un DateTime marcado como local en el servidor (UTC) representa el mismo
    // instante que ese UTC: convertirlo debe dar igualmente hora peninsular.
    [Fact]
    public void Desde_ConKindLocal_NoDuplicaLaConversion()
    {
        var utc = new DateTime(2026, 8, 10, 9, 11, 49, DateTimeKind.Utc);

        var local = HoraEspana.Desde(utc.ToLocalTime());

        local.Should().Be(HoraEspana.Desde(utc),
            "el mismo instante debe producir la misma hora peninsular, venga marcado como sea");
    }

    [Fact]
    public void Desde_DateTimeOffset_UsaElInstanteAbsoluto()
    {
        // Mismo instante expresado en un huso distinto (Nueva York, UTC-4 en agosto).
        var offset = new DateTimeOffset(2026, 8, 10, 5, 11, 49, TimeSpan.FromHours(-4));

        var local = HoraEspana.Desde(offset);

        local.Hour.Should().Be(11);
    }

    [Fact]
    public void Formatear_ProduceLaHoraPeninsular()
    {
        var utc = new DateTime(2026, 8, 10, 9, 11, 49, DateTimeKind.Utc);

        HoraEspana.Formatear(utc, "dd/MM/yyyy HH:mm").Should().Be("10/08/2026 11:11");
    }

    [Fact]
    public void Formatear_ValorNulo_DevuelveGuion()
    {
        HoraEspana.Formatear(null, "dd/MM/yyyy").Should().Be("—");
    }

    // El cambio de hora cae el ultimo domingo de octubre a las 03:00 locales.
    [Fact]
    public void Desde_EnElCambioDeHoraDeOctubre_RespetaElSalto()
    {
        var antes = new DateTime(2026, 10, 25, 0, 30, 0, DateTimeKind.Utc);
        var despues = new DateTime(2026, 10, 25, 1, 30, 0, DateTimeKind.Utc);

        HoraEspana.Desde(antes).Hour.Should().Be(2, "todavia en CEST (UTC+2)");
        HoraEspana.Desde(despues).Hour.Should().Be(2, "ya en CET (UTC+1): la hora 02 se repite");
    }
}
