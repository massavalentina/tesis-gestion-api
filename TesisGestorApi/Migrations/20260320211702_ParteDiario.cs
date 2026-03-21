using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TesisGestorApi.Migrations
{
    /// <inheritdoc />
    public partial class ParteDiario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Motivo",
                table: "ClasesDictadas",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PartesDiarios",
                columns: table => new
                {
                    IdParte = table.Column<Guid>(type: "uuid", nullable: false),
                    IdCurso = table.Column<Guid>(type: "uuid", nullable: false),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartesDiarios", x => x.IdParte);
                    table.ForeignKey(
                        name: "FK_PartesDiarios_Cursos_IdCurso",
                        column: x => x.IdCurso,
                        principalTable: "Cursos",
                        principalColumn: "IdCurso",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ComentariosParte",
                columns: table => new
                {
                    IdComentario = table.Column<Guid>(type: "uuid", nullable: false),
                    IdParte = table.Column<Guid>(type: "uuid", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Contenido = table.Column<string>(type: "text", nullable: false),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    Autor = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComentariosParte", x => x.IdComentario);
                    table.ForeignKey(
                        name: "FK_ComentariosParte_PartesDiarios_IdParte",
                        column: x => x.IdParte,
                        principalTable: "PartesDiarios",
                        principalColumn: "IdParte",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ComentariosParte_IdParte",
                table: "ComentariosParte",
                column: "IdParte");

            migrationBuilder.CreateIndex(
                name: "IX_PartesDiarios_IdCurso_Fecha",
                table: "PartesDiarios",
                columns: new[] { "IdCurso", "Fecha" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ComentariosParte");

            migrationBuilder.DropTable(
                name: "PartesDiarios");

            migrationBuilder.DropColumn(
                name: "Motivo",
                table: "ClasesDictadas");
        }
    }
}
