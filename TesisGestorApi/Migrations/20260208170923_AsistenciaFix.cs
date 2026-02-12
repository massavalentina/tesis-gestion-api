using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TesisGestorApi.Migrations
{
    /// <inheritdoc />
    public partial class AsistenciaFix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Asistencias_Estudiantes_EstudianteIdEstudiante",
                table: "Asistencias");

            migrationBuilder.DropForeignKey(
                name: "FK_Asistencias_TiposAsistencia_TipoAsistenciaIdTipo",
                table: "Asistencias");

            migrationBuilder.DropForeignKey(
                name: "FK_RetirosAnticipados_Asistencias_IdAsistencia",
                table: "RetirosAnticipados");

            migrationBuilder.DropIndex(
                name: "IX_RetirosAnticipados_IdAsistencia",
                table: "RetirosAnticipados");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Asistencias",
                table: "Asistencias");

            migrationBuilder.DropIndex(
                name: "IX_Asistencias_EstudianteIdEstudiante",
                table: "Asistencias");

            migrationBuilder.DropColumn(
                name: "ValorAsistenciaMañana",
                table: "TiposAsistencia");

            migrationBuilder.DropColumn(
                name: "ValorAsistenciaTarde",
                table: "TiposAsistencia");

            migrationBuilder.DropColumn(
                name: "IdAsistencia",
                table: "Asistencias");

            migrationBuilder.DropColumn(
                name: "EstudianteIdEstudiante",
                table: "Asistencias");

            migrationBuilder.DropColumn(
                name: "IdEstudiante",
                table: "Asistencias");

            migrationBuilder.DropColumn(
                name: "Turno",
                table: "Asistencias");

            migrationBuilder.RenameColumn(
                name: "TipoAsistenciaIdTipo",
                table: "Asistencias",
                newName: "EstudianteId");

            migrationBuilder.RenameColumn(
                name: "IdTipoAsistencia",
                table: "Asistencias",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "FechaAsistencia",
                table: "Asistencias",
                newName: "Fecha");

            migrationBuilder.RenameIndex(
                name: "IX_Asistencias_TipoAsistenciaIdTipo",
                table: "Asistencias",
                newName: "IX_Asistencias_EstudianteId");

            migrationBuilder.AlterColumn<string>(
                name: "Codigo",
                table: "TiposAsistencia",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "Descripcion",
                table: "TiposAsistencia",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "ValorBase",
                table: "TiposAsistencia",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "AsistenciaId",
                table: "RetirosAnticipados",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));


            migrationBuilder.AddColumn<Guid>(
                name: "TipoManianaId",
                table: "Asistencias",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TipoTardeId",
                table: "Asistencias",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ValorTotalInasistencia",
                table: "Asistencias",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Asistencias",
                table: "Asistencias",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_RetirosAnticipados_AsistenciaId",
                table: "RetirosAnticipados",
                column: "AsistenciaId");

            migrationBuilder.CreateIndex(
                name: "IX_Asistencias_TipoManianaId",
                table: "Asistencias",
                column: "TipoManianaId");

            migrationBuilder.CreateIndex(
                name: "IX_Asistencias_TipoTardeId",
                table: "Asistencias",
                column: "TipoTardeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Asistencias_Estudiantes_EstudianteId",
                table: "Asistencias",
                column: "EstudianteId",
                principalTable: "Estudiantes",
                principalColumn: "IdEstudiante",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Asistencias_TiposAsistencia_TipoManianaId",
                table: "Asistencias",
                column: "TipoManianaId",
                principalTable: "TiposAsistencia",
                principalColumn: "IdTipo");

            migrationBuilder.AddForeignKey(
                name: "FK_Asistencias_TiposAsistencia_TipoTardeId",
                table: "Asistencias",
                column: "TipoTardeId",
                principalTable: "TiposAsistencia",
                principalColumn: "IdTipo");

            migrationBuilder.AddForeignKey(
                name: "FK_RetirosAnticipados_Asistencias_AsistenciaId",
                table: "RetirosAnticipados",
                column: "AsistenciaId",
                principalTable: "Asistencias",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Asistencias_Estudiantes_EstudianteId",
                table: "Asistencias");

            migrationBuilder.DropForeignKey(
                name: "FK_Asistencias_TiposAsistencia_TipoManianaId",
                table: "Asistencias");

            migrationBuilder.DropForeignKey(
                name: "FK_Asistencias_TiposAsistencia_TipoTardeId",
                table: "Asistencias");

            migrationBuilder.DropForeignKey(
                name: "FK_RetirosAnticipados_Asistencias_AsistenciaId",
                table: "RetirosAnticipados");

            migrationBuilder.DropIndex(
                name: "IX_RetirosAnticipados_AsistenciaId",
                table: "RetirosAnticipados");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Asistencias",
                table: "Asistencias");

            migrationBuilder.DropIndex(
                name: "IX_Asistencias_TipoManianaId",
                table: "Asistencias");

            migrationBuilder.DropIndex(
                name: "IX_Asistencias_TipoTardeId",
                table: "Asistencias");

            migrationBuilder.DropColumn(
                name: "Descripcion",
                table: "TiposAsistencia");

            migrationBuilder.DropColumn(
                name: "ValorBase",
                table: "TiposAsistencia");

            migrationBuilder.DropColumn(
                name: "AsistenciaId",
                table: "RetirosAnticipados");

            migrationBuilder.DropColumn(
                name: "TipoManianaId",
                table: "Asistencias");

            migrationBuilder.DropColumn(
                name: "TipoTardeId",
                table: "Asistencias");

            migrationBuilder.DropColumn(
                name: "ValorTotalInasistencia",
                table: "Asistencias");

            migrationBuilder.RenameColumn(
                name: "Fecha",
                table: "Asistencias",
                newName: "FechaAsistencia");

            migrationBuilder.RenameColumn(
                name: "EstudianteId",
                table: "Asistencias",
                newName: "TipoAsistenciaIdTipo");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "Asistencias",
                newName: "IdTipoAsistencia");

            migrationBuilder.RenameIndex(
                name: "IX_Asistencias_EstudianteId",
                table: "Asistencias",
                newName: "IX_Asistencias_TipoAsistenciaIdTipo");

            migrationBuilder.AlterColumn<string>(
                name: "Codigo",
                table: "TiposAsistencia",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10);

            migrationBuilder.AddColumn<decimal>(
                name: "ValorAsistenciaMañana",
                table: "TiposAsistencia",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ValorAsistenciaTarde",
                table: "TiposAsistencia",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "IdAsistencia",
                table: "Asistencias",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "EstudianteIdEstudiante",
                table: "Asistencias",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "IdEstudiante",
                table: "Asistencias",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "Turno",
                table: "Asistencias",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Asistencias",
                table: "Asistencias",
                column: "IdAsistencia");

            migrationBuilder.CreateIndex(
                name: "IX_RetirosAnticipados_IdAsistencia",
                table: "RetirosAnticipados",
                column: "IdAsistencia",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Asistencias_EstudianteIdEstudiante",
                table: "Asistencias",
                column: "EstudianteIdEstudiante");

            migrationBuilder.AddForeignKey(
                name: "FK_Asistencias_Estudiantes_EstudianteIdEstudiante",
                table: "Asistencias",
                column: "EstudianteIdEstudiante",
                principalTable: "Estudiantes",
                principalColumn: "IdEstudiante",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Asistencias_TiposAsistencia_TipoAsistenciaIdTipo",
                table: "Asistencias",
                column: "TipoAsistenciaIdTipo",
                principalTable: "TiposAsistencia",
                principalColumn: "IdTipo",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RetirosAnticipados_Asistencias_IdAsistencia",
                table: "RetirosAnticipados",
                column: "IdAsistencia",
                principalTable: "Asistencias",
                principalColumn: "IdAsistencia",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
