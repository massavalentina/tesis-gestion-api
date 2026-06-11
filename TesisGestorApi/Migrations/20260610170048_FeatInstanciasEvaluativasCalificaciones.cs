using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TesisGestorApi.Migrations
{
    /// <inheritdoc />
    public partial class FeatInstanciasEvaluativasCalificaciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InstanciaEvaluativa",
                columns: table => new
                {
                    IdIE = table.Column<Guid>(type: "uuid", nullable: false),
                    IdEC = table.Column<Guid>(type: "uuid", nullable: false),
                    Nro = table.Column<int>(type: "integer", nullable: false),
                    Estado = table.Column<int>(type: "integer", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstanciaEvaluativa", x => x.IdIE);
                    table.ForeignKey(
                        name: "FK_InstanciaEvaluativa_EspaciosCurriculares_IdEC",
                        column: x => x.IdEC,
                        principalTable: "EspaciosCurriculares",
                        principalColumn: "IdEC",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ArchivoIE",
                columns: table => new
                {
                    IdArchivoIE = table.Column<Guid>(type: "uuid", nullable: false),
                    IdIE = table.Column<Guid>(type: "uuid", nullable: false),
                    TipoCalificacion = table.Column<int>(type: "integer", nullable: false),
                    TipoIE = table.Column<int>(type: "integer", nullable: false),
                    Titulo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    NombreArchivo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    UrlArchivo = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    FechaEjecucion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaCarga = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IdUsuarioCarga = table.Column<Guid>(type: "uuid", nullable: false),
                    Habilitada = table.Column<bool>(type: "boolean", nullable: false),
                    IdArchivoIEAnterior = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchivoIE", x => x.IdArchivoIE);
                    table.ForeignKey(
                        name: "FK_ArchivoIE_ArchivoIE_IdArchivoIEAnterior",
                        column: x => x.IdArchivoIEAnterior,
                        principalTable: "ArchivoIE",
                        principalColumn: "IdArchivoIE",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ArchivoIE_InstanciaEvaluativa_IdIE",
                        column: x => x.IdIE,
                        principalTable: "InstanciaEvaluativa",
                        principalColumn: "IdIE",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ArchivoIE_Usuarios_IdUsuarioCarga",
                        column: x => x.IdUsuarioCarga,
                        principalTable: "Usuarios",
                        principalColumn: "IdUsuario",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Calificacion",
                columns: table => new
                {
                    IdCalificacion = table.Column<Guid>(type: "uuid", nullable: false),
                    IdIE = table.Column<Guid>(type: "uuid", nullable: false),
                    IdEstudiante = table.Column<Guid>(type: "uuid", nullable: false),
                    TipoCalificacion = table.Column<int>(type: "integer", nullable: false),
                    IdArchivoIE = table.Column<Guid>(type: "uuid", nullable: false),
                    Puntaje = table.Column<int>(type: "integer", nullable: true),
                    Habilitada = table.Column<bool>(type: "boolean", nullable: false),
                    FechaCarga = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IdUsuarioCarga = table.Column<Guid>(type: "uuid", nullable: false),
                    Origen = table.Column<int>(type: "integer", nullable: false),
                    IdCalificacionAnterior = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Calificacion", x => x.IdCalificacion);
                    table.ForeignKey(
                        name: "FK_Calificacion_ArchivoIE_IdArchivoIE",
                        column: x => x.IdArchivoIE,
                        principalTable: "ArchivoIE",
                        principalColumn: "IdArchivoIE",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Calificacion_Calificacion_IdCalificacionAnterior",
                        column: x => x.IdCalificacionAnterior,
                        principalTable: "Calificacion",
                        principalColumn: "IdCalificacion",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Calificacion_Estudiantes_IdEstudiante",
                        column: x => x.IdEstudiante,
                        principalTable: "Estudiantes",
                        principalColumn: "IdEstudiante",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Calificacion_InstanciaEvaluativa_IdIE",
                        column: x => x.IdIE,
                        principalTable: "InstanciaEvaluativa",
                        principalColumn: "IdIE",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Calificacion_Usuarios_IdUsuarioCarga",
                        column: x => x.IdUsuarioCarga,
                        principalTable: "Usuarios",
                        principalColumn: "IdUsuario",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AuditoriaCalificacionSesion",
                columns: table => new
                {
                    IdSesionAuditoria = table.Column<Guid>(type: "uuid", nullable: false),
                    IdEC = table.Column<Guid>(type: "uuid", nullable: false),
                    IdUsuario = table.Column<Guid>(type: "uuid", nullable: false),
                    Origen = table.Column<int>(type: "integer", nullable: false),
                    FechaRegistro = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditoriaCalificacionSesion", x => x.IdSesionAuditoria);
                    table.ForeignKey(
                        name: "FK_AuditoriaCalificacionSesion_EspaciosCurriculares_IdEC",
                        column: x => x.IdEC,
                        principalTable: "EspaciosCurriculares",
                        principalColumn: "IdEC",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AuditoriaCalificacionSesion_Usuarios_IdUsuario",
                        column: x => x.IdUsuario,
                        principalTable: "Usuarios",
                        principalColumn: "IdUsuario",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AuditoriaCalificacionDetalle",
                columns: table => new
                {
                    IdDetalleAuditoria = table.Column<Guid>(type: "uuid", nullable: false),
                    IdSesionAuditoria = table.Column<Guid>(type: "uuid", nullable: false),
                    IdIE = table.Column<Guid>(type: "uuid", nullable: false),
                    IdEstudiante = table.Column<Guid>(type: "uuid", nullable: false),
                    TipoCalificacion = table.Column<int>(type: "integer", nullable: false),
                    ValorAnterior = table.Column<int>(type: "integer", nullable: true),
                    ValorNuevo = table.Column<int>(type: "integer", nullable: true),
                    IdCalificacionAnterior = table.Column<Guid>(type: "uuid", nullable: true),
                    IdCalificacionNueva = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditoriaCalificacionDetalle", x => x.IdDetalleAuditoria);
                    table.ForeignKey(
                        name: "FK_AuditoriaCalificacionDetalle_AuditoriaCalificacionSesion_IdSesionAuditoria",
                        column: x => x.IdSesionAuditoria,
                        principalTable: "AuditoriaCalificacionSesion",
                        principalColumn: "IdSesionAuditoria",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AuditoriaCalificacionDetalle_Calificacion_IdCalificacionAnterior",
                        column: x => x.IdCalificacionAnterior,
                        principalTable: "Calificacion",
                        principalColumn: "IdCalificacion",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuditoriaCalificacionDetalle_Calificacion_IdCalificacionNueva",
                        column: x => x.IdCalificacionNueva,
                        principalTable: "Calificacion",
                        principalColumn: "IdCalificacion",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuditoriaCalificacionDetalle_Estudiantes_IdEstudiante",
                        column: x => x.IdEstudiante,
                        principalTable: "Estudiantes",
                        principalColumn: "IdEstudiante",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuditoriaCalificacionDetalle_InstanciaEvaluativa_IdIE",
                        column: x => x.IdIE,
                        principalTable: "InstanciaEvaluativa",
                        principalColumn: "IdIE",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArchivoIE_IdArchivoIEAnterior",
                table: "ArchivoIE",
                column: "IdArchivoIEAnterior");

            migrationBuilder.CreateIndex(
                name: "IX_ArchivoIE_IdIE_TipoCalificacion",
                table: "ArchivoIE",
                columns: new[] { "IdIE", "TipoCalificacion" },
                unique: true,
                filter: "\"Habilitada\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_ArchivoIE_IdIE_TipoCalificacion_FechaCarga",
                table: "ArchivoIE",
                columns: new[] { "IdIE", "TipoCalificacion", "FechaCarga" });

            migrationBuilder.CreateIndex(
                name: "IX_ArchivoIE_IdUsuarioCarga",
                table: "ArchivoIE",
                column: "IdUsuarioCarga");

            migrationBuilder.CreateIndex(
                name: "IX_Calificacion_IdArchivoIE",
                table: "Calificacion",
                column: "IdArchivoIE");

            migrationBuilder.CreateIndex(
                name: "IX_Calificacion_IdCalificacionAnterior",
                table: "Calificacion",
                column: "IdCalificacionAnterior");

            migrationBuilder.CreateIndex(
                name: "IX_Calificacion_IdEstudiante",
                table: "Calificacion",
                column: "IdEstudiante");

            migrationBuilder.CreateIndex(
                name: "IX_Calificacion_IdIE_IdEstudiante",
                table: "Calificacion",
                columns: new[] { "IdIE", "IdEstudiante" });

            migrationBuilder.CreateIndex(
                name: "IX_Calificacion_IdIE_IdEstudiante_TipoCalificacion",
                table: "Calificacion",
                columns: new[] { "IdIE", "IdEstudiante", "TipoCalificacion" },
                unique: true,
                filter: "\"Habilitada\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_Calificacion_IdIE_TipoCalificacion",
                table: "Calificacion",
                columns: new[] { "IdIE", "TipoCalificacion" });

            migrationBuilder.CreateIndex(
                name: "IX_Calificacion_IdUsuarioCarga",
                table: "Calificacion",
                column: "IdUsuarioCarga");

            migrationBuilder.CreateIndex(
                name: "IX_AuditoriaCalificacionDetalle_IdCalificacionAnterior",
                table: "AuditoriaCalificacionDetalle",
                column: "IdCalificacionAnterior");

            migrationBuilder.CreateIndex(
                name: "IX_AuditoriaCalificacionDetalle_IdCalificacionNueva",
                table: "AuditoriaCalificacionDetalle",
                column: "IdCalificacionNueva");

            migrationBuilder.CreateIndex(
                name: "IX_AuditoriaCalificacionDetalle_IdEstudiante",
                table: "AuditoriaCalificacionDetalle",
                column: "IdEstudiante");

            migrationBuilder.CreateIndex(
                name: "IX_AuditoriaCalificacionDetalle_IdIE_IdEstudiante",
                table: "AuditoriaCalificacionDetalle",
                columns: new[] { "IdIE", "IdEstudiante" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditoriaCalificacionDetalle_IdSesionAuditoria",
                table: "AuditoriaCalificacionDetalle",
                column: "IdSesionAuditoria");

            migrationBuilder.CreateIndex(
                name: "IX_AuditoriaCalificacionSesion_IdEC_FechaRegistro",
                table: "AuditoriaCalificacionSesion",
                columns: new[] { "IdEC", "FechaRegistro" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditoriaCalificacionSesion_IdUsuario",
                table: "AuditoriaCalificacionSesion",
                column: "IdUsuario");

            migrationBuilder.CreateIndex(
                name: "IX_InstanciaEvaluativa_IdEC_Nro",
                table: "InstanciaEvaluativa",
                columns: new[] { "IdEC", "Nro" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditoriaCalificacionDetalle");

            migrationBuilder.DropTable(
                name: "AuditoriaCalificacionSesion");

            migrationBuilder.DropTable(
                name: "Calificacion");

            migrationBuilder.DropTable(
                name: "ArchivoIE");

            migrationBuilder.DropTable(
                name: "InstanciaEvaluativa");
        }
    }
}
