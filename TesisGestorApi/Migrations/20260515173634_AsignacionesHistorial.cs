using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TesisGestorApi.Migrations
{
    /// <inheritdoc />
    public partial class AsignacionesHistorial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocentesEspaciosCurriculares",
                columns: table => new
                {
                    IdDocenteEC = table.Column<Guid>(type: "uuid", nullable: false),
                    IdDocente = table.Column<Guid>(type: "uuid", nullable: false),
                    IdEC = table.Column<Guid>(type: "uuid", nullable: false),
                    FechaDesde = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaHasta = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Motivo = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocentesEspaciosCurriculares", x => x.IdDocenteEC);
                    table.ForeignKey(
                        name: "FK_DocentesEspaciosCurriculares_Docentes_IdDocente",
                        column: x => x.IdDocente,
                        principalTable: "Docentes",
                        principalColumn: "IdDocente",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DocentesEspaciosCurriculares_EspaciosCurriculares_IdEC",
                        column: x => x.IdEC,
                        principalTable: "EspaciosCurriculares",
                        principalColumn: "IdEC",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PreceptoresCursos",
                columns: table => new
                {
                    IdPreceptorCurso = table.Column<Guid>(type: "uuid", nullable: false),
                    IdPreceptor = table.Column<Guid>(type: "uuid", nullable: false),
                    IdCurso = table.Column<Guid>(type: "uuid", nullable: false),
                    FechaDesde = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaHasta = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Motivo = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreceptoresCursos", x => x.IdPreceptorCurso);
                    table.ForeignKey(
                        name: "FK_PreceptoresCursos_Cursos_IdCurso",
                        column: x => x.IdCurso,
                        principalTable: "Cursos",
                        principalColumn: "IdCurso",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PreceptoresCursos_Preceptores_IdPreceptor",
                        column: x => x.IdPreceptor,
                        principalTable: "Preceptores",
                        principalColumn: "IdPreceptor",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocentesEspaciosCurriculares_IdDocente",
                table: "DocentesEspaciosCurriculares",
                column: "IdDocente");

            migrationBuilder.CreateIndex(
                name: "IX_DocentesEspaciosCurriculares_IdEC",
                table: "DocentesEspaciosCurriculares",
                column: "IdEC");

            migrationBuilder.CreateIndex(
                name: "IX_PreceptoresCursos_IdCurso",
                table: "PreceptoresCursos",
                column: "IdCurso");

            migrationBuilder.CreateIndex(
                name: "IX_PreceptoresCursos_IdPreceptor",
                table: "PreceptoresCursos",
                column: "IdPreceptor");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocentesEspaciosCurriculares");

            migrationBuilder.DropTable(
                name: "PreceptoresCursos");
        }
    }
}
