namespace DocumentIA.Core.Services.Classification;

/// <summary>Distribucion completa de A para un documento. Sin IO. AB#100779.</summary>
public sealed class DistribucionEmbeddings
{
    public IReadOnlyDictionary<string, double> ProbTdn1 { get; init; } = new Dictionary<string, double>();
    /// <summary>Familia -> (subtipo -> probabilidad). Familia constante: un unico subtipo con 1.0.</summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, double>> ProbTdn2 { get; init; } =
        new Dictionary<string, IReadOnlyDictionary<string, double>>();
    public string Tdn1 { get; init; } = string.Empty;
    public double ConfianzaTdn1 { get; init; }
    /// <summary>Subtipo ganador de la familia ganadora; cadena vacia si la familia no cubre subtipos.</summary>
    public string Tdn2 { get; init; } = string.Empty;

    public double ProbabilidadPar(string tdn1, string tdn2)
    {
        if (!ProbTdn1.TryGetValue(tdn1, out var p1) || !ProbTdn2.TryGetValue(tdn1, out var subtipos))
        {
            return 0;
        }

        return subtipos.TryGetValue(tdn2, out var p2) ? p1 * p2 : 0;
    }

    public IReadOnlyList<(string Tdn1, double Probabilidad)> Top(int n) =>
        ProbTdn1.OrderByDescending(kv => kv.Value).Take(n).Select(kv => (kv.Key, kv.Value)).ToList();
}

public static class ClasificadorEmbeddings
{
    public static DistribucionEmbeddings Inferir(ModeloEmbeddings modelo, ReadOnlySpan<float> vector)
    {
        var dims = modelo.Manifiesto.Dimensiones;
        if (vector.Length != dims)
        {
            throw new ArgumentException($"El vector tiene {vector.Length} dimensiones y el modelo espera {dims}.", nameof(vector));
        }

        var x = Normalizar(vector);

        var p1 = Probabilidades(modelo.Tdn1.Clases, modelo.Tdn1.Coef, modelo.Tdn1.Intercept, x);
        var probTdn1 = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < modelo.Tdn1.Clases.Count; i++)
        {
            probTdn1[modelo.Tdn1.Clases[i]] = p1[i];
        }

        var probTdn2 = new Dictionary<string, IReadOnlyDictionary<string, double>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (familia, f) in modelo.Tdn2)
        {
            var subtipos = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            if (f.Constante is not null)
            {
                subtipos[f.Constante] = 1.0;
            }
            else
            {
                var p2 = Probabilidades(f.Clases, f.Coef, f.Intercept, x);
                for (var i = 0; i < f.Clases.Count; i++)
                {
                    subtipos[f.Clases[i]] = p2[i];
                }
            }

            probTdn2[familia] = subtipos;
        }

        // Paridad con Python: una familia TDN1 sin modelo TDN2 se trata como "no cubierta" (subtipo vacio con prob. 1.0).
        foreach (var clase in modelo.Tdn1.Clases)
        {
            if (!probTdn2.ContainsKey(clase))
            {
                probTdn2[clase] = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { [string.Empty] = 1.0 };
            }
        }

        var ganadora = Argmax(modelo.Tdn1.Clases, p1);
        var tdn2 = probTdn2.TryGetValue(ganadora, out var dist)
            ? dist.OrderByDescending(kv => kv.Value).First().Key
            : string.Empty;

        return new DistribucionEmbeddings
        {
            ProbTdn1 = probTdn1,
            ProbTdn2 = probTdn2,
            Tdn1 = ganadora,
            ConfianzaTdn1 = probTdn1[ganadora],
            Tdn2 = tdn2
        };
    }

    /// <summary>
    /// Misma convencion que sklearn: con dos clases, una fila de coef y sigmoide para clases[1];
    /// con mas, softmax sobre los logits (solver lbfgs multinomial).
    /// </summary>
    internal static double[] Probabilidades(List<string> clases, double[][] coef, double[] intercept, double[] x)
    {
        if (clases.Count == 2 && coef.Length == 1)
        {
            var z = Dot(coef[0], x) + intercept[0];
            var p1 = 1.0 / (1.0 + Math.Exp(-z));
            return new[] { 1.0 - p1, p1 };
        }

        var logits = new double[coef.Length];
        var max = double.NegativeInfinity;
        for (var k = 0; k < coef.Length; k++)
        {
            logits[k] = Dot(coef[k], x) + intercept[k];
            max = Math.Max(max, logits[k]);
        }

        var suma = 0.0;
        for (var k = 0; k < logits.Length; k++)
        {
            logits[k] = Math.Exp(logits[k] - max);
            suma += logits[k];
        }

        for (var k = 0; k < logits.Length; k++)
        {
            logits[k] /= suma;
        }

        return logits;
    }

    /// <summary>El spike normalizo L2 los vectores antes de entrenar; se repite aqui por paridad.</summary>
    private static double[] Normalizar(ReadOnlySpan<float> vector)
    {
        var x = new double[vector.Length];
        var norma = 0.0;
        for (var i = 0; i < vector.Length; i++)
        {
            x[i] = vector[i];
            norma += x[i] * x[i];
        }

        norma = Math.Sqrt(norma);
        if (norma > 0)
        {
            for (var i = 0; i < x.Length; i++)
            {
                x[i] /= norma;
            }
        }

        return x;
    }

    private static double Dot(double[] w, double[] x)
    {
        var s = 0.0;
        for (var i = 0; i < w.Length; i++)
        {
            s += w[i] * x[i];
        }

        return s;
    }

    private static string Argmax(List<string> clases, double[] p)
    {
        var mejor = 0;
        for (var i = 1; i < p.Length; i++)
        {
            if (p[i] > p[mejor])
            {
                mejor = i;
            }
        }

        return clases[mejor];
    }
}
