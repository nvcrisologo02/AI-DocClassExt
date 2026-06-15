using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentIA.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSummaryPrompts : Migration
    {
        /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Tipologias",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "FechaCreacion", "PublicadaEn" },
                values: new object[] { new DateTime(2026, 6, 15, 5, 34, 32, 346, DateTimeKind.Utc).AddTicks(3061), new DateTime(2026, 6, 15, 5, 34, 32, 346, DateTimeKind.Utc).AddTicks(3056) });

            // Insert Summary Prompts
            var now = DateTime.UtcNow;
            migrationBuilder.InsertData(
                table: "PromptTemplates",
                columns: new[] { "PromptKey", "Version", "Content", "IsActive", "Description", "CreatedAtUtc", "CreatedBy", "PublishedAtUtc", "PublishedBy" },
                values: new object[,]
                {
                    {
                        "summary.system",
                        1,
                        "Eres un analista documental experto. Responde en espanol de Espana, sin inventar informacion y siguiendo estrictamente el formato solicitado.",
                        true,
                        "System prompt for document summary generation",
                        now,
                        "migration",
                        now,
                        "migration"
                    },
                    {
                        "summary.user",
                        1,
                        "Genera un resumen ejecutivo del documento procesado siguiendo estrictamente estas instrucciones:\n\n- Idioma: Español (España)\n- Longitud máxima: 500 caracteres\n- No inventar información ni inferir datos no presentes en el documento\n- Ser claro, conciso y preciso\n- No utilizar frases genéricas ni vagas\n- Evitar redundancias\n- Priorizar información relevante para la toma de decisiones\n\nFormato obligatorio (mantener este orden y estructura):\n\n1. Objetivo del documento:\n   Describir brevemente la finalidad del documento\n\n2. Datos clave:\n   Enumerar los puntos más relevantes o información esencial\n\n3. Alertas:\n   Identificar riesgos, inconsistencias o aspectos críticos\n\n4. Acciones recomendadas:\n   Proponer actuaciones basadas únicamente en el contenido del documento\n\n5. Contenido:\n   Resumen general del contenido principal\n\nReglas adicionales:\n- Si algún apartado no tiene información suficiente, indicar exactamente: \"N/A\"\n- No completar apartados con suposiciones\n- No añadir información externa al documento\n- El resultado debe ser compacto, profesional y fácil de leer\n\nContenido del documento:\n{contenido}",
                        true,
                        "User prompt template for document summary generation",
                        now,
                        "migration",
                        now,
                        "migration"
                    }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "PromptTemplates",
                keyColumn: "PromptKey",
                keyValue: "summary.system");

            migrationBuilder.DeleteData(
                table: "PromptTemplates",
                keyColumn: "PromptKey",
                keyValue: "summary.user");

            migrationBuilder.UpdateData(
                table: "Tipologias",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "FechaCreacion", "PublicadaEn" },
                values: new object[] { new DateTime(2026, 6, 11, 7, 51, 3, 479, DateTimeKind.Utc).AddTicks(5413), new DateTime(2026, 6, 11, 7, 51, 3, 479, DateTimeKind.Utc).AddTicks(5410) });
        }
    }
}
