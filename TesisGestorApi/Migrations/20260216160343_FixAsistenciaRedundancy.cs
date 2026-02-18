using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TesisGestorApi.Migrations
{
    /// <inheritdoc />
    public partial class FixAsistenciaRedundancy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Anios",
                columns: table => new
                {
                    IdAnio = table.Column<Guid>(type: "uuid", nullable: false),
                    Numero = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Anios", x => x.IdAnio);
                });

            migrationBuilder.CreateTable(
                name: "Curriculas",
                columns: table => new
                {
                    IdCurricula = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    Descripcion = table.Column<string>(type: "text", nullable: false),
                    Codigo = table.Column<string>(type: "text", nullable: false),
                    Estado = table.Column<string>(type: "text", nullable: false),
                    EsContraturno = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Curriculas", x => x.IdCurricula);
                });

            migrationBuilder.CreateTable(
                name: "Divisiones",
                columns: table => new
                {
                    IdDivision = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<char>(type: "character(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Divisiones", x => x.IdDivision);
                });

            migrationBuilder.CreateTable(
                name: "Estudiantes",
                columns: table => new
                {
                    IdEstudiante = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    Apellido = table.Column<string>(type: "text", nullable: false),
                    Documento = table.Column<string>(type: "text", nullable: false),
                    FechaNacimiento = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Sexo = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Estudiantes", x => x.IdEstudiante);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    IdRol = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.IdRol);
                });

            migrationBuilder.CreateTable(
                name: "TiposAsistencia",
                columns: table => new
                {
                    IdTipo = table.Column<Guid>(type: "uuid", nullable: false),
                    Codigo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Descripcion = table.Column<string>(type: "text", nullable: false),
                    ValorBase = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TiposAsistencia", x => x.IdTipo);
                });

            migrationBuilder.CreateTable(
                name: "Tutores",
                columns: table => new
                {
                    IdTutor = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    Apellido = table.Column<string>(type: "text", nullable: false),
                    Documento = table.Column<string>(type: "text", nullable: false),
                    Telefono = table.Column<int>(type: "integer", nullable: false),
                    Correo = table.Column<string>(type: "text", nullable: false),
                    RelacionEstudiante = table.Column<string>(type: "text", nullable: false),
                    FechaNacimiento = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Disponibilidad = table.Column<string>(type: "text", nullable: false),
                    EsPrincipal = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tutores", x => x.IdTutor);
                });

            migrationBuilder.CreateTable(
                name: "Usuarios",
                columns: table => new
                {
                    IdUsuario = table.Column<Guid>(type: "uuid", nullable: false),
                    Mail = table.Column<string>(type: "text", nullable: false),
                    Contraseña = table.Column<string>(type: "text", nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    Verificado = table.Column<bool>(type: "boolean", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuarios", x => x.IdUsuario);
                });

            migrationBuilder.CreateTable(
                name: "Cursos",
                columns: table => new
                {
                    IdCurso = table.Column<Guid>(type: "uuid", nullable: false),
                    Codigo = table.Column<string>(type: "text", nullable: false),
                    Estado = table.Column<bool>(type: "boolean", nullable: false),
                    AñoLectivo = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IdAnio = table.Column<Guid>(type: "uuid", nullable: false),
                    IdDivision = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cursos", x => x.IdCurso);
                    table.ForeignKey(
                        name: "FK_Cursos_Anios_IdAnio",
                        column: x => x.IdAnio,
                        principalTable: "Anios",
                        principalColumn: "IdAnio",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Cursos_Divisiones_IdDivision",
                        column: x => x.IdDivision,
                        principalTable: "Divisiones",
                        principalColumn: "IdDivision",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CredencialesQR",
                columns: table => new
                {
                    IdQR = table.Column<Guid>(type: "uuid", nullable: false),
                    Codigo = table.Column<Guid>(type: "uuid", nullable: false),
                    AñoLectivo = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    Enviado = table.Column<bool>(type: "boolean", nullable: false),
                    FechaGeneracion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaExpiracion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IdEstudiante = table.Column<Guid>(type: "uuid", nullable: false),
                    EstudianteIdEstudiante = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CredencialesQR", x => x.IdQR);
                    table.ForeignKey(
                        name: "FK_CredencialesQR_Estudiantes_EstudianteIdEstudiante",
                        column: x => x.EstudianteIdEstudiante,
                        principalTable: "Estudiantes",
                        principalColumn: "IdEstudiante",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Asistencias",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    EstudianteId = table.Column<Guid>(type: "uuid", nullable: false),
                    TipoManianaId = table.Column<Guid>(type: "uuid", nullable: true),
                    HoraEntradaManana = table.Column<TimeSpan>(type: "interval", nullable: true),
                    HoraSalidaManana = table.Column<TimeSpan>(type: "interval", nullable: true),
                    HoraEntradaTarde = table.Column<TimeSpan>(type: "interval", nullable: true),
                    HoraSalidaTarde = table.Column<TimeSpan>(type: "interval", nullable: true),
                    TipoTardeId = table.Column<Guid>(type: "uuid", nullable: true),
                    ValorTotalInasistencia = table.Column<decimal>(type: "numeric", nullable: false),
                    EstudianteIdEstudiante = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Asistencias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Asistencias_Estudiantes_EstudianteId",
                        column: x => x.EstudianteId,
                        principalTable: "Estudiantes",
                        principalColumn: "IdEstudiante",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Asistencias_Estudiantes_EstudianteIdEstudiante",
                        column: x => x.EstudianteIdEstudiante,
                        principalTable: "Estudiantes",
                        principalColumn: "IdEstudiante");
                    table.ForeignKey(
                        name: "FK_Asistencias_TiposAsistencia_TipoManianaId",
                        column: x => x.TipoManianaId,
                        principalTable: "TiposAsistencia",
                        principalColumn: "IdTipo");
                    table.ForeignKey(
                        name: "FK_Asistencias_TiposAsistencia_TipoTardeId",
                        column: x => x.TipoTardeId,
                        principalTable: "TiposAsistencia",
                        principalColumn: "IdTipo");
                });

            migrationBuilder.CreateTable(
                name: "TutorEstudiante",
                columns: table => new
                {
                    IdTutor = table.Column<Guid>(type: "uuid", nullable: false),
                    IdEstudiante = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TutorEstudiante", x => new { x.IdTutor, x.IdEstudiante });
                    table.ForeignKey(
                        name: "FK_TutorEstudiante_Estudiantes_IdEstudiante",
                        column: x => x.IdEstudiante,
                        principalTable: "Estudiantes",
                        principalColumn: "IdEstudiante",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TutorEstudiante_Tutores_IdTutor",
                        column: x => x.IdTutor,
                        principalTable: "Tutores",
                        principalColumn: "IdTutor",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Docentes",
                columns: table => new
                {
                    IdDocente = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    Apellido = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    Documento = table.Column<string>(type: "text", nullable: false),
                    IdUsuario = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Docentes", x => x.IdDocente);
                    table.ForeignKey(
                        name: "FK_Docentes_Usuarios_IdUsuario",
                        column: x => x.IdUsuario,
                        principalTable: "Usuarios",
                        principalColumn: "IdUsuario",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UsuariosRoles",
                columns: table => new
                {
                    IdUsuario = table.Column<Guid>(type: "uuid", nullable: false),
                    IdRol = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsuariosRoles", x => new { x.IdUsuario, x.IdRol });
                    table.ForeignKey(
                        name: "FK_UsuariosRoles_Roles_IdRol",
                        column: x => x.IdRol,
                        principalTable: "Roles",
                        principalColumn: "IdRol",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UsuariosRoles_Usuarios_IdUsuario",
                        column: x => x.IdUsuario,
                        principalTable: "Usuarios",
                        principalColumn: "IdUsuario",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DetallesCursado",
                columns: table => new
                {
                    IdCursado = table.Column<Guid>(type: "uuid", nullable: false),
                    Estado = table.Column<bool>(type: "boolean", nullable: false),
                    IdEstudiante = table.Column<Guid>(type: "uuid", nullable: false),
                    IdCurso = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DetallesCursado", x => x.IdCursado);
                    table.ForeignKey(
                        name: "FK_DetallesCursado_Cursos_IdCurso",
                        column: x => x.IdCurso,
                        principalTable: "Cursos",
                        principalColumn: "IdCurso",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DetallesCursado_Estudiantes_IdEstudiante",
                        column: x => x.IdEstudiante,
                        principalTable: "Estudiantes",
                        principalColumn: "IdEstudiante",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Preceptores",
                columns: table => new
                {
                    IdPreceptor = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    Apellido = table.Column<string>(type: "text", nullable: false),
                    Documento = table.Column<string>(type: "text", nullable: false),
                    IdCurso = table.Column<Guid>(type: "uuid", nullable: false),
                    CursoIdCurso = table.Column<Guid>(type: "uuid", nullable: false),
                    IdUsuario = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Preceptores", x => x.IdPreceptor);
                    table.ForeignKey(
                        name: "FK_Preceptores_Cursos_CursoIdCurso",
                        column: x => x.CursoIdCurso,
                        principalTable: "Cursos",
                        principalColumn: "IdCurso",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Preceptores_Usuarios_IdUsuario",
                        column: x => x.IdUsuario,
                        principalTable: "Usuarios",
                        principalColumn: "IdUsuario",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RetirosAnticipados",
                columns: table => new
                {
                    IdRetiro = table.Column<Guid>(type: "uuid", nullable: false),
                    HorarioRetiro = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConReingreso = table.Column<bool>(type: "boolean", nullable: false),
                    HorarioReingreso = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IdEstudiante = table.Column<Guid>(type: "uuid", nullable: false),
                    EstudianteIdEstudiante = table.Column<Guid>(type: "uuid", nullable: false),
                    IdTutor = table.Column<Guid>(type: "uuid", nullable: false),
                    TutorIdTutor = table.Column<Guid>(type: "uuid", nullable: false),
                    IdAsistencia = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetirosAnticipados", x => x.IdRetiro);
                    table.ForeignKey(
                        name: "FK_RetirosAnticipados_Asistencias_IdAsistencia",
                        column: x => x.IdAsistencia,
                        principalTable: "Asistencias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RetirosAnticipados_Estudiantes_EstudianteIdEstudiante",
                        column: x => x.EstudianteIdEstudiante,
                        principalTable: "Estudiantes",
                        principalColumn: "IdEstudiante",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RetirosAnticipados_Tutores_TutorIdTutor",
                        column: x => x.TutorIdTutor,
                        principalTable: "Tutores",
                        principalColumn: "IdTutor",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EspaciosCurriculares",
                columns: table => new
                {
                    IdEC = table.Column<Guid>(type: "uuid", nullable: false),
                    IdCurso = table.Column<Guid>(type: "uuid", nullable: false),
                    IdDocente = table.Column<Guid>(type: "uuid", nullable: false),
                    IdCurricula = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EspaciosCurriculares", x => x.IdEC);
                    table.ForeignKey(
                        name: "FK_EspaciosCurriculares_Curriculas_IdCurricula",
                        column: x => x.IdCurricula,
                        principalTable: "Curriculas",
                        principalColumn: "IdCurricula",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EspaciosCurriculares_Cursos_IdCurso",
                        column: x => x.IdCurso,
                        principalTable: "Cursos",
                        principalColumn: "IdCurso",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EspaciosCurriculares_Docentes_IdDocente",
                        column: x => x.IdDocente,
                        principalTable: "Docentes",
                        principalColumn: "IdDocente",
                        onDelete: ReferentialAction.Restrict);
                });

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

            migrationBuilder.CreateTable(
                name: "Horarios",
                columns: table => new
                {
                    IdHorario = table.Column<Guid>(type: "uuid", nullable: false),
                    DíaSemana = table.Column<int>(type: "integer", nullable: false),
                    HorarioEntrada = table.Column<TimeSpan>(type: "interval", nullable: false),
                    HorarioSalida = table.Column<TimeSpan>(type: "interval", nullable: false),
                    IdCurso = table.Column<Guid>(type: "uuid", nullable: false),
                    IdEC = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Horarios", x => x.IdHorario);
                    table.ForeignKey(
                        name: "FK_Horarios_Cursos_IdCurso",
                        column: x => x.IdCurso,
                        principalTable: "Cursos",
                        principalColumn: "IdCurso",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Horarios_EspaciosCurriculares_IdEC",
                        column: x => x.IdEC,
                        principalTable: "EspaciosCurriculares",
                        principalColumn: "IdEC",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AsistenciasPorEspacio",
                columns: table => new
                {
                    IdAsistenciaEspacio = table.Column<Guid>(type: "uuid", nullable: false),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    IdEstudiante = table.Column<Guid>(type: "uuid", nullable: false),
                    IdClaseDictada = table.Column<Guid>(type: "uuid", nullable: false),
                    Presente = table.Column<bool>(type: "boolean", nullable: false),
                    Motivo = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AsistenciasPorEspacio", x => x.IdAsistenciaEspacio);
                    table.ForeignKey(
                        name: "FK_AsistenciasPorEspacio_ClasesDictadas_IdClaseDictada",
                        column: x => x.IdClaseDictada,
                        principalTable: "ClasesDictadas",
                        principalColumn: "IdClaseDictada",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AsistenciasPorEspacio_Estudiantes_IdEstudiante",
                        column: x => x.IdEstudiante,
                        principalTable: "Estudiantes",
                        principalColumn: "IdEstudiante",
                        onDelete: ReferentialAction.Restrict);
                });

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
                name: "IX_Asistencias_TipoManianaId",
                table: "Asistencias",
                column: "TipoManianaId");

            migrationBuilder.CreateIndex(
                name: "IX_Asistencias_TipoTardeId",
                table: "Asistencias",
                column: "TipoTardeId");

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
                name: "IX_ClasesDictadas_IdEC_Fecha",
                table: "ClasesDictadas",
                columns: new[] { "IdEC", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_CredencialesQR_EstudianteIdEstudiante",
                table: "CredencialesQR",
                column: "EstudianteIdEstudiante");

            migrationBuilder.CreateIndex(
                name: "IX_Cursos_IdAnio",
                table: "Cursos",
                column: "IdAnio");

            migrationBuilder.CreateIndex(
                name: "IX_Cursos_IdDivision",
                table: "Cursos",
                column: "IdDivision");

            migrationBuilder.CreateIndex(
                name: "IX_DetallesCursado_IdCurso",
                table: "DetallesCursado",
                column: "IdCurso");

            migrationBuilder.CreateIndex(
                name: "IX_DetallesCursado_IdEstudiante",
                table: "DetallesCursado",
                column: "IdEstudiante");

            migrationBuilder.CreateIndex(
                name: "IX_Docentes_IdUsuario",
                table: "Docentes",
                column: "IdUsuario",
                unique: true);

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
                name: "IX_Horarios_IdCurso",
                table: "Horarios",
                column: "IdCurso");

            migrationBuilder.CreateIndex(
                name: "IX_Horarios_IdEC",
                table: "Horarios",
                column: "IdEC");

            migrationBuilder.CreateIndex(
                name: "IX_Preceptores_CursoIdCurso",
                table: "Preceptores",
                column: "CursoIdCurso");

            migrationBuilder.CreateIndex(
                name: "IX_Preceptores_IdUsuario",
                table: "Preceptores",
                column: "IdUsuario",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RetirosAnticipados_EstudianteIdEstudiante",
                table: "RetirosAnticipados",
                column: "EstudianteIdEstudiante");

            migrationBuilder.CreateIndex(
                name: "IX_RetirosAnticipados_IdAsistencia",
                table: "RetirosAnticipados",
                column: "IdAsistencia",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RetirosAnticipados_TutorIdTutor",
                table: "RetirosAnticipados",
                column: "TutorIdTutor");

            migrationBuilder.CreateIndex(
                name: "IX_TutorEstudiante_IdEstudiante",
                table: "TutorEstudiante",
                column: "IdEstudiante");

            migrationBuilder.CreateIndex(
                name: "IX_UsuariosRoles_IdRol",
                table: "UsuariosRoles",
                column: "IdRol");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AsistenciasPorEspacio");

            migrationBuilder.DropTable(
                name: "CredencialesQR");

            migrationBuilder.DropTable(
                name: "DetallesCursado");

            migrationBuilder.DropTable(
                name: "Horarios");

            migrationBuilder.DropTable(
                name: "Preceptores");

            migrationBuilder.DropTable(
                name: "RetirosAnticipados");

            migrationBuilder.DropTable(
                name: "TutorEstudiante");

            migrationBuilder.DropTable(
                name: "UsuariosRoles");

            migrationBuilder.DropTable(
                name: "ClasesDictadas");

            migrationBuilder.DropTable(
                name: "Asistencias");

            migrationBuilder.DropTable(
                name: "Tutores");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "EspaciosCurriculares");

            migrationBuilder.DropTable(
                name: "Estudiantes");

            migrationBuilder.DropTable(
                name: "TiposAsistencia");

            migrationBuilder.DropTable(
                name: "Curriculas");

            migrationBuilder.DropTable(
                name: "Cursos");

            migrationBuilder.DropTable(
                name: "Docentes");

            migrationBuilder.DropTable(
                name: "Anios");

            migrationBuilder.DropTable(
                name: "Divisiones");

            migrationBuilder.DropTable(
                name: "Usuarios");
        }
    }
}
