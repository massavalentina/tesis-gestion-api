using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TesisGestorApi.Migrations
{
    /// <inheritdoc />
    public partial class FeatRetiroAnticipado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RetirosAnticipados_Estudiantes_EstudianteIdEstudiante",
                table: "RetirosAnticipados");

            migrationBuilder.DropForeignKey(
                name: "FK_RetirosAnticipados_Tutores_TutorIdTutor",
                table: "RetirosAnticipados");

            migrationBuilder.DropIndex(
                name: "IX_RetirosAnticipados_TutorIdTutor",
                table: "RetirosAnticipados");

            migrationBuilder.DropColumn(
                name: "TutorIdTutor",
                table: "RetirosAnticipados");

            migrationBuilder.AlterColumn<Guid>(
                name: "IdTutor",
                table: "RetirosAnticipados",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "EstudianteIdEstudiante",
                table: "RetirosAnticipados",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "ApellidoResponsable",
                table: "RetirosAnticipados",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CorreoResponsable",
                table: "RetirosAnticipados",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DNIResponsable",
                table: "RetirosAnticipados",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "HorarioLimiteReingreso",
                table: "RetirosAnticipados",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NombrePreceptor",
                table: "RetirosAnticipados",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NombreResponsable",
                table: "RetirosAnticipados",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RelacionResponsable",
                table: "RetirosAnticipados",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TelefonoResponsable",
                table: "RetirosAnticipados",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Turno",
                table: "RetirosAnticipados",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_RetirosAnticipados_IdEstudiante",
                table: "RetirosAnticipados",
                column: "IdEstudiante");

            migrationBuilder.CreateIndex(
                name: "IX_RetirosAnticipados_IdTutor",
                table: "RetirosAnticipados",
                column: "IdTutor");

            migrationBuilder.AddForeignKey(
                name: "FK_RetirosAnticipados_Estudiantes_EstudianteIdEstudiante",
                table: "RetirosAnticipados",
                column: "EstudianteIdEstudiante",
                principalTable: "Estudiantes",
                principalColumn: "IdEstudiante");

            migrationBuilder.AddForeignKey(
                name: "FK_RetirosAnticipados_Estudiantes_IdEstudiante",
                table: "RetirosAnticipados",
                column: "IdEstudiante",
                principalTable: "Estudiantes",
                principalColumn: "IdEstudiante",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RetirosAnticipados_Tutores_IdTutor",
                table: "RetirosAnticipados",
                column: "IdTutor",
                principalTable: "Tutores",
                principalColumn: "IdTutor",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RetirosAnticipados_Estudiantes_EstudianteIdEstudiante",
                table: "RetirosAnticipados");

            migrationBuilder.DropForeignKey(
                name: "FK_RetirosAnticipados_Estudiantes_IdEstudiante",
                table: "RetirosAnticipados");

            migrationBuilder.DropForeignKey(
                name: "FK_RetirosAnticipados_Tutores_IdTutor",
                table: "RetirosAnticipados");

            migrationBuilder.DropIndex(
                name: "IX_RetirosAnticipados_IdEstudiante",
                table: "RetirosAnticipados");

            migrationBuilder.DropIndex(
                name: "IX_RetirosAnticipados_IdTutor",
                table: "RetirosAnticipados");

            migrationBuilder.DropColumn(
                name: "ApellidoResponsable",
                table: "RetirosAnticipados");

            migrationBuilder.DropColumn(
                name: "CorreoResponsable",
                table: "RetirosAnticipados");

            migrationBuilder.DropColumn(
                name: "DNIResponsable",
                table: "RetirosAnticipados");

            migrationBuilder.DropColumn(
                name: "HorarioLimiteReingreso",
                table: "RetirosAnticipados");

            migrationBuilder.DropColumn(
                name: "NombrePreceptor",
                table: "RetirosAnticipados");

            migrationBuilder.DropColumn(
                name: "NombreResponsable",
                table: "RetirosAnticipados");

            migrationBuilder.DropColumn(
                name: "RelacionResponsable",
                table: "RetirosAnticipados");

            migrationBuilder.DropColumn(
                name: "TelefonoResponsable",
                table: "RetirosAnticipados");

            migrationBuilder.DropColumn(
                name: "Turno",
                table: "RetirosAnticipados");

            migrationBuilder.AlterColumn<Guid>(
                name: "IdTutor",
                table: "RetirosAnticipados",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "EstudianteIdEstudiante",
                table: "RetirosAnticipados",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TutorIdTutor",
                table: "RetirosAnticipados",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_RetirosAnticipados_TutorIdTutor",
                table: "RetirosAnticipados",
                column: "TutorIdTutor");

            migrationBuilder.AddForeignKey(
                name: "FK_RetirosAnticipados_Estudiantes_EstudianteIdEstudiante",
                table: "RetirosAnticipados",
                column: "EstudianteIdEstudiante",
                principalTable: "Estudiantes",
                principalColumn: "IdEstudiante",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RetirosAnticipados_Tutores_TutorIdTutor",
                table: "RetirosAnticipados",
                column: "TutorIdTutor",
                principalTable: "Tutores",
                principalColumn: "IdTutor",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
