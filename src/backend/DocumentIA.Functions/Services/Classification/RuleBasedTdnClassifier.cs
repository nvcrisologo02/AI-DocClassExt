using System;
using System.Collections.Generic;
using System.Linq;
using DocumentIA.Core.Models;
using Microsoft.Extensions.Logging;

namespace DocumentIA.Functions.Services.Classification
{
    /// <summary>
    /// Clasificador basado en reglas heurísticas (strong signals, positive signals, negative signals).
    /// Implementa lógica de puntuación para la clasificación jerárquica TDN1 -> TDN2 -> Matricula.
    /// </summary>
    public class RuleBasedTdnClassifier
    {
        private readonly ILogger<RuleBasedTdnClassifier> _logger;
        private readonly IReadOnlyList<TipologiaClassificationProfile> _profiles;
        private const int ImmediateClassificationMaxCharsDefault = 1000;

        public RuleBasedTdnClassifier(
            ILogger<RuleBasedTdnClassifier> logger,
            ITipologiaClassificationProfileProvider? profileProvider = null)
        {
            _logger = logger;
            _profiles = profileProvider?.GetProfiles() ?? Array.Empty<TipologiaClassificationProfile>();
        }

        /// <summary>
        /// Clasifica el documento basándose en señales heurísticas presentes en la ventana de texto.
        /// Devuelve un resultado con tipología detectada y confianza basada en scoring de reglas.
        /// </summary>
        public RuleBasedClassificationResult Classify(DocumentClassificationWindow window)
        {
            if (_profiles.Count == 0)
            {
                return ClassifyLegacy(window);
            }

            var textoNormalizado = NormalizeText(window.ExtractedText ?? string.Empty);

            var evaluated = new List<ProfileScore>();
            foreach (var profile in _profiles.OrderByDescending(profile => profile.Priority))
            {
                var maxChars = profile.ImmediateClassificationTriggers?.MaxCharsToScan > 0
                    ? profile.ImmediateClassificationTriggers.MaxCharsToScan
                    : ImmediateClassificationMaxCharsDefault;
                var firstPageText = GetFirstPageText(textoNormalizado, maxChars);

                if (TryEvaluateImmediateProfile(profile, firstPageText, out var immediateEvaluation))
                {
                    evaluated.Add(immediateEvaluation);
                    continue;
                }

                var evaluation = EvaluateProfile(profile, textoNormalizado, firstPageText, window.TotalPaginas, window.CharsTextoNativo);
                if (evaluation is not null)
                {
                    evaluated.Add(evaluation);
                }
            }

            if (evaluated.Count == 0)
            {
                return new RuleBasedClassificationResult
                {
                    TipologiaDetectada = "Desconocido",
                    Confianza = 0.0,
                    Razon = "below_minimum_signal_score",
                    ClasificacionMetodo = "indeterminate",
                    RouteToReview = true,
                    SeñalesEncontradas = new Dictionary<string, int>(),
                    Score = new HeuristicScore()
                };
            }

            var ordered = evaluated
                .OrderByDescending(x => x.Score)
                .ThenByDescending(x => x.Profile.Priority)
                .ToList();

            var best = ordered[0];
            var secondScore = ordered.Count > 1 ? ordered[1].Score : 0.0;
            var margin = best.Score - secondScore;
            var routeToReview = margin < best.Profile.MinimumMarginOverSecond;

            _logger.LogInformation(
                "Clasificación por reglas v1.4 para {Documento}: tipologia={Tipologia}, confianza={Confianza}, score={Score}, margin={Margin}, metodo={Metodo}",
                window.DocumentName,
                best.Profile.Codigo,
                best.Score,
                best.Score,
                margin,
                best.ClasificacionMetodo);

            return new RuleBasedClassificationResult
            {
                TipologiaDetectada = best.Profile.Codigo,
                Confianza = best.Score,
                Razon = best.Razon,
                ClasificacionMetodo = best.ClasificacionMetodo,
                TriggerActivado = best.TriggerActivado,
                RouteToReview = routeToReview || best.RouteToReview,
                Margin = margin,
                SeñalesEncontradas = best.SeñalesDetectadas,
                Score = best.HeuristicScore
            };
        }

