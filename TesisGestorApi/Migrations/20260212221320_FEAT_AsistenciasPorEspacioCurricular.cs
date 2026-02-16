using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TesisGestorApi.Migrations
{
    /// <inheritdoc />
    public partial class FEAT_AsistenciasPorEspacioCurricular : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AsistenciasPorEspacio_Curriculas_CurriculaIdCurricula",
                table: "AsistenciasPorEspacio");

            migrationBuilder.DropForeignKey(
                name: "FK_AsistenciasPorEspacio_Estudiantes_EstudianteIdEstudiante",
                table: "AsistenciasPorEspacio");

            migrationBuilder.DropForeignKey(
                name: "FK_AsistenciasPorEspacio_TiposAsistencia_TipoAsistenciaIdTipo",
                table: "AsistenciasPorEspacio");

            migrationBuilder.DropForeignKey(
                name: "FK_Curriculas_Cursos_CursoIdCurso",
                table: "Curriculas");

            migrationBuilder.DropForeignKey(
                name: "FK_Curriculas_Docentes_DocenteIdDocente",
                table: "Curriculas");

            migrationBuilder.DropForeignKey(
                name: "FK_Curriculas_EspaciosCurriculares_EspacioCurricularIdEC",
                table: "Curriculas");

            migrationBuilder.DropForeignKey(
                name: "FK_Curriculas_Horarios_HorarioIdHorario",
                table: "Curriculas");

            migrationBuilder.DropForeignKey(
                name: "FK_Horarios_Cursos_CursoIdCurso",
                table: "Horarios");

            migrationBuilder.DropForeignKey(
                name: "FK_RetirosAnticipados_Asistencias_AsistenciaId",
                table: "RetirosAnticipados");

            migrationBuilder.DropIndex(
                name: "IX_RetirosAnticipados_AsistenciaId",
                table: "RetirosAnticipados");

            migrationBuilder.DropIndex(
                name: "IX_Curriculas_CursoIdCurso",
                table: "Curriculas");

            migrationBuilder.DropIndex(
                name: "IX_Curriculas_DocenteIdDocente",
                table: "Curriculas");

            migrationBuilder.DropIndex(
                name: "IX_Curriculas_EspacioCurricularIdEC",
                table: "Curriculas");

            migrationBuilder.DropIndex(
                name: "IX_Curriculas_HorarioIdHorario",
                table: "Curriculas");

            migrationBuilder.DropIndex(
                name: "IX_AsistenciasPorEspacio_CurriculaIdCurricula",
                table: "AsistenciasPorEspacio");

            migrationBuilder.DropIndex(
                name: "IX_AsistenciasPorEspacio_EstudianteIdEstudiante",
                table: "AsistenciasPorEspacio");

            migrationBuilder.DropIndex(
                name: "IX_AsistenciasPorEspacio_TipoAsistenciaIdTipo",
                table: "AsistenciasPorEspacio");

            migrationBuilder.DropIndex(
                name: "IX_Asistencias_EstudianteId",
                table: "Asistencias");

            migrationBuilder.DropColumn(
                name: "AsistenciaId",
                table: "RetirosAnticipados");

            migrationBuilder.DropColumn(
                name: "Codigo",
                table: "EspaciosCurriculares");

            migrationBuilder.DropColumn(
                name: "Descripcion",
                table: "EspaciosCurriculares");

            migrationBuilder.DropColumn(
                name: "EsContraturno",
                table: "EspaciosCurriculares");

            migrationBuilder.DropColumn(
                name: "Estado",
                table: "EspaciosCurriculares");

            migrationBuilder.DropColumn(
                name: "Nombre",
                table: "EspaciosCurriculares");

            migrationBuilder.DropColumn(
                name: "CursoIdCurso",
                table: "Curriculas");

            migrationBuilder.DropColumn(
                name: "DocenteIdDocente",
                table: "Curriculas");

            migrationBuilder.DropColumn(
                name: "EspacioCurricularIdEC",
                table: "Curriculas");

            migrationBuilder.DropColumn(
                name: "HorarioIdHorario",
                table: "Curriculas");

            migrationBuilder.DropColumn(
                name: "IdCurso",
                table: "Curriculas");

            migrationBuilder.DropColumn(
                name: "IdDocente",
                table: "Curriculas");

            migrationBuilder.DropColumn(
                name: "IdEspacioCurricular",
                table: "Curriculas");

            migrationBuilder.DropColumn(
                name: "IdHorario",
                table: "Curriculas");

            migrationBuilder.DropColumn(
                name: "CurriculaIdCurricula",
                table: "AsistenciasPorEspacio");

            migrationBuilder.DropColumn(
                name: "EstudianteIdEstudiante",
                table: "AsistenciasPorEspacio");

            migrationBuilder.DropColumn(
                name: "FechaAsistencia",
                table: "AsistenciasPorEspacio");

            migrationBuilder.RenameColumn(
                name: "CursoIdCurso",
                table: "Horarios",
                newName: "IdEC");

            migrationBuilder.RenameIndex(
                name: "IX_Horarios_CursoIdCurso",
                table: "Horarios",
                newName: "IX_Horarios_IdEC");

            migrationBuilder.RenameColumn(
                name: "TipoAsistenciaIdTipo",
                table: "AsistenciasPorEspacio",
                newName: "IdEC");

            migrationBuilder.RenameColumn(
                name: "IdTipoAsistencia",
                table: "AsistenciasPorEspacio",
                newName: "IdClaseDictada");

            migrationBuilder.RenameColumn(
                name: "IdCurricula",
                table: "AsistenciasPorEspacio",
                newName: "EspacioCurricularIdEC");

            migrationBuilder.AddColumn<int>(
                name: "DíaSemana",
                table: "Horarios",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "IdCurricula",
                table: "EspaciosCurriculares",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "IdCurso",
                table: "EspaciosCurriculares",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "IdDocente",
                table: "EspaciosCurriculares",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "Codigo",
                table: "Curriculas",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Descripcion",
                table: "Curriculas",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "EsContraturno",
                table: "Curriculas",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Estado",
                table: "Curriculas",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Nombre",
                table: "Curriculas",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateOnly>(
                name: "Fecha",
                table: "AsistenciasPorEspacio",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<string>(
                name: "Motivo",
                table: "AsistenciasPorEspacio",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "Presente",
                table: "AsistenciasPorEspacio",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "EstudianteIdEstudiante",
                table: "Asistencias",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ClasesDictadas",
                columns: table => new
                {
                    IdClaseDictada = table.Column<Guid>(type: "uuid", nullable: false),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    Tema = table.Column<string>(type: "text", nullable: true),
                    Dictada = table.Column<bool>(type: "boolean", nullable: false),
                    IdEC = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClasesDictadas", x => x.IdClaseDictada);
                    table.ForeignKey(
                        name: "FK_ClasesDictadas_EspaciosCurriculares_IdEC",
                        column: x => x.IdEC,
                        principalTable: "EspaciosCurriculares",
                        principalColumn: "IdEC",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RetirosAnticipados_IdAsistencia",
                table: "RetirosAnticipados",
                column: "IdAsistencia",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Horarios_IdCurso",
                table: "Horarios",
                column: "IdCurso");

            migrationBuilder.CreateIndex(
                name: "IX_EspaciosCurriculares_IdCurricula",
                table: "EspaciosCurriculares",
                column: "IdCurricula");

            migrationBuilder.CreateIndex(
                name: "IX_EspaciosCurriculares_IdCurso",
                table: "EspaciosCurriculares",
                column: "IdCurso");

            migrationBuilder.CreateIndex(
                name: "IX_EspaciosCurriculares_IdDocente",
                table: "EspaciosCurriculares",
                column: "IdDocente");

            migrationBuilder.CreateIndex(
                name: "IX_AsistenciasPorEspacio_EspacioCurricularIdEC",
                table: "AsistenciasPorEspacio",
                column: "EspacioCurricularIdEC");

            migrationBuilder.CreateIndex(
                name: "IX_AsistenciasPorEspacio_IdClaseDictada_IdEstudiante",
                table: "AsistenciasPorEspacio",
                columns: new[] { "IdClaseDictada", "IdEstudiante" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AsistenciasPorEspacio_IdEstudiante",
                table: "AsistenciasPorEspacio",
                column: "IdEstudiante");

            migrationBuilder.CreateIndex(
                name: "IX_Asistencias_EstudianteId_Fecha",
                table: "Asistencias",
                columns: new[] { "EstudianteId", "Fecha" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Asistencias_EstudianteIdEstudiante",
                table: "Asistencias",
                column: "EstudianteIdEstudiante");

            migrationBuilder.CreateIndex(
                name: "IX_ClasesDictadas_IdEC_Fecha",
                table: "ClasesDictadas",
                columns: new[] { "IdEC", "Fecha" });

            migrationBuilder.AddForeignKey(
                name: "FK_Asistencias_Estudiantes_EstudianteIdEstudiante",
                table: "Asistencias",
                column: "EstudianteIdEstudiante",
                principalTable: "Estudiantes",
                principalColumn: "IdEstudiante");

            migrationBuilder.AddForeignKey(
                name: "FK_AsistenciasPorEspacio_ClasesDictadas_IdClaseDictada",
                table: "AsistenciasPorEspacio",
                column: "IdClaseDictada",
                principalTable: "ClasesDictadas",
                principalColumn: "IdClaseDictada",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AsistenciasPorEspacio_EspaciosCurriculares_EspacioCurricula~",
                table: "AsistenciasPorEspacio",
                column: "EspacioCurricularIdEC",
                principalTable: "EspaciosCurriculares",
                principalColumn: "IdEC",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AsistenciasPorEspacio_Estudiantes_IdEstudiante",
                table: "AsistenciasPorEspacio",
                column: "IdEstudiante",
                principalTable: "Estudiantes",
                principalColumn: "IdEstudiante",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EspaciosCurriculares_Curriculas_IdCurricula",
                table: "EspaciosCurriculares",
                column: "IdCurricula",
                principalTable: "Curriculas",
                principalColumn: "IdCurricula",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EspaciosCurriculares_Cursos_IdCurso",
                table: "EspaciosCurriculares",
                column: "IdCurso",
                principalTable: "Cursos",
                principalColumn: "IdCurso",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_EspaciosCurriculares_Docentes_IdDocente",
                table: "EspaciosCurriculares",
                column: "IdDocente",
                principalTable: "Docentes",
                principalColumn: "IdDocente",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Horarios_Cursos_IdCurso",
                table: "Horarios",
                column: "IdCurso",
                principalTable: "Cursos",
                principalColumn: "IdCurso",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Horarios_EspaciosCurriculares_IdEC",
                table: "Horarios",
                column: "IdEC",
                principalTable: "EspaciosCurriculares",
                principalColumn: "IdEC",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RetirosAnticipados_Asistencias_IdAsistencia",
                table: "RetirosAnticipados",
                column: "IdAsistencia",
                principalTable: "Asistencias",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Asistencias_Estudiantes_EstudianteIdEstudiante",
                table: "Asistencias");

            migrationBuilder.DropForeignKey(
                name: "FK_AsistenciasPorEspacio_ClasesDictadas_IdClaseDictada",
                table: "AsistenciasPorEspacio");

            migrationBuilder.DropForeignKey(
                name: "FK_AsistenciasPorEspacio_EspaciosCurriculares_EspacioCurricula~",
                table: "AsistenciasPorEspacio");

            migrationBuilder.DropForeignKey(
                name: "FK_AsistenciasPorEspacio_Estudiantes_IdEstudiante",
                table: "AsistenciasPorEspacio");

            migrationBuilder.DropForeignKey(
                name: "FK_EspaciosCurriculares_Curriculas_IdCurricula",
                table: "EspaciosCurriculares");

            migrationBuilder.DropForeignKey(
                name: "FK_EspaciosCurriculares_Cursos_IdCurso",
                table: "EspaciosCurriculares");

            migrationBuilder.DropForeignKey(
                name: "FK_EspaciosCurriculares_Docentes_IdDocente",
                table: "EspaciosCurriculares");

            migrationBuilder.DropForeignKey(
                name: "FK_Horarios_Cursos_IdCurso",
                table: "Horarios");

            migrationBuilder.DropForeignKey(
                name: "FK_Horarios_EspaciosCurriculares_IdEC",
                table: "Horarios");

            migrationBuilder.DropForeignKey(
                name: "FK_RetirosAnticipados_Asistencias_IdAsistencia",
                table: "RetirosAnticipados");

            migrationBuilder.DropTable(
                name: "ClasesDictadas");

            migrationBuilder.DropIndex(
                name: "IX_RetirosAnticipados_IdAsistencia",
                table: "RetirosAnticipados");

            migrationBuilder.DropIndex(
                name: "IX_Horarios_IdCurso",
                table: "Horarios");

            migrationBuilder.DropIndex(
                name: "IX_EspaciosCurriculares_IdCurricula",
                table: "EspaciosCurriculares");

            migrationBuilder.DropIndex(
                name: "IX_EspaciosCurriculares_IdCurso",
                table: "EspaciosCurriculares");

            migrationBuilder.DropIndex(
                name: "IX_EspaciosCurriculares_IdDocente",
                table: "EspaciosCurriculares");

            migrationBuilder.DropIndex(
                name: "IX_AsistenciasPorEspacio_EspacioCurricularIdEC",
                table: "AsistenciasPorEspacio");

            migrationBuilder.DropIndex(
                name: "IX_AsistenciasPorEspacio_IdClaseDictada_IdEstudiante",
                table: "AsistenciasPorEspacio");

            migrationBuilder.DropIndex(
                name: "IX_AsistenciasPorEspacio_IdEstudiante",
                table: "AsistenciasPorEspacio");

            migrationBuilder.DropIndex(
                name: "IX_Asistencias_EstudianteId_Fecha",
                table: "Asistencias");

            migrationBuilder.DropIndex(
                name: "IX_Asistencias_EstudianteIdEstudiante",
                table: "Asistencias");

            migrationBuilder.DropColumn(
                name: "DíaSemana",
                table: "Horarios");

            migrationBuilder.DropColumn(
                name: "IdCurricula",
                table: "EspaciosCurriculares");

            migrationBuilder.DropColumn(
                name: "IdCurso",
                table: "EspaciosCurriculares");

            migrationBuilder.DropColumn(
                name: "IdDocente",
                table: "EspaciosCurriculares");

            migrationBuilder.DropColumn(
                name: "Codigo",
                table: "Curriculas");

            migrationBuilder.DropColumn(
                name: "Descripcion",
                table: "Curriculas");

            migrationBuilder.DropColumn(
                name: "EsContraturno",
                table: "Curriculas");

            migrationBuilder.DropColumn(
                name: "Estado",
                table: "Curriculas");

            migrationBuilder.DropColumn(
                name: "Nombre",
                table: "Curriculas");

            migrationBuilder.DropColumn(
                name: "Fecha",
                table: "AsistenciasPorEspacio");

            migrationBuilder.DropColumn(
                name: "Motivo",
                table: "AsistenciasPorEspacio");

            migrationBuilder.DropColumn(
                name: "Presente",
                table: "AsistenciasPorEspacio");

            migrationBuilder.DropColumn(
                name: "EstudianteIdEstudiante",
                table: "Asistencias");

            migrationBuilder.RenameColumn(
                name: "IdEC",
                table: "Horarios",
                newName: "CursoIdCurso");

            migrationBuilder.RenameIndex(
                name: "IX_Horarios_IdEC",
                table: "Horarios",
                newName: "IX_Horarios_CursoIdCurso");

            migrationBuilder.RenameColumn(
                name: "IdEC",
                table: "AsistenciasPorEspacio",
                newName: "TipoAsistenciaIdTipo");

            migrationBuilder.RenameColumn(
                name: "IdClaseDictada",
                table: "AsistenciasPorEspacio",
                newName: "IdTipoAsistencia");

            migrationBuilder.RenameColumn(
                name: "EspacioCurricularIdEC",
                table: "AsistenciasPorEspacio",
                newName: "IdCurricula");

            migrationBuilder.AddColumn<Guid>(
                name: "AsistenciaId",
                table: "RetirosAnticipados",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "Codigo",
                table: "EspaciosCurriculares",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Descripcion",
                table: "EspaciosCurriculares",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "EsContraturno",
                table: "EspaciosCurriculares",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Estado",
                table: "EspaciosCurriculares",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Nombre",
                table: "EspaciosCurriculares",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "CursoIdCurso",
                table: "Curriculas",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "DocenteIdDocente",
                table: "Curriculas",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "EspacioCurricularIdEC",
                table: "Curriculas",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "HorarioIdHorario",
                table: "Curriculas",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "IdCurso",
                table: "Curriculas",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "IdDocente",
                table: "Curriculas",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "IdEspacioCurricular",
                table: "Curriculas",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "IdHorario",
                table: "Curriculas",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CurriculaIdCurricula",
                table: "AsistenciasPorEspacio",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "EstudianteIdEstudiante",
                table: "AsistenciasPorEspacio",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaAsistencia",
                table: "AsistenciasPorEspacio",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateIndex(
                name: "IX_RetirosAnticipados_AsistenciaId",
                table: "RetirosAnticipados",
                column: "AsistenciaId");

            migrationBuilder.CreateIndex(
                name: "IX_Curriculas_CursoIdCurso",
                table: "Curriculas",
                column: "CursoIdCurso");

            migrationBuilder.CreateIndex(
                name: "IX_Curriculas_DocenteIdDocente",
                table: "Curriculas",
                column: "DocenteIdDocente");

            migrationBuilder.CreateIndex(
                name: "IX_Curriculas_EspacioCurricularIdEC",
                table: "Curriculas",
                column: "EspacioCurricularIdEC");

            migrationBuilder.CreateIndex(
                name: "IX_Curriculas_HorarioIdHorario",
                table: "Curriculas",
                column: "HorarioIdHorario");

            migrationBuilder.CreateIndex(
                name: "IX_AsistenciasPorEspacio_CurriculaIdCurricula",
                table: "AsistenciasPorEspacio",
                column: "CurriculaIdCurricula");

            migrationBuilder.CreateIndex(
                name: "IX_AsistenciasPorEspacio_EstudianteIdEstudiante",
                table: "AsistenciasPorEspacio",
                column: "EstudianteIdEstudiante");

            migrationBuilder.CreateIndex(
                name: "IX_AsistenciasPorEspacio_TipoAsistenciaIdTipo",
                table: "AsistenciasPorEspacio",
                column: "TipoAsistenciaIdTipo");

            migrationBuilder.CreateIndex(
                name: "IX_Asistencias_EstudianteId",
                table: "Asistencias",
                column: "EstudianteId");

            migrationBuilder.AddForeignKey(
                name: "FK_AsistenciasPorEspacio_Curriculas_CurriculaIdCurricula",
                table: "AsistenciasPorEspacio",
                column: "CurriculaIdCurricula",
                principalTable: "Curriculas",
                principalColumn: "IdCurricula",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AsistenciasPorEspacio_Estudiantes_EstudianteIdEstudiante",
                table: "AsistenciasPorEspacio",
                column: "EstudianteIdEstudiante",
                principalTable: "Estudiantes",
                principalColumn: "IdEstudiante",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AsistenciasPorEspacio_TiposAsistencia_TipoAsistenciaIdTipo",
                table: "AsistenciasPorEspacio",
                column: "TipoAsistenciaIdTipo",
                principalTable: "TiposAsistencia",
                principalColumn: "IdTipo",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Curriculas_Cursos_CursoIdCurso",
                table: "Curriculas",
                column: "CursoIdCurso",
                principalTable: "Cursos",
                principalColumn: "IdCurso",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Curriculas_Docentes_DocenteIdDocente",
                table: "Curriculas",
                column: "DocenteIdDocente",
                principalTable: "Docentes",
                principalColumn: "IdDocente",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Curriculas_EspaciosCurriculares_EspacioCurricularIdEC",
                table: "Curriculas",
                column: "EspacioCurricularIdEC",
                principalTable: "EspaciosCurriculares",
                principalColumn: "IdEC",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Curriculas_Horarios_HorarioIdHorario",
                table: "Curriculas",
                column: "HorarioIdHorario",
                principalTable: "Horarios",
                principalColumn: "IdHorario",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Horarios_Cursos_CursoIdCurso",
                table: "Horarios",
                column: "CursoIdCurso",
                principalTable: "Cursos",
                principalColumn: "IdCurso",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RetirosAnticipados_Asistencias_AsistenciaId",
                table: "RetirosAnticipados",
                column: "AsistenciaId",
                principalTable: "Asistencias",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
