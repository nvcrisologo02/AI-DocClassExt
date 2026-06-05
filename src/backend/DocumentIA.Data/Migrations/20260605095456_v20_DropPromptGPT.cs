using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentIA.Data.Migrations
{
    /// <inheritdoc />
    /// 
    /// ⚠️ FINAL CLEANUP MIGRATION - v2.0 RELEASE
    /// 
    /// This migration removes the deprecated PromptGPT column from Tipologias table.
    /// 
    /// ⚠️ IMPORTANT - DO NOT EXECUTE UNTIL 2026-07-31 ⚠️
    /// 
    /// After 6 months of v1.5 with [Obsolete] warnings, execute this to finalize deprecation.
    /// 
    /// Prerequisites:
    /// - All clients verified to use ConfiguracionJson only
    /// - All code updated to use TipologiaExtensions.GetSystemPrompt()
    /// - Database backed up
    /// 
    /// Estimated downtime: ~100ms
    /// Rollback: Restore from backup (immediate)
    /// 
    public partial class v20_DropPromptGPT : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // FINAL: Remove deprecated PromptGPT column
            migrationBuilder.DropColumn(
                name: "PromptGPT",
                table: "Tipologias");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore deprecated PromptGPT column if rollback needed
            migrationBuilder.AddColumn<string>(
                name: "PromptGPT",
                table: "Tipologias",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
