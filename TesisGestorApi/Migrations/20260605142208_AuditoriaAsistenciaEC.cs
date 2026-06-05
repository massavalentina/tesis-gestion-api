using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TesisGestorApi.Migrations
{
    /// <inheritdoc />
    public partial class AuditoriaAsistenciaEC : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditoriasAsistenciaEC",
                columns: table => new
                {
                    IdAuditoria = table.Column<Guid>(type: "uuid", nullable: false),
                    IdEstudiante = table.Column<Guid>(type: "uuid", nullable: false),
                    IdClaseDictada = table.Column<Guid>(type: "uuid", nullable: false),
                    TipoEvento = table.Column<int>(type: "integer", nullable: false),
                    EstadoAnterior = table.Column<bool>(type: "boolean", nullable: true),
                    EstadoNuevo = table.Column<bool>(type: "boolean", nullable: false),
                    HorarioEvento = table.Column<TimeSpan>(type: "interval", nullable: false),
                    IdUsuario = table.Column<Guid>(type: "uuid", nullable: false),
                    FechaRegistro = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditoriasAsistenciaEC", x => x.IdAuditoria);
                    table.ForeignKey(
                        name: "FK_AuditoriasAsistenciaEC_ClasesDictadas_IdClaseDictada",
                        column: x => x.IdClaseDictada,
                        principalTable: "ClasesDictadas",
                        principalColumn: "IdClaseDictada",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AuditoriasAsistenciaEC_Estudiantes_IdEstudiante",
                        column: x => x.IdEstudiante,
                        principalTable: "Estudiantes",
                        principalColumn: "IdEstudiante",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuditoriasAsistenciaEC_Usuarios_IdUsuario",
                        column: x => x.IdUsuario,
                        principalTable: "Usuarios",
                        principalColumn: "IdUsuario",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditoriasAsistenciaEC_IdClaseDictada",
                table: "AuditoriasAsistenciaEC",
                column: "IdClaseDictada");

            migrationBuilder.CreateIndex(
                name: "IX_AuditoriasAsistenciaEC_IdEstudiante_IdClaseDictada_FechaReg~",
                table: "AuditoriasAsistenciaEC",
                columns: new[] { "IdEstudiante", "IdClaseDictada", "FechaRegistro" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditoriasAsistenciaEC_IdUsuario",
                table: "AuditoriasAsistenciaEC",
                column: "IdUsuario");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditoriasAsistenciaEC");
        }
    }
}
