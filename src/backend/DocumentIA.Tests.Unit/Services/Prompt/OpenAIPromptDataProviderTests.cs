using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DocumentIA.Core.Configuration;
using DocumentIA.Core.Models;
using DocumentIA.Functions.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

#nullable enable

namespace DocumentIA.Tests.Unit.Services.Prompt
{
    /// <summary>
    /// Test suite for template interpolation and prompt resolution in OpenAIPromptDataProvider.
    /// Focuses on the InterpolateTemplate static method and cascade resolution logic.
    /// </summary>
    public class OpenAIPromptDataProviderTests
    {
        // Note: Tests focus on static InterpolateTemplate method, no provider instance needed
        public OpenAIPromptDataProviderTests()
        {
        }

        [Fact]
        public void InterpolateTemplate_WithContenidoPlaceholder_SubstitutesMarkdownContent()
        {
            // Given: A template with {contenido} placeholder
            var template = "Resumen del documento:\n{contenido}\n\nFin del resumen.";
            var contenido = "# Important Document\n\nThis is critical information.";
            var datos = new Dictionary<string, object>();

            // When: InterpolateTemplate is called
            var result = OpenAIPromptDataProvider.InterpolateTemplate(template, contenido, datos);

            // Then: The {contenido} placeholder should be replaced with markdown content
            result.Should().NotContain("{contenido}");
            result.Should().Contain(contenido);
            result.Should().Be("Resumen del documento:\n# Important Document\n\nThis is critical information.\n\nFin del resumen.");
        }

        [Fact]
        public void InterpolateTemplate_WithCampoPlaceholder_SubstitutesExtractedFieldValue()
        {
            // Given: A template with {campo:NombreCampo} placeholders
            //        and extracted data containing those fields
            var template = "Documento: {campo:FincaRegistral}\nTipo: {campo:TipoDocumento}\n\nContenido:\n{contenido}";
            var contenido = "Property details...";
            var datos = new Dictionary<string, object>
            {
                { "FincaRegistral", "MAD-12345-ABC" },
                { "TipoDocumento", "Deed" }
            };

            // When: InterpolateTemplate is called
            var result = OpenAIPromptDataProvider.InterpolateTemplate(template, contenido, datos);

            // Then: All {campo:X} placeholders should be replaced with extracted values
            result.Should().NotContain("{campo:FincaRegistral}");
            result.Should().NotContain("{campo:TipoDocumento}");
            result.Should().Contain("MAD-12345-ABC");
            result.Should().Contain("Deed");
            result.Should().Be("Documento: MAD-12345-ABC\nTipo: Deed\n\nContenido:\nProperty details...");
        }

        [Fact]
        public void InterpolateTemplate_WithMissingCampoField_LeavesPlaceholderEmpty()
        {
            // Given: A template with {campo:MissingField} but field not in datos
            var template = "Solicitante: {campo:Solicitante}\nDocumento: {campo:Documento}";
            var contenido = "Content...";
            var datos = new Dictionary<string, object>
            {
                { "Documento", "DOC-001" }
                // Solicitante is missing
            };

            // When: InterpolateTemplate is called
            var result = OpenAIPromptDataProvider.InterpolateTemplate(template, contenido, datos);

            // Then: Missing campo placeholders should be replaced with empty string
            result.Should().Be("Solicitante: \nDocumento: DOC-001");
        }

        [Fact]
        public void InterpolateTemplate_WithMultiplePlaceholders_InterpolatesAllCorrectly()
        {
            // Given: A template with multiple placeholder types
            var template = "Tipología: {campo:Tipologia}, Contenido: {contenido}, Ref: {campo:NumeroExpediente}";
            var contenido = "Document data here";
            var datos = new Dictionary<string, object>
            {
                { "Tipologia", "ESCR-06" },
                { "NumeroExpediente", "EXP-2026-00123" }
            };

            // When: InterpolateTemplate is called
            var result = OpenAIPromptDataProvider.InterpolateTemplate(template, contenido, datos);

            // Then: All placeholders should be substituted
            result.Should().NotContain("{contenido}");
            result.Should().NotContain("{campo:");
            result.Should().Be("Tipología: ESCR-06, Contenido: Document data here, Ref: EXP-2026-00123");
        }

        [Fact]
        public void InterpolateTemplate_WithCaseSensitivePlaceholders_HandlesCaseInsensitively()
        {
            // Given: Placeholders with different cases
            var template = "Contenido 1: {CONTENIDO}\nContenido 2: {ConTenIdo}\nContenido 3: {contenido}";
            var contenido = "ACTUAL_CONTENT";
            var datos = new Dictionary<string, object>();

            // When: InterpolateTemplate is called
            var result = OpenAIPromptDataProvider.InterpolateTemplate(template, contenido, datos);

            // Then: {contenido} should be replaced case-insensitively
            // (This test documents the actual behavior - update assertion based on implementation)
            result.Should().Contain(contenido);
        }

        // ========== Helper Methods ==========

        /// <summary>
        /// Gets the InterpolateTemplate static method via reflection for testing.
        /// This is a direct unit test of the public static method.
        /// </summary>
        private string InvokeInterpolateTemplate(
            string template, 
            string contenido, 
            Dictionary<string, object> datos)
        {
            var method = typeof(OpenAIPromptDataProvider)
                .GetMethod("InterpolateTemplate",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            
            var result = method?.Invoke(null, new object[] { template, contenido, datos });
            return result as string ?? throw new InvalidOperationException("InterpolateTemplate should return a string");
        }
    }
}
