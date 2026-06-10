using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TesisGestorApi.Migrations
{
    /// <inheritdoc />
    public partial class FeatProgramas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Programas",
                columns: table => new
                {
                    IdPrograma = table.Column<Guid>(type: "uuid", nullable: false),
                    IdDocente = table.Column<Guid>(type: "uuid", nullable: false),
                    IdCurso = table.Column<Guid>(type: "uuid", nullable: false),
                    IdEC = table.Column<Guid>(type: "uuid", nullable: false),
                    AnioLectivo = table.Column<int>(type: "integer", nullable: false),
                    Titulo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Descripcion = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    HorasCatedra = table.Column<int>(type: "integer", nullable: false),
                    Origen = table.Column<int>(type: "integer", nullable: false),
                    Estado = table.Column<int>(type: "integer", nullable: false),
                    FechaVencimiento = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaUltimaModificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Programas", x => x.IdPrograma);
                    table.ForeignKey(
                        name: "FK_Programas_Cursos_IdCurso",
                        column: x => x.IdCurso,
                        principalTable: "Cursos",
                        principalColumn: "IdCurso",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Programas_Docentes_IdDocente",
                        column: x => x.IdDocente,
                        principalTable: "Docentes",
                        principalColumn: "IdDocente",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Programas_EspaciosCurriculares_IdEC",
                        column: x => x.IdEC,
                        principalTable: "EspaciosCurriculares",
                        principalColumn: "IdEC",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ObjetivosPrograma",
                columns: table => new
                {
                    IdObjetivo = table.Column<Guid>(type: "uuid", nullable: false),
                    IdPrograma = table.Column<Guid>(type: "uuid", nullable: false),
                    Descripcion = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Nro = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ObjetivosPrograma", x => x.IdObjetivo);
                    table.ForeignKey(
                        name: "FK_ObjetivosPrograma_Programas_IdPrograma",
                        column: x => x.IdPrograma,
                        principalTable: "Programas",
                        principalColumn: "IdPrograma",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Unidades",
                columns: table => new
                {
                    IdUnidad = table.Column<Guid>(type: "uuid", nullable: false),
                    IdPrograma = table.Column<Guid>(type: "uuid", nullable: false),
                    Titulo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Descripcion = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Nro = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Unidades", x => x.IdUnidad);
                    table.ForeignKey(
                        name: "FK_Unidades_Programas_IdPrograma",
                        column: x => x.IdPrograma,
                        principalTable: "Programas",
                        principalColumn: "IdPrograma",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Temas",
                columns: table => new
                {
                    IdTema = table.Column<Guid>(type: "uuid", nullable: false),
                    IdUnidad = table.Column<Guid>(type: "uuid", nullable: false),
                    Titulo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Descripcion = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Nro = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Temas", x => x.IdTema);
                    table.ForeignKey(
                        name: "FK_Temas_Unidades_IdUnidad",
                        column: x => x.IdUnidad,
                        principalTable: "Unidades",
                        principalColumn: "IdUnidad",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ObjetivosPrograma_IdPrograma",
                table: "ObjetivosPrograma",
                column: "IdPrograma");

            migrationBuilder.CreateIndex(
                name: "IX_Programas_IdCurso",
                table: "Programas",
                column: "IdCurso");

            migrationBuilder.CreateIndex(
                name: "IX_Programas_IdDocente",
                table: "Programas",
                column: "IdDocente");

            migrationBuilder.CreateIndex(
                name: "IX_Programas_IdEC_AnioLectivo",
                table: "Programas",
                columns: new[] { "IdEC", "AnioLectivo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Temas_IdUnidad",
                table: "Temas",
                column: "IdUnidad");

            migrationBuilder.CreateIndex(
                name: "IX_Unidades_IdPrograma",
                table: "Unidades",
                column: "IdPrograma");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ObjetivosPrograma");

            migrationBuilder.DropTable(
                name: "Temas");

            migrationBuilder.DropTable(
                name: "Unidades");

            migrationBuilder.DropTable(
                name: "Programas");
        }
    }
}
