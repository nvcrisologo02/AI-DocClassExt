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

        public RuleBasedTdnClassifier(ILogger<RuleBasedTdnClassifier> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Clasifica el documento basándose en señales heurísticas presentes en la ventana de texto.
        /// Devuelve un resultado con tipología detectada y confianza basada en scoring de reglas.
        /// </summary>
        public RuleBasedClassificationResult Classify(DocumentClassificationWindow window)
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
            var score = CalculateScore(textoNormalizado, out var señalesDetectadas);

            // Resolver tipología basada en score
            var tipologia = ResolveTypologyFromScore(score, textoNormalizado);

            _logger.LogInformation(
                "Clasificación por reglas para {Documento}: tipologia={Tipologia}, confianza={Confianza}, score={Score}",
                window.DocumentName,
                tipologia,
                score.TotalScore,
                score);

            return new RuleBasedClassificationResult
            {
                TipologiaDetectada = tipologia,
                Confianza = Math.Min(score.TotalScore, 1.0),
                Razon = score.TotalScore >= 0.6 ? "rule_based_match" : "rule_based_low_confidence",
                SeñalesEncontradas = señalesDetectadas,
                Score = score
            };
        }

        private string NormalizeText(string text)
        {
            return text.ToLowerInvariant();
        }

        private HeuristicScore CalculateScore(string texto, out Dictionary<string, int> señalesDetectadas)
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

        private string ResolveTypologyFromScore(HeuristicScore score, string texto)
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
    }

    /// <summary>
    /// Resultado de la clasificación basada en reglas.
    /// </summary>
    public class RuleBasedClassificationResult
    {
        public string? TipologiaDetectada { get; set; }
        public double Confianza { get; set; }
        public string? Razon { get; set; }
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