        private ProfileScore? EvaluateProfile(
            TipologiaClassificationProfile profile,
            string normalizedText,
            string firstPageText,
            int totalPaginas,
            int charsTextoNativo)
        {
            var sinais = new Dictionary<string, int>();

            if (IsVetoedByOutOfScope(profile, firstPageText, out var vetoSignals))
            {
                foreach (var item in vetoSignals)
                {
                    sinais[item.Key] = item.Value;
                }

                return null;
            }

            var strongHits = CountHits(normalizedText, profile.StrongSignals, sinais);
            var positiveHits = CountHits(normalizedText, profile.PositiveSignals, sinais);
            var negativeHits = CountHits(normalizedText, profile.NegativeSignals, sinais);
            var outOfScopeHits = CountHits(normalizedText, profile.OutOfScopeSignals, sinais);

            var rawScore = (strongHits * 1.0) + (positiveHits * 0.3) - (negativeHits * 0.8) - (outOfScopeHits * 2.0);
            var denominator = Math.Max(profile.StrongSignals.Count + profile.PositiveSignals.Count, 1);
            var normalizedScore = Math.Max(0.0, Math.Min(1.0, rawScore / denominator));
            var metadataBonus = profile.MetadataBonus
                .Where(rule => rule.Matches(totalPaginas, charsTextoNativo))
                .Sum(rule => rule.Score);

            var total = Math.Max(0.0, Math.Min(1.0, normalizedScore + metadataBonus));

            if (total < profile.MinimumSignalScore)
            {
                return null;
            }

            return new ProfileScore
            {
                Profile = profile,
                Score = total,
                SeñalesDetectadas = sinais,
                ClasificacionMetodo = "signalScoring",
                Razon = "rule_based_match",
                RouteToReview = false,
                HeuristicScore = new HeuristicScore
                {
                    StrongSignalScore = strongHits,
                    PositiveSignalScore = positiveHits,
                    NegativeSignalScore = negativeHits + outOfScopeHits,
                    TotalScore = total
                }
            };
        }

        private bool TryEvaluateImmediateProfile(
            TipologiaClassificationProfile profile,
            string firstPageText,
            out ProfileScore evaluation)
        {
            evaluation = null!;

            var triggerConfig = profile.ImmediateClassificationTriggers;
            if (triggerConfig is null || !triggerConfig.Enabled)
            {
                return false;
            }

            if (triggerConfig.VetoedByOutOfScope && IsVetoedByOutOfScope(profile, firstPageText, out var vetoDetails))
            {
                return false;
            }

            foreach (var trigger in triggerConfig.Triggers)
            {
                if (firstPageText.Contains(trigger, StringComparison.OrdinalIgnoreCase))
                {
                    var signals = new Dictionary<string, int>
                    {
                        [trigger] = 1
                    };

                    evaluation = new ProfileScore
                    {
                        Profile = profile,
                        Score = Math.Max(0.0, Math.Min(1.0, triggerConfig.ResultScore)),
                        SeñalesDetectadas = signals,
                        ClasificacionMetodo = "immediateClassificationTrigger",
                        TriggerActivado = trigger,
                        Razon = "immediate_trigger",
                        RouteToReview = false,
                        HeuristicScore = new HeuristicScore
                        {
                            StrongSignalScore = 1,
                            PositiveSignalScore = 0,
                            NegativeSignalScore = 0,
                            TotalScore = Math.Max(0.0, Math.Min(1.0, triggerConfig.ResultScore))
                        }
                    };

                    return true;
                }
            }

            return false;
        }

        private static bool IsVetoedByOutOfScope(
            TipologiaClassificationProfile profile,
            string normalizedText,
            out Dictionary<string, int> matchedSignals)
        {
            matchedSignals = new Dictionary<string, int>();

            foreach (var signal in profile.OutOfScopeSignals)
            {
                if (normalizedText.Contains(signal, StringComparison.Ordinal))
                {
                    matchedSignals[signal] = 1;
                }
            }

            return matchedSignals.Count > 0;
        }

        private static string GetFirstPageText(string normalizedText, int maxChars)
        {
            var effectiveMaxChars = maxChars > 0 ? maxChars : ImmediateClassificationMaxCharsDefault;
            return normalizedText.Length > effectiveMaxChars
                ? normalizedText[..effectiveMaxChars]
                : normalizedText;
        }

        private string NormalizeText(string text)
        {
            return text.ToLowerInvariant();
        }

        private static int CountHits(string text, IEnumerable<string> signals, IDictionary<string, int> foundSignals)
        {
            var hits = 0;
            foreach (var signal in signals)
            {
                if (text.Contains(signal, StringComparison.Ordinal))
                {
                    hits++;
                    foundSignals[signal] = foundSignals.TryGetValue(signal, out var count) ? count + 1 : 1;
                }
            }

            return hits;
        }

        private RuleBasedClassificationResult ClassifyLegacy(DocumentClassificationWindow window)
        {
            if (string.IsNullOrWhiteSpace(window.ExtractedText))
            {
                _logger.LogWarning("Ventana de clasificación vacía para {Documento}", window.DocumentName);
                return new RuleBasedClassificationResult
                {
                    TipologiaDetectada = "Desconocido",
                    Confianza = 0.0,
                    Razon = "no_text_available",
                    SeñalesEncontradas = new Dictionary<string, int>()
                };
            }

            var textoNormalizado = NormalizeText(window.ExtractedText);
            var score = CalculateLegacyScore(textoNormalizado, out var señalesDetectadas);

            var tipologia = ResolveLegacyTypologyFromScore(score, textoNormalizado);

            _logger.LogInformation(
                "Clasificación por reglas legacy para {Documento}: tipologia={Tipologia}, confianza={Confianza}, score={Score}",
                window.DocumentName,
                tipologia,
                score.TotalScore,
                score);

            return new RuleBasedClassificationResult
            {
                TipologiaDetectada = tipologia,
                Confianza = Math.Min(score.TotalScore, 1.0),
                Razon = score.TotalScore >= 0.6 ? "rule_based_match" : "rule_based_low_confidence",
                ClasificacionMetodo = "legacy",
                RouteToReview = score.TotalScore < 0.6,
                SeñalesEncontradas = señalesDetectadas,
                Score = score
            };
        }

