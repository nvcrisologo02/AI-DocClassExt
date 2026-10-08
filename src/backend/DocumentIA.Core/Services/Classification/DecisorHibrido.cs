using DocumentIA.Core.Models;

namespace DocumentIA.Core.Services.Classification;

/// <summary>Entrada del decisor: modo, umbrales y contexto de la peticion. Sin IO.</summary>
public sealed class ParametrosDecision
{
    public string Modo { get; init; } = ModosEmbeddings.Off;
    public double UmbralConfianza { get; init; } = 0.6;
    public string ModoRestringido { get; init; } = ModosEmbeddings.Sombra;
    public double UmbralMasa { get; init; } = 0.5;
    public double UmbralConfianzaCondicionada { get; init; } = 0.8;
    public bool ExpectedTypeInformado { get; init; }
    /// <summary>Codigos de tipologia permitidos. Nulo o vacio: peticion sin restriccion.</summary>
    public IReadOnlyList<string>? RestriccionCodigos { get; init; }

    public bool EsRestringida => RestriccionCodigos is { Count: > 0 };
}

public sealed class DecisionHibrida
{
    public string Decision { get; init; } = DecisionesEmbeddings.Omitido;
    public string Motivo { get; init; } = MotivosEmbeddings.Off;
    /// <summary>Modo aplicado: el normal o el restringido segun la peticion.</summary>
    public string ModoEfectivo { get; init; } = ModosEmbeddings.Off;
    /// <summary>Codigo de tipologia que contesta o se persiste; "Desconocido" por masa insuficiente; nulo si el par no mapea.</summary>
    public string? Tipologia { get; init; }
    public string? Tdn1 { get; init; }
    public string? Tdn2 { get; init; }
    public double Confianza { get; init; }
    public RestringidoEmbeddings? Restringido { get; init; }
}

/// <summary>
/// Aplica modo, umbrales, restriccion, manifiesto y puertas sobre la distribucion de A.
/// Toda la logica del criterio 8 del PBI vive aqui. Sin IO. AB#100779.
/// </summary>
public static class DecisorHibrido
{
    public const string TipologiaDesconocido = "Desconocido";

    public static string ModoEfectivo(string modo, string modoRestringido, bool restringida) =>
        ModosEmbeddings.Normalizar(restringida ? modoRestringido : modo);

