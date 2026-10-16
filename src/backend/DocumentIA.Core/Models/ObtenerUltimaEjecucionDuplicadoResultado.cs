namespace DocumentIA.Core.Models;

/// <summary>
/// Resultado de buscar una historica reutilizable (AB#100930). Con Salida nula y ModoEstricto,
/// MotivoNoReutilizacion dice por que y el orquestador sigue el flujo normal; sin ModoEstricto
/// el orquestador aplica la regla anterior (DUPLICADO vacio en la rama SHA256).
/// </summary>
public sealed class ObtenerUltimaEjecucionDuplicadoResultado
{
    public ContratoSalida? Salida { get; set; }
    public string? MotivoNoReutilizacion { get; set; }
    public bool ModoEstricto { get; set; }

    public static ObtenerUltimaEjecucionDuplicadoResultado Reutilizada(ContratoSalida salida, bool modoEstricto)
        => new() { Salida = salida, ModoEstricto = modoEstricto };

    public static ObtenerUltimaEjecucionDuplicadoResultado NoReutilizada(string? motivo, bool modoEstricto)
        => new() { MotivoNoReutilizacion = motivo, ModoEstricto = modoEstricto };
}
