using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TesisGestorApi.Migrations
{
    /// <inheritdoc />
    public partial class FeatImportacionCalificacionesCidi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "IdImportacionCalificaciones",
                table: "Calificacion",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "IdImportacionCalificaciones",
                table: "AuditoriaCalificacionSesion",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ImportacionCalificaciones",
                columns: table => new
                {
                    IdImportacionCalificaciones = table.Column<Guid>(type: "uuid", nullable: false),
                    IdEC = table.Column<Guid>(type: "uuid", nullable: false),
                    IdCurso = table.Column<Guid>(type: "uuid", nullable: false),
                    IdDocente = table.Column<Guid>(type: "uuid", nullable: false),
                    IdUsuario = table.Column<Guid>(type: "uuid", nullable: false),
                    AnioLectivo = table.Column<int>(type: "integer", nullable: false),
                    Estado = table.Column<int>(type: "integer", nullable: false),
                    NombreArchivoOriginal = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TamanioArchivoBytes = table.Column<long>(type: "bigint", nullable: false),
                    HashArchivoSha256 = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    MotorLectura = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ArchivoTemporalContenido = table.Column<byte[]>(type: "bytea", nullable: true),
                    RutaArchivoFinal = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ResumenAnalisisJson = table.Column<string>(type: "text", nullable: false),
                    RevisionJson = table.Column<string>(type: "text", nullable: true),
                    ResumenConfirmacionJson = table.Column<string>(type: "text", nullable: true),
                    ErrorTecnico = table.Column<string>(type: "text", nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaUltimaActualizacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaConfirmacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportacionCalificaciones", x => x.IdImportacionCalificaciones);
                    table.ForeignKey(
                        name: "FK_ImportacionCalificaciones_Cursos_IdCurso",
                        column: x => x.IdCurso,
                        principalTable: "Cursos",
                        principalColumn: "IdCurso",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ImportacionCalificaciones_Docentes_IdDocente",
                        column: x => x.IdDocente,
                        principalTable: "Docentes",
                        principalColumn: "IdDocente",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ImportacionCalificaciones_EspaciosCurriculares_IdEC",
                        column: x => x.IdEC,
                        principalTable: "EspaciosCurriculares",
                        principalColumn: "IdEC",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ImportacionCalificaciones_Usuarios_IdUsuario",
                        column: x => x.IdUsuario,
                        principalTable: "Usuarios",
                        principalColumn: "IdUsuario",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Calificacion_IdImportacionCalificaciones",
                table: "Calificacion",
                column: "IdImportacionCalificaciones");

            migrationBuilder.CreateIndex(
                name: "IX_AuditoriaCalificacionSesion_IdImportacionCalificaciones",
                table: "AuditoriaCalificacionSesion",
                column: "IdImportacionCalificaciones");

            migrationBuilder.CreateIndex(
                name: "IX_ImportacionCalificaciones_IdCurso",
                table: "ImportacionCalificaciones",
                column: "IdCurso");

            migrationBuilder.CreateIndex(
                name: "IX_ImportacionCalificaciones_IdDocente",
                table: "ImportacionCalificaciones",
                column: "IdDocente");

            migrationBuilder.CreateIndex(
                name: "IX_ImportacionCalificaciones_IdEC_Estado_FechaUltimaActualizac~",
                table: "ImportacionCalificaciones",
                columns: new[] { "IdEC", "Estado", "FechaUltimaActualizacion" });

            migrationBuilder.CreateIndex(
                name: "IX_ImportacionCalificaciones_IdUsuario",
                table: "ImportacionCalificaciones",
                column: "IdUsuario");

            migrationBuilder.AddForeignKey(
                name: "FK_AuditoriaCalificacionSesion_ImportacionCalificaciones_IdImp~",
                table: "AuditoriaCalificacionSesion",
                column: "IdImportacionCalificaciones",
                principalTable: "ImportacionCalificaciones",
                principalColumn: "IdImportacionCalificaciones",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Calificacion_ImportacionCalificaciones_IdImportacionCalific~",
                table: "Calificacion",
                column: "IdImportacionCalificaciones",
                principalTable: "ImportacionCalificaciones",
                principalColumn: "IdImportacionCalificaciones",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AuditoriaCalificacionSesion_ImportacionCalificaciones_IdImp~",
                table: "AuditoriaCalificacionSesion");

            migrationBuilder.DropForeignKey(
                name: "FK_Calificacion_ImportacionCalificaciones_IdImportacionCalific~",
                table: "Calificacion");

            migrationBuilder.DropTable(
                name: "ImportacionCalificaciones");

            migrationBuilder.DropIndex(
                name: "IX_Calificacion_IdImportacionCalificaciones",
                table: "Calificacion");

            migrationBuilder.DropIndex(
                name: "IX_AuditoriaCalificacionSesion_IdImportacionCalificaciones",
                table: "AuditoriaCalificacionSesion");

            migrationBuilder.DropColumn(
                name: "IdImportacionCalificaciones",
                table: "Calificacion");

            migrationBuilder.DropColumn(
                name: "IdImportacionCalificaciones",
                table: "AuditoriaCalificacionSesion");
        }
    }
}