        private HeuristicScore CalculateLegacyScore(string texto, out Dictionary<string, int> señalesDetectadas)
        {
            señalesDetectadas = new Dictionary<string, int>();
            var score = new HeuristicScore();

            // Strong signals (3.0 points cada uno)
            var strongSignals = new[] 
            { 
                "escritura de compraventa", "dacion en pago", "cancelacion de hipoteca",
                "prestamo hipotecario", "decreto de adjudicacion", "mandamiento de cancelacion"
            };
            foreach (var signal in strongSignals)
            {
                if (texto.Contains(signal))
                {
                    score.StrongSignalScore += 3.0;
                    señalesDetectadas[signal] = señalesDetectadas.GetValueOrDefault(signal, 0) + 1;
                }
            }

            // Positive signals (1.0 point cada uno)
            var positiveSignals = new[] 
            { 
                "escritura", "compra", "vende", "hipoteca", "transmite", "adquisicion",
                "titulo de propiedad", "dominio"
            };
            foreach (var signal in positiveSignals)
            {
                if (texto.Contains(signal))
                {
                    score.PositiveSignalScore += 1.0;
                    señalesDetectadas[signal] = señalesDetectadas.GetValueOrDefault(signal, 0) + 1;
                }
            }

            // Negative signals (-2.0 points cada uno)
            var negativeSignals = new[] 
            { 
                "nota simple", "ibi", "tasacion", "desconocido"
            };
            foreach (var signal in negativeSignals)
            {
                if (texto.Contains(signal))
                {
                    score.NegativeSignalScore -= 2.0;
                    señalesDetectadas[signal] = señalesDetectadas.GetValueOrDefault(signal, 0) + 1;
                }
            }

            // Normalizar score final a 0-1
            score.TotalScore = Math.Max(0.0, Math.Min(1.0, (score.StrongSignalScore + score.PositiveSignalScore + score.NegativeSignalScore) / 10.0));

            return score;
        }

        private string ResolveLegacyTypologyFromScore(HeuristicScore score, string texto)
        {
            if (score.TotalScore >= 0.85)
            {
                if (texto.Contains("compraventa"))
                    return "escr.compraventa";
                if (texto.Contains("dacion"))
                    return "escr.dacion";
                if (texto.Contains("prestamo") && texto.Contains("hipotecario"))
                    return "escr.prestamo-originario";
                if (texto.Contains("cancelacion") && texto.Contains("hipoteca"))
                    return "escr.cancelacion-hipotecaria";
                if (texto.Contains("decreto") && texto.Contains("adjudicacion"))
                    return "sere.subasta-adjudicacion.auto";
                if (texto.Contains("mandamiento") && texto.Contains("cancelacion"))
                    return "sere.subasta-cancelacion.cargas";
            }

            if (score.TotalScore >= 0.6)
                return "escr.titularidad.otro";

            return "Desconocido";
        }

        private sealed class ProfileScore
        {
            public required TipologiaClassificationProfile Profile { get; init; }
            public required double Score { get; init; }
            public required Dictionary<string, int> SeñalesDetectadas { get; init; }
            public required HeuristicScore HeuristicScore { get; init; }
            public string ClasificacionMetodo { get; init; } = string.Empty;
            public string? TriggerActivado { get; init; }
            public string Razon { get; init; } = string.Empty;
            public bool RouteToReview { get; init; }
        }
    }

    /// <summary>
    /// Resultado de la clasificación basada en reglas.
    /// </summary>
    public class RuleBasedClassificationResult
    {
        public string? TipologiaDetectada { get; set; }
        public double Confianza { get; set; }
        public string? Razon { get; set; }
        public string? ClasificacionMetodo { get; set; }
        public string? TriggerActivado { get; set; }
        public bool RouteToReview { get; set; }
        public double Margin { get; set; }
        public Dictionary<string, int> SeñalesEncontradas { get; set; } = new();
        public HeuristicScore? Score { get; set; }
    }

    /// <summary>
    /// Score heurístico calculado durante la clasificación por reglas.
    /// </summary>
    public class HeuristicScore
    {
        public double StrongSignalScore { get; set; }
        public double PositiveSignalScore { get; set; }
        public double NegativeSignalScore { get; set; }
        public double TotalScore { get; set; }
    }
}
