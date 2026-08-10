namespace DocumentIA.Admin.Services;

/// <summary>
/// Conversion de instantes a hora peninsular espanola para la capa de
/// presentacion.
/// </summary>
/// <remarks>
/// No se usa <c>ToLocalTime()</c> en ningun punto de la interfaz: en Blazor
/// Server el render ocurre en el servidor, asi que "local" es la zona del
/// contenedor, no la del navegador del usuario. El App Service corre en UTC,
/// de modo que la conversion era la identidad y la pagina mostraba UTC (dos
/// horas menos que la hora peninsular en verano).
///
/// Ajustar WEBSITE_TIME_ZONE tampoco es la solucion: obligaria a mantener el
/// ajuste en los tres entornos y dejaria el resultado a merced de la
/// configuracion de infraestructura. La conversion se hace aqui, explicita y
/// verificable, y el horario de verano lo resuelve la propia base de datos de
/// zonas horarias.
/// </remarks>
public static class HoraEspana
{
    // El identificador cambia segun el sistema operativo: IANA en Linux (donde
    // se ejecuta el App Service) y el nombre de Windows en las maquinas de
    // desarrollo. .NET traduce entre ambos en la mayoria de plataformas, pero
    // no en todas, asi que se intentan los dos antes de rendirse.
    private static readonly TimeZoneInfo Zona = ResolverZona();

    private static TimeZoneInfo ResolverZona()
    {
        foreach (var id in new[] { "Europe/Madrid", "Romance Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        // Sin base de datos de zonas horarias no hay conversion posible; mostrar
        // UTC es preferible a impedir que la pagina se pinte.
        return TimeZoneInfo.Utc;
    }

    /// <summary>Nombre de la zona aplicada, para mostrarlo junto a las horas.</summary>
    public static string NombreZona => Zona.Id;

    /// <summary>
    /// Convierte a hora peninsular. Un valor sin marca de zona
    /// (<see cref="DateTimeKind.Unspecified"/>) se interpreta como UTC: es
    /// como llegan las fechas del backend, que las genera con
    /// <c>DateTime.UtcNow</c> y las serializa sin sufijo Z.
    /// </summary>
    public static DateTime Desde(DateTime instante)
    {
        var utc = instante.Kind switch
        {
            DateTimeKind.Utc => instante,
            DateTimeKind.Local => instante.ToUniversalTime(),
            _ => DateTime.SpecifyKind(instante, DateTimeKind.Utc)
        };

        return TimeZoneInfo.ConvertTimeFromUtc(utc, Zona);
    }

    public static DateTime Desde(DateTimeOffset instante) =>
        TimeZoneInfo.ConvertTime(instante, Zona).DateTime;

    /// <summary>Convierte y formatea; devuelve un guion si no hay valor.</summary>
    public static string Formatear(DateTime? instante, string formato) =>
        instante is null ? "—" : Desde(instante.Value).ToString(formato);

    public static string Formatear(DateTimeOffset? instante, string formato) =>
        instante is null ? "—" : Desde(instante.Value).ToString(formato);
}
