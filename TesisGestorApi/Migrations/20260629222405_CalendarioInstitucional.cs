using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TesisGestorApi.Migrations
{
    /// <inheritdoc />
    public partial class CalendarioInstitucional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EventosInstitucionales",
                columns: table => new
                {
                    IdEvento = table.Column<Guid>(type: "uuid", nullable: false),
                    Titulo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Descripcion = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    TipoEvento = table.Column<int>(type: "integer", nullable: false),
                    FechaInicio = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaFin = table.Column<DateOnly>(type: "date", nullable: false),
                    AnioLectivo = table.Column<int>(type: "integer", nullable: false),
                    ContabilizaAsistencia = table.Column<bool>(type: "boolean", nullable: false),
                    CambioActividad = table.Column<bool>(type: "boolean", nullable: false),
                    ComentarioCambioActividad = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IdUsuarioCreacion = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventosInstitucionales", x => x.IdEvento);
                    table.ForeignKey(
                        name: "FK_EventosInstitucionales_Usuarios_IdUsuarioCreacion",
                        column: x => x.IdUsuarioCreacion,
                        principalTable: "Usuarios",
                        principalColumn: "IdUsuario",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AuditoriasEventosInstitucionales",
                columns: table => new
                {
                    IdAuditoria = table.Column<Guid>(type: "uuid", nullable: false),
                    IdEvento = table.Column<Guid>(type: "uuid", nullable: false),
                    TipoOperacion = table.Column<int>(type: "integer", nullable: false),
                    ValoresAnteriores = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ValoresNuevos = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    IdUsuario = table.Column<Guid>(type: "uuid", nullable: false),
                    FechaRegistro = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditoriasEventosInstitucionales", x => x.IdAuditoria);
                    table.ForeignKey(
                        name: "FK_AuditoriasEventosInstitucionales_EventosInstitucionales_IdE~",
                        column: x => x.IdEvento,
                        principalTable: "EventosInstitucionales",
                        principalColumn: "IdEvento",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AuditoriasEventosInstitucionales_Usuarios_IdUsuario",
                        column: x => x.IdUsuario,
                        principalTable: "Usuarios",
                        principalColumn: "IdUsuario",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EventosInstitucionalCursos",
                columns: table => new
                {
                    IdEvento = table.Column<Guid>(type: "uuid", nullable: false),
                    IdCurso = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventosInstitucionalCursos", x => new { x.IdEvento, x.IdCurso });
                    table.ForeignKey(
                        name: "FK_EventosInstitucionalCursos_Cursos_IdCurso",
                        column: x => x.IdCurso,
                        principalTable: "Cursos",
                        principalColumn: "IdCurso",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EventosInstitucionalCursos_EventosInstitucionales_IdEvento",
                        column: x => x.IdEvento,
                        principalTable: "EventosInstitucionales",
                        principalColumn: "IdEvento",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditoriasEventosInstitucionales_IdEvento",
                table: "AuditoriasEventosInstitucionales",
                column: "IdEvento");

            migrationBuilder.CreateIndex(
                name: "IX_AuditoriasEventosInstitucionales_IdUsuario",
                table: "AuditoriasEventosInstitucionales",
                column: "IdUsuario");

            migrationBuilder.CreateIndex(
                name: "IX_EventosInstitucionalCursos_IdCurso",
                table: "EventosInstitucionalCursos",
                column: "IdCurso");

            migrationBuilder.CreateIndex(
                name: "IX_EventosInstitucionales_AnioLectivo_Activo",
                table: "EventosInstitucionales",
                columns: new[] { "AnioLectivo", "Activo" });

            migrationBuilder.CreateIndex(
                name: "IX_EventosInstitucionales_IdUsuarioCreacion",
                table: "EventosInstitucionales",
                column: "IdUsuarioCreacion");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditoriasEventosInstitucionales");

            migrationBuilder.DropTable(
                name: "EventosInstitucionalCursos");

            migrationBuilder.DropTable(
                name: "EventosInstitucionales");
        }
    }
}