    public static DecisionHibrida Decidir(
        DistribucionEmbeddings distribucion,
        ManifiestoModeloEmbeddings manifiesto,
        ParametrosDecision p,
        IReadOnlyDictionary<string, TipologiaPar> catalogoPares)
    {
        var modo = ModoEfectivo(p.Modo, p.ModoRestringido, p.EsRestringida);
        if (modo == ModosEmbeddings.Off)
        {
            return new DecisionHibrida { Decision = DecisionesEmbeddings.Omitido, Motivo = MotivosEmbeddings.Off, ModoEfectivo = modo };
        }

        // Prediccion sin restringir, con la grafia del catalogo cuando el par mapea.
        var parLibre = Buscar(catalogoPares, distribucion.Tdn1, distribucion.Tdn2);
        var tdn1 = parLibre?.Tdn1 ?? distribucion.Tdn1;
        var tdn2 = parLibre?.Tdn2 ?? distribucion.Tdn2;
        var tipologiaLibre = parLibre?.Codigo;
        var prediccionLibre = tipologiaLibre ?? $"{distribucion.Tdn1}/{distribucion.Tdn2}";

        if (!p.EsRestringida)
        {
            return DecidirSinRestriccion(distribucion, p, modo, tdn1, tdn2, tipologiaLibre);
        }

        // Restringida: masa y confianza condicionada se calculan en cualquier modo distinto de off.
        var permitidos = p.RestriccionCodigos!;
        var paresManifiesto = manifiesto.Tipologias
            .GroupBy(t => t.Codigo, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var fueraDelManifiesto = permitidos.Where(c => !paresManifiesto.ContainsKey(c)).ToList();
        var paresPermitidos = permitidos
            .Where(paresManifiesto.ContainsKey)
            .Select(c => paresManifiesto[c])
            .ToList();

        var masa = 0.0;
        TipologiaManifiesto? mejor = null;
        var pMejor = 0.0;
        foreach (var par in paresPermitidos)
        {
            var prob = distribucion.ProbabilidadPar(par.Tdn1, par.Tdn2);
            masa += prob;
            if (prob > pMejor)
            {
                pMejor = prob;
                mejor = par;
            }
        }

        var restringido = new RestringidoEmbeddings
        {
            Masa = masa,
            ConfianzaCondicionada = masa > 0 ? pMejor / masa : 0,
            PrediccionSinRestringir = prediccionLibre
        };

        DecisionHibrida Derivar(string motivo, string? puerta = null)
        {
            restringido.Puerta = puerta;
            return new DecisionHibrida
            {
                Decision = DecisionesEmbeddings.DerivarGpt,
                Motivo = motivo,
                ModoEfectivo = modo,
                Tipologia = tipologiaLibre,
                Tdn1 = tdn1,
                Tdn2 = tdn2,
                Confianza = distribucion.ConfianzaTdn1,
                Restringido = restringido
            };
        }

        if (modo == ModosEmbeddings.Sombra)
        {
            return Derivar(MotivosEmbeddings.Sombra);
        }

        // hibrido restringido: puertas, en este orden.
        if (p.ExpectedTypeInformado)
        {
            return Derivar(MotivosEmbeddings.ExpectedType);
        }

        if (fueraDelManifiesto.Count > 0)
        {
            return Derivar(MotivosEmbeddings.Cobertura, MotivosEmbeddings.Cobertura);
        }

        var paresDistintos = paresPermitidos.Select(t => TipologiaPar.Clave(t.Tdn1, t.Tdn2)).Distinct().Count();
        if (paresDistintos < paresPermitidos.Count)
        {
            return Derivar(MotivosEmbeddings.ParCompartido, MotivosEmbeddings.ParCompartido);
        }

        if (!manifiesto.Calibrado)
        {
            return Derivar(MotivosEmbeddings.SinCalibracion, MotivosEmbeddings.SinCalibracion);
        }

        if (masa < p.UmbralMasa)
        {
            return new DecisionHibrida
            {
                Decision = DecisionesEmbeddings.Contesta,
                Motivo = MotivosEmbeddings.MasaInsuficiente,
                ModoEfectivo = modo,
                Tipologia = TipologiaDesconocido,
                Tdn1 = tdn1,
                Tdn2 = tdn2,
                Confianza = masa,
                Restringido = restringido
            };
        }

        if (mejor is not null && restringido.ConfianzaCondicionada >= p.UmbralConfianzaCondicionada)
        {
            var parCatalogo = Buscar(catalogoPares, mejor.Tdn1, mejor.Tdn2);
            return new DecisionHibrida
            {
                Decision = DecisionesEmbeddings.Contesta,
                Motivo = MotivosEmbeddings.Umbral,
                ModoEfectivo = modo,
                Tipologia = mejor.Codigo,
                Tdn1 = parCatalogo?.Tdn1 ?? mejor.Tdn1,
                Tdn2 = parCatalogo?.Tdn2 ?? mejor.Tdn2,
                Confianza = restringido.ConfianzaCondicionada,
                Restringido = restringido
            };
        }

        return Derivar(MotivosEmbeddings.ConfianzaCondicionadaBaja);
    }

    private static DecisionHibrida DecidirSinRestriccion(
        DistribucionEmbeddings d, ParametrosDecision p, string modo, string tdn1, string tdn2, string? tipologia)
    {
        DecisionHibrida Resultado(string decision, string motivo) => new()
        {
            Decision = decision,
            Motivo = motivo,
            ModoEfectivo = modo,
            Tipologia = tipologia,
            Tdn1 = tdn1,
            Tdn2 = tdn2,
            Confianza = d.ConfianzaTdn1
        };

        if (modo == ModosEmbeddings.Sombra)
        {
            return Resultado(DecisionesEmbeddings.DerivarGpt, MotivosEmbeddings.Sombra);
        }

        if (p.ExpectedTypeInformado)
        {
            return Resultado(DecisionesEmbeddings.DerivarGpt, MotivosEmbeddings.ExpectedType);
        }

        if (d.ConfianzaTdn1 < p.UmbralConfianza)
        {
            return Resultado(DecisionesEmbeddings.DerivarGpt, MotivosEmbeddings.ConfianzaBaja);
        }

        if (tipologia is null)
        {
            return Resultado(DecisionesEmbeddings.DerivarGpt, MotivosEmbeddings.SinTipologia);
        }

        return Resultado(DecisionesEmbeddings.Contesta, MotivosEmbeddings.Umbral);
    }

    private static TipologiaPar? Buscar(IReadOnlyDictionary<string, TipologiaPar> catalogo, string? tdn1, string? tdn2)
    {
        if (string.IsNullOrWhiteSpace(tdn1) || string.IsNullOrWhiteSpace(tdn2))
        {
            return null;
        }

        return catalogo.TryGetValue(TipologiaPar.Clave(tdn1, tdn2), out var par) ? par : null;
    }
}
