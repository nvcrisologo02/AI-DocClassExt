using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentIA.Data.Migrations
{
    /// <inheritdoc />
    public partial class IndiceDocumentosMD5 : Migration
    {
        // Nota: EF scaffoldea aqui un UpdateData sobre Tipologias por el seed no
        // determinista (DateTime.UtcNow en HasData). Se ha retirado a proposito, igual que
        // en 20260920063449_IndiceMonitorCostes: esta migracion solo crea un indice y no
        // debe reescribir datos.
        //
        // AB#100863: la verificacion de duplicados por MD5 (VerificarDuplicadoPorMD5Activity,
        // ingesta desde GDC) hacia scan completo de Documentos porque MD5 no tenia indice; en
        // PRO (S0, 72k filas, 1,3 GB) superaba el CommandTimeout de 30 s con la cache fria.
        // El INCLUDE de SHA256 resuelve la consulta entera en el indice.
        //
        // En PRO no se aplica tal cual: usar scripts/database/indice-documentos-md5-pro.sql,
        // que crea el indice con ONLINE = ON y registra la migracion.
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Documentos_MD5",
                table: "Documentos",
                column: "MD5")
                .Annotation("SqlServer:Include", new[] { "SHA256" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Documentos_MD5",
                table: "Documentos");
        }
    }
}
