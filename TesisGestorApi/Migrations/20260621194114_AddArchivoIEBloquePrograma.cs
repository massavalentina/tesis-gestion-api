using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TesisGestorApi.Migrations
{
    /// <inheritdoc />
    public partial class AddArchivoIEBloquePrograma : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ArchivoIEBloquePrograma",
                columns: table => new
                {
                    IdArchivoIE = table.Column<Guid>(type: "uuid", nullable: false),
                    IdBloquePrograma = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchivoIEBloquePrograma", x => new { x.IdArchivoIE, x.IdBloquePrograma });
                    table.ForeignKey(
                        name: "FK_ArchivoIEBloquePrograma_ArchivoIE_IdArchivoIE",
                        column: x => x.IdArchivoIE,
                        principalTable: "ArchivoIE",
                        principalColumn: "IdArchivoIE",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ArchivoIEBloquePrograma_BloquesProgramas_IdBloquePrograma",
                        column: x => x.IdBloquePrograma,
                        principalTable: "BloquesProgramas",
                        principalColumn: "IdBloquePrograma",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArchivoIEBloquePrograma_IdBloquePrograma",
                table: "ArchivoIEBloquePrograma",
                column: "IdBloquePrograma");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArchivoIEBloquePrograma");
        }
    }
}
