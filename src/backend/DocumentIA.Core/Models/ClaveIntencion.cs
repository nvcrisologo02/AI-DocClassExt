namespace DocumentIA.Core.Models;

/// <summary>
/// Componentes normalizados de la intencion de una peticion (AB#100930). Dos peticiones con
/// los mismos componentes persiguen el mismo objetivo y pueden compartir resultado.
/// </summary>
public sealed class ClaveIntencionComponentes
{
    public string Modo { get; init; } = ModosIntencion.Completo;
    public string Nivel { get; init; } = string.Empty;
    /// <summary>"*" sin restriccion; si no, codigos publicados en mayusculas, sin duplicados, ordenados y unidos por coma.</summary>
    public string Universo { get; init; } = ClaveIntencionBuilderConstantes.UniversoCompleto;
    public string ExpectedType { get; init; } = string.Empty;
    /// <summary>Deployment GPT, ClassifierId de DI o "expectedtype-input".</summary>
    public string ModeloClasificacion { get; init; } = string.Empty;
}

/// <summary>Clave calculada: hash de 32 hexadecimales y componentes serializados para auditoria.</summary>
public sealed record ClaveIntencion(string Clave, string ComponentesJson, ClaveIntencionComponentes Componentes);

public static class ModosIntencion
{
    public const string SoloClasificacion = "solo-clasificacion";
    public const string Completo = "completo";
}

/// <summary>Valores de DocumentoEjecuciones.MotivoReutilizacion y del contrato (spec, secciones 3 y 5).</summary>
public static class MotivosReutilizacion
{
    public const string ClaveIgual = "clave-igual";
    public const string SinHistorica = "sin-historica";
    public const string ClaveDistinta = "clave-distinta";
    public const string ModeloExtraccionDistinto = "modelo-extraccion-distinto";
    public const string PaginasInsuficientes = "paginas-insuficientes";
    public const string ContratoInvalido = "contrato-invalido";

    /// <summary>Todas las historicas con esa clave estan en estado de fallo (AB#100956).</summary>
    public const string HistoricaFallida = "historica-fallida";
}

internal static class ClaveIntencionBuilderConstantes
{
    public const string UniversoCompleto = "*";
}
