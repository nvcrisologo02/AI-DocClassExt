using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentIA.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeedInitialPrompts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Seed inicial de los 4 prompts canónicos (Phase 1 y Phase 2, System y User)
            migrationBuilder.InsertData(
                table: "PromptTemplates",
                columns: new[] { "PromptKey", "Version", "Content", "IsActive", "Description", "CreatedAtUtc", "UpdatedAtUtc", "CreatedBy", "PublishedAtUtc", "PublishedBy" },
                values: new object[,]
                {
                    // Phase 1 - System Prompt
                    {
                        "classification.phase1.system",
                        1,
                        "Eres un sistema experto en clasificación de documentos del sector inmobiliario español, especialmente documentos de SAREB (Sociedad de Gestión de Activos procedentes de la Reestructuración Bancaria). Analiza el documento adjunto y clasifícalo en una familia TDN1. Clasifica por el acto jurídico principal del documento, ignorando el medio de remisión (correo, notificación, traslado, etc.). Responde exclusivamente en JSON válido con esta estructura: {\"tdn1\": \"CODIGO_TDN1\" | null, \"propuesta\": \"texto libre\", \"resumen\": \"resumen ejecutivo\", \"confianza\": 0.0-1.0}. El campo 'confianza' debe ser un número entre 0.0 (ninguna certeza) y 1.0 (certeza absoluta) que refleje tu nivel de confianza en la clasificación. No incluyas texto fuera del JSON.",
                        true,
                        "Phase 1 System Prompt - Clasificación de familias TDN1",
                        DateTime.UtcNow,
                        DateTime.UtcNow,
                        "seed-migration",
                        DateTime.UtcNow,
                        "seed-migration"
                    },
                    // Phase 1 - User Prompt Template
                    {
                        "classification.phase1.user",
                        1,
                        "Prompt adicional de instrucciones (si aplica):\n{CONTEXT_PROMPT}\n\nFamilias TDN1 disponibles:\n{TDN1_CATALOG}\n\nSi no puedes resolver una familia, devuelve tdn1=null y completa propuesta con una sugerencia no vinculante. Comenzando siempre por el codigo de la tipologia propuesta, seguido de la justificacion en no mas de 200 caracteres (ejemplo: 'ESCR-06: Se trata de una escritura de dación en pago en la que los deudores transmiten un bien al acreedor para cancelar la deuda hipotecaria existente y extinguir las obligaciones derivadas.'). - No inventes códigos ni nombres fuera del catálogo.\n\nCONTENIDO DEL DOCUMENTO (texto/markdown):\n{DOCUMENT_TEXT}",
                        true,
                        "Phase 1 User Prompt Template - Instrucciones para clasificación TDN1",
                        DateTime.UtcNow,
                        DateTime.UtcNow,
                        "seed-migration",
                        DateTime.UtcNow,
                        "seed-migration"
                    },
                    // Phase 2 - System Prompt
                    {
                        "classification.phase2.system",
                        1,
                        "Eres un sistema experto en clasificación de documentos del sector inmobiliario español, especialmente documentos de SAREB (Sociedad de Gestión de Activos procedentes de la Reestructuración Bancaria). Debes seleccionar exclusivamente una tipología de la familia TDN1 ya resuelta basándote en el contenido del documento. Responde exclusivamente en JSON válido con esta estructura: {\"tdn2\": \"CODIGO_TDN2\", \"resumen\": \"resumen ejecutivo\", \"confianza\": 0.0-1.0}. El campo 'confianza' debe ser un número entre 0.0 (ninguna certeza) y 1.0 (certeza absoluta) que refleje tu nivel de confianza en la clasificación. No incluyas texto fuera del JSON.",
                        true,
                        "Phase 2 System Prompt - Clasificación de tipologías TDN2",
                        DateTime.UtcNow,
                        DateTime.UtcNow,
                        "seed-migration",
                        DateTime.UtcNow,
                        "seed-migration"
                    },
                    // Phase 2 - User Prompt Template
                    {
                        "classification.phase2.user",
                        1,
                        "Familia TDN1 resuelta: {TDN1_CODE}\n\nTipologías disponibles en esta familia:\n{TDN2_CATALOG}\n\nSi no puedes resolver la tipología exacta, devuelve una propuesta no vinculante comenzando siempre por el código de la tipología propuesta, seguido de la justificación en no más de 200 caracteres (ejemplo: 'ESCR-06: Se trata de una escritura de dación en pago en la que los deudores transmiten un bien al acreedor para cancelar la deuda hipotecaria existente y extinguir las obligaciones derivadas.').\n\nCONTENIDO DEL DOCUMENTO (texto/markdown):\n{DOCUMENT_TEXT}",
                        true,
                        "Phase 2 User Prompt Template - Instrucciones para clasificación TDN2",
                        DateTime.UtcNow,
                        DateTime.UtcNow,
                        "seed-migration",
                        DateTime.UtcNow,
                        "seed-migration"
                    }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Eliminar los prompts iniciales si se revierte la migración
            migrationBuilder.DeleteData(
                table: "PromptTemplates",
                keyColumn: "PromptKey",
                keyValue: "classification.phase1.system");

            migrationBuilder.DeleteData(
                table: "PromptTemplates",
                keyColumn: "PromptKey",
                keyValue: "classification.phase1.user");

            migrationBuilder.DeleteData(
                table: "PromptTemplates",
                keyColumn: "PromptKey",
                keyValue: "classification.phase2.system");

            migrationBuilder.DeleteData(
                table: "PromptTemplates",
                keyColumn: "PromptKey",
                keyValue: "classification.phase2.user");
        }
    }
}
