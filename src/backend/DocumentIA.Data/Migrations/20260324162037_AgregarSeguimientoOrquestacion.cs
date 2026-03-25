using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentIA.Data.Migrations
{
    /// <inheritdoc />
    public partial class AgregarSeguimientoOrquestacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActivityTimelineJson",
                table: "DocumentoEjecuciones",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DuracionClasificacionMs",
                table: "DocumentoEjecuciones",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DuracionExtraccionMs",
                table: "DocumentoEjecuciones",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DuracionGDCMs",
                table: "DocumentoEjecuciones",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DuracionIntegracionMs",
                table: "DocumentoEjecuciones",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DuracionPersistenciaMs",
                table: "DocumentoEjecuciones",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DuracionValidacionMs",
                table: "DocumentoEjecuciones",
                type: "int",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Tipologias",
                keyColumn: "Id",
                keyValue: 1,
                column: "FechaCreacion",
                value: new DateTime(2026, 3, 24, 16, 20, 36, 869, DateTimeKind.Utc).AddTicks(6009));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActivityTimelineJson",
                table: "DocumentoEjecuciones");

            migrationBuilder.DropColumn(
                name: "DuracionClasificacionMs",
                table: "DocumentoEjecuciones");

            migrationBuilder.DropColumn(
                name: "DuracionExtraccionMs",
                table: "DocumentoEjecuciones");

            migrationBuilder.DropColumn(
                name: "DuracionGDCMs",
                table: "DocumentoEjecuciones");

            migrationBuilder.DropColumn(
                name: "DuracionIntegracionMs",
                table: "DocumentoEjecuciones");

            migrationBuilder.DropColumn(
                name: "DuracionPersistenciaMs",
                table: "DocumentoEjecuciones");

            migrationBuilder.DropColumn(
                name: "DuracionValidacionMs",
                table: "DocumentoEjecuciones");

            migrationBuilder.UpdateData(
                table: "Tipologias",
                keyColumn: "Id",
                keyValue: 1,
                column: "FechaCreacion",
                value: new DateTime(2026, 2, 27, 13, 2, 46, 171, DateTimeKind.Utc).AddTicks(9791));
        }
    }
}
