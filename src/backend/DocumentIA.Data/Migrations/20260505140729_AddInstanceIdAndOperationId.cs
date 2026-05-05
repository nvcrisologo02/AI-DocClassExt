using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentIA.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInstanceIdAndOperationId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InstanceId",
                table: "DocumentoEjecuciones",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OperationId",
                table: "DocumentoEjecuciones",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Tipologias",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "FechaCreacion", "PublicadaEn" },
                values: new object[] { new DateTime(2026, 5, 5, 14, 7, 29, 22, DateTimeKind.Utc).AddTicks(2912), new DateTime(2026, 5, 5, 14, 7, 29, 22, DateTimeKind.Utc).AddTicks(2907) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InstanceId",
                table: "DocumentoEjecuciones");

            migrationBuilder.DropColumn(
                name: "OperationId",
                table: "DocumentoEjecuciones");

            migrationBuilder.UpdateData(
                table: "Tipologias",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "FechaCreacion", "PublicadaEn" },
                values: new object[] { new DateTime(2026, 4, 24, 14, 51, 31, 704, DateTimeKind.Utc).AddTicks(2522), new DateTime(2026, 4, 24, 14, 51, 31, 704, DateTimeKind.Utc).AddTicks(2517) });
        }
    }
}
