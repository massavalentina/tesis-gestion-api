using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TesisGestorApi.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanificacionClases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BloquesProgramas",
                columns: table => new
                {
                    IdBloquePrograma = table.Column<Guid>(type: "uuid", nullable: false),
                    IdPrograma = table.Column<Guid>(type: "uuid", nullable: false),
                    IdUnidad = table.Column<Guid>(type: "uuid", nullable: false),
                    IdTema = table.Column<Guid>(type: "uuid", nullable: true),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    Estado = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BloquesProgramas", x => x.IdBloquePrograma);
                    table.ForeignKey(
                        name: "FK_BloquesProgramas_Programas_IdPrograma",
                        column: x => x.IdPrograma,
                        principalTable: "Programas",
                        principalColumn: "IdPrograma",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BloquesProgramas_Temas_IdTema",
                        column: x => x.IdTema,
                        principalTable: "Temas",
                        principalColumn: "IdTema");
                    table.ForeignKey(
                        name: "FK_BloquesProgramas_Unidades_IdUnidad",
                        column: x => x.IdUnidad,
                        principalTable: "Unidades",
                        principalColumn: "IdUnidad");
                });

            migrationBuilder.CreateTable(
                name: "Planificaciones",
                columns: table => new
                {
                    IdPlanificacion = table.Column<Guid>(type: "uuid", nullable: false),
                    IdDocente = table.Column<Guid>(type: "uuid", nullable: false),
                    Titulo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Descripcion = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    FechaDesde = table.Column<DateOnly>(type: "date", nullable: true),
                    FechaHasta = table.Column<DateOnly>(type: "date", nullable: true),
                    Estado = table.Column<int>(type: "integer", nullable: false),
                    Url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Planificaciones", x => x.IdPlanificacion);
                    table.ForeignKey(
                        name: "FK_Planificaciones_Docentes_IdDocente",
                        column: x => x.IdDocente,
                        principalTable: "Docentes",
                        principalColumn: "IdDocente",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClasesBloquesProgramas",
                columns: table => new
                {
                    IdClasePlanificacion = table.Column<Guid>(type: "uuid", nullable: false),
                    IdBloquePrograma = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClasesBloquesProgramas", x => new { x.IdClasePlanificacion, x.IdBloquePrograma });
                    table.ForeignKey(
                        name: "FK_ClasesBloquesProgramas_BloquesProgramas_IdBloquePrograma",
                        column: x => x.IdBloquePrograma,
                        principalTable: "BloquesProgramas",
                        principalColumn: "IdBloquePrograma",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClasesBloquesProgramas_Planificaciones_IdClasePlanificacion",
                        column: x => x.IdClasePlanificacion,
                        principalTable: "Planificaciones",
                        principalColumn: "IdPlanificacion",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BloquesProgramas_IdPrograma",
                table: "BloquesProgramas",
                column: "IdPrograma");

            migrationBuilder.CreateIndex(
                name: "IX_BloquesProgramas_IdTema",
                table: "BloquesProgramas",
                column: "IdTema");

            migrationBuilder.CreateIndex(
                name: "IX_BloquesProgramas_IdUnidad",
                table: "BloquesProgramas",
                column: "IdUnidad");

            migrationBuilder.CreateIndex(
                name: "IX_ClasesBloquesProgramas_IdBloquePrograma",
                table: "ClasesBloquesProgramas",
                column: "IdBloquePrograma");

            migrationBuilder.CreateIndex(
                name: "IX_Planificaciones_IdDocente",
                table: "Planificaciones",
                column: "IdDocente");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClasesBloquesProgramas");

            migrationBuilder.DropTable(
                name: "BloquesProgramas");

            migrationBuilder.DropTable(
                name: "Planificaciones");
        }
    }
}
