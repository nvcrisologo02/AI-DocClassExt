using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentIA.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeedRestrictedClassificationPrompts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Tipologias",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "FechaCreacion", "PublicadaEn" },
                values: new object[] { new DateTime(2026, 8, 12, 6, 50, 18, 906, DateTimeKind.Utc).AddTicks(444), new DateTime(2026, 8, 12, 6, 50, 18, 906, DateTimeKind.Utc).AddTicks(435) });

            // AB#100063: seed idempotente de los 2 prompts de la clasificación restringida en fase
            // única (classification.restricted.system / classification.restricted.user). El contenido
            // es una copia exacta de GptClasificarDataProvider.RestriccionFasePlanaSystemPrompt /
            // .RestriccionFasePlanaUserPromptTemplate (fallback de código si estas filas no existen o
            // no están activas). Idempotente para poder reaplicarse a mano en entornos donde las
            // migraciones se ejecutan de forma manual (ver docs/08_CHECKLISTS_DESPLIEGUE.md).
            //
            // Los saltos de línea del contenido se construyen con CHAR(10) en vez de líneas en blanco
            // literales dentro del SQL: el generador de scripts de EF Core (dotnet ef migrations
            // script, incluido --idempotent) colapsa las líneas en blanco de los bloques Sql() al
            // reindentar el texto, lo que rompería los saltos de párrafo ("\n\n") del prompt original.
            migrationBuilder.Sql(@"
DECLARE @now DATETIME2 = SYSUTCDATETIME();
DECLARE @restrictedSystemContent NVARCHAR(MAX) =
    N'Eres un sistema experto en clasificación documental del sector inmobiliario y financiero español. Tu tarea es clasificar el documento en UNA de las tipologías del catálogo restringido que se te proporciona, comparando el CONTENIDO del documento con la descripción de cada tipología.' + CHAR(10) + CHAR(10) +
    N'CLASIFICACIÓN RESTRINGIDA A UN CONJUNTO ACOTADO: el solicitante garantiza que este documento debería corresponder a UNA de las tipologías del catálogo anterior. Elige la MÁS compatible con el contenido del documento, aunque este pudiera encajar de forma natural en otra categoría documental no listada. Responde con tipologia null SOLO si el contenido no guarda ninguna relación razonable con ninguna de las tipologías listadas. No inventes códigos fuera del catálogo.';
DECLARE @restrictedUserContent NVARCHAR(MAX) =
    N'{CONTEXT_PROMPT}' + CHAR(10) + CHAR(10) +
    N'TIPOLOGÍAS CANDIDATAS (el solicitante garantiza que el documento debería ser una de estas):' + CHAR(10) +
    N'{CATALOGO}' + CHAR(10) + CHAR(10) +
    N'CONTENIDO DEL DOCUMENTO (texto/markdown):' + CHAR(10) +
    N'{DOCUMENT_TEXT}';

IF NOT EXISTS (SELECT 1 FROM [PromptTemplates] WHERE [PromptKey] = N'classification.restricted.system')
BEGIN
    INSERT INTO [PromptTemplates] ([PromptKey], [Version], [Content], [IsActive], [Description], [CreatedAtUtc], [CreatedBy], [PublishedAtUtc], [PublishedBy])
    VALUES (
        N'classification.restricted.system',
        1,
        @restrictedSystemContent,
        1,
        N'System prompt de la clasificación restringida en fase única (AB#100063): compara el documento contra el catálogo acotado de tipologías candidatas.',
        @now,
        N'seed-migration',
        @now,
        N'seed-migration'
    );
END

IF NOT EXISTS (SELECT 1 FROM [PromptTemplates] WHERE [PromptKey] = N'classification.restricted.user')
BEGIN
    INSERT INTO [PromptTemplates] ([PromptKey], [Version], [Content], [IsActive], [Description], [CreatedAtUtc], [CreatedBy], [PublishedAtUtc], [PublishedBy])
    VALUES (
        N'classification.restricted.user',
        1,
        @restrictedUserContent,
        1,
        N'User prompt de la clasificación restringida en fase única (AB#100063): plantilla con el catálogo acotado de tipologías candidatas.',
        @now,
        N'seed-migration',
        @now,
        N'seed-migration'
    );
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "PromptTemplates",
                keyColumn: "PromptKey",
                keyValue: "classification.restricted.system");

            migrationBuilder.DeleteData(
                table: "PromptTemplates",
                keyColumn: "PromptKey",
                keyValue: "classification.restricted.user");

            migrationBuilder.UpdateData(
                table: "Tipologias",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "FechaCreacion", "PublicadaEn" },
                values: new object[] { new DateTime(2026, 8, 5, 10, 3, 49, 922, DateTimeKind.Utc).AddTicks(1216), new DateTime(2026, 8, 5, 10, 3, 49, 922, DateTimeKind.Utc).AddTicks(1212) });
        }
    }
}
