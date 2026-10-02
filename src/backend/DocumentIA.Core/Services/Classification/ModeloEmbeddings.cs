using System.Text.Json;

namespace DocumentIA.Core.Services.Classification;

/// <summary>
/// Artefacto exportado por exportar_modelo.py (DocumentIA.Batch): manifiesto mas pesos de la
/// regresion logistica TDN1 y, por familia, TDN2 (constante o lineal). AB#100779.
/// </summary>
public sealed class ModeloEmbeddings
{
    private static readonly JsonSerializerOptions Opciones = new() { PropertyNameCaseInsensitive = true };

    public ManifiestoModeloEmbeddings Manifiesto { get; set; } = new();
    public ModeloLineal Tdn1 { get; set; } = new();
    public Dictionary<string, ModeloFamiliaTdn2> Tdn2 { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Parsea y valida. Lanza InvalidDataException con el motivo si el artefacto no es usable.</summary>
    public static ModeloEmbeddings Parse(string json)
    {
        ModeloEmbeddings? modelo;
        try
        {
            modelo = JsonSerializer.Deserialize<ModeloEmbeddings>(json, Opciones);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Artefacto de embeddings no es JSON valido: {ex.Message}", ex);
        }

        if (modelo is null)
        {
            throw new InvalidDataException("Artefacto de embeddings vacio.");
        }

        // System.Text.Json crea el diccionario con el comparador por defecto: se rehace sin distinguir mayusculas.
        modelo.Tdn2 = new Dictionary<string, ModeloFamiliaTdn2>(modelo.Tdn2, StringComparer.OrdinalIgnoreCase);
        modelo.Validar();
        return modelo;
    }

    private void Validar()
    {
        var dims = Manifiesto.Dimensiones;
        if (dims <= 0)
        {
            throw new InvalidDataException("El manifiesto no declara dimensiones.");
        }

        ValidarLineal("TDN1", Tdn1.Clases, Tdn1.Coef, Tdn1.Intercept, dims);

        foreach (var (familia, f) in Tdn2)
        {
            if (f.Constante is not null)
            {
                continue;
            }

            if (f.Clases.Count == 0 || f.Coef.Length == 0)
            {
                throw new InvalidDataException($"La familia '{familia}' no tiene constante ni coeficientes.");
            }

            ValidarLineal($"familia '{familia}'", f.Clases, f.Coef, f.Intercept, dims);
        }
    }

    private static void ValidarLineal(string nombre, List<string> clases, double[][] coef, double[] intercept, int dims)
    {
        if (clases.Count < 2)
        {
            throw new InvalidDataException($"{nombre}: hacen falta al menos dos clases.");
        }

        var filasEsperadas = clases.Count == 2 ? 1 : clases.Count;
        if (coef.Length != filasEsperadas || intercept.Length != filasEsperadas)
        {
            throw new InvalidDataException($"{nombre}: {clases.Count} clases requieren {filasEsperadas} filas de coef e intercept.");
        }

        foreach (var fila in coef)
        {
            if (fila.Length != dims)
            {
                throw new InvalidDataException($"{nombre}: fila de coef con {fila.Length} valores y el manifiesto declara {dims} dimensiones.");
            }
        }
    }
}

public sealed class ManifiestoModeloEmbeddings
{
    public string Version { get; set; } = string.Empty;
    public string EntrenadoEn { get; set; } = string.Empty;
    public string Particion { get; set; } = string.Empty;
    public int NDocumentos { get; set; }
    public string ModeloEmbeddings { get; set; } = string.Empty;
    public int Dimensiones { get; set; }
    public int MaxChars { get; set; }
    public bool Calibrado { get; set; }
    public double UmbralRecomendado { get; set; }
    public List<TipologiaManifiesto> Tipologias { get; set; } = new();
}

public sealed class TipologiaManifiesto
{
    public string Codigo { get; set; } = string.Empty;
    public string Tdn1 { get; set; } = string.Empty;
    public string Tdn2 { get; set; } = string.Empty;
}

/// <summary>Regresion logistica de sklearn: coef (K x dims, o 1 x dims si binaria) e intercept.</summary>
public sealed class ModeloLineal
{
    public List<string> Clases { get; set; } = new();
    public double[][] Coef { get; set; } = Array.Empty<double[]>();
    public double[] Intercept { get; set; } = Array.Empty<double>();
}

/// <summary>Familia TDN2: o un subtipo constante (cadena vacia = no cubierto) o un modelo lineal.</summary>
public sealed class ModeloFamiliaTdn2
{
    public string? Constante { get; set; }
    public List<string> Clases { get; set; } = new();
    public double[][] Coef { get; set; } = Array.Empty<double[]>();
    public double[] Intercept { get; set; } = Array.Empty<double>();
}
