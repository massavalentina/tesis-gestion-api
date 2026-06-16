using Microsoft.EntityFrameworkCore;
using RepoDB.Entities;
using TesisGestorApi.Entities;

namespace TesisGestorApi.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // ===== Académico base =====
        public DbSet<Anio> Anios { get; set; }
        public DbSet<Division> Divisiones { get; set; }
        public DbSet<Curso> Cursos { get; set; }
        public DbSet<Horario> Horarios { get; set; }

        // ===== Personas =====
        public DbSet<Estudiante> Estudiantes { get; set; }
        public DbSet<Tutor> Tutores { get; set; }
        public DbSet<Docente> Docentes { get; set; }
        public DbSet<Preceptor> Preceptores { get; set; }

        // ===== Cursado =====
        public DbSet<DetalleCursado> DetallesCursado { get; set; }
        public DbSet<EspacioCurricular> EspaciosCurriculares { get; set; }
        public DbSet<Curricula> Curriculas { get; set; }

        // ===== Asignaciones =====
        public DbSet<DocenteEspacioCurricular> DocentesEspaciosCurriculares { get; set; }
        public DbSet<PreceptorCurso> PreceptoresCursos { get; set; }

        // ===== Asistencias =====
        public DbSet<Asistencia> Asistencias { get; set; }
        public DbSet<RetiroAnticipado> RetirosAnticipados { get; set; }
        public DbSet<AsistenciaPorEspacio> AsistenciasPorEspacio { get; set; }
        public DbSet<TipoAsistencia> TiposAsistencia { get; set; }
        public DbSet<ClaseDictada> ClasesDictadas { get; set; }
        public DbSet<AsistenciaResumenAnual> AsistenciasResumenAnual { get; set; }
        public DbSet<AsistenciaUmbralNotificacion> AsistenciasUmbralNotificacion { get; set; }
        public DbSet<AuditoriaAsistenciaEC> AuditoriasAsistenciaEC { get; set; }

        // ===== Parte Diario =====
        public DbSet<ParteDiario> PartesDiarios { get; set; }
        public DbSet<ComentarioParte> ComentariosParte { get; set; }

        // ===== Seguridad / Otros =====
        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }
        public DbSet<PasswordResetToken> PasswordResetTokens { get; set; }
        public DbSet<CredencialQR> CredencialesQR { get; set; }
        public DbSet<Rol> Roles { get; set; }
        public DbSet<UsuarioRol> UsuariosRoles { get; set; }

        // ===== Permisos =====
        public DbSet<Permiso> Permisos { get; set; }
        public DbSet<RolPermiso> RolPermisos { get; set; }

        // ===== Programas =====
        public DbSet<Programa> Programas { get; set; }
        public DbSet<ObjetivoPrograma> ObjetivosPrograma { get; set; }
        public DbSet<Unidad> Unidades { get; set; }
        public DbSet<Tema> Temas { get; set; }


        // ===== Planificaciones =====
        public DbSet<BloquePrograma> BloquesProgramas { get; set; }
        public DbSet<Planificacion> Planificaciones { get; set; }
        public DbSet<ClaseBloquePrograma> ClasesBloquesProgramas { get; set; }

        // ===== Calificaciones =====
        public DbSet<InstanciaEvaluativa> InstanciasEvaluativas { get; set; }
        public DbSet<ArchivoIE> ArchivosIE { get; set; }
        public DbSet<Calificacion> Calificaciones { get; set; }
        public DbSet<AuditoriaCalificacionSesion> AuditoriasCalificacionesSesiones { get; set; }
        public DbSet<AuditoriaCalificacionDetalle> AuditoriasCalificacionesDetalles { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            /// Seguridad y usuarios

            modelBuilder.Entity<UsuarioRol>()
                .HasKey(ur => new { ur.IdUsuario, ur.IdRol });

            modelBuilder.Entity<UsuarioRol>()
                .HasOne(ur => ur.Usuario)
                .WithMany(u => u.UsuarioRoles)
                .HasForeignKey(ur => ur.IdUsuario)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UsuarioRol>()
                .HasOne(ur => ur.Rol)
                .WithMany(r => r.UsuarioRoles)
                .HasForeignKey(ur => ur.IdRol)
                .OnDelete(DeleteBehavior.Cascade);

modelBuilder.Entity<RolPermiso>(entity =>
{
    entity.HasOne(rp => rp.Rol)
          .WithMany(r => r.RolPermisos)
          .HasForeignKey(rp => rp.IdRol)
          .OnDelete(DeleteBehavior.Cascade);

    entity.HasOne(rp => rp.Permiso)
          .WithMany(p => p.RolPermisos)
          .HasForeignKey(rp => rp.IdPermiso)
          .OnDelete(DeleteBehavior.Cascade);

    entity.HasIndex(rp => new { rp.IdRol, rp.IdPermiso }).IsUnique();
});

modelBuilder.Entity<Usuario>()
    .HasIndex(u => u.Email).IsUnique();

modelBuilder.Entity<Usuario>()
    .HasIndex(u => u.Documento).IsUnique();

modelBuilder.Entity<RefreshToken>()
    .HasOne(rt => rt.Usuario)
    .WithMany()
    .HasForeignKey(rt => rt.IdUsuario)
    .OnDelete(DeleteBehavior.Cascade);

modelBuilder.Entity<RefreshToken>()
    .HasIndex(rt => rt.Token)
    .IsUnique();

            modelBuilder.Entity<PasswordResetToken>()
                .HasOne(prt => prt.Usuario)
                .WithMany()
                .HasForeignKey(prt => prt.IdUsuario)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PasswordResetToken>()
                .HasIndex(prt => prt.Token)
                .IsUnique();

            modelBuilder.Entity<Docente>()
                .HasOne(d => d.Usuario)
                .WithOne(u => u.Docente)
                .HasForeignKey<Docente>(d => d.IdUsuario)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Preceptor>()
                .HasOne(p => p.Usuario)
                .WithOne(u => u.Preceptor)
                .HasForeignKey<Preceptor>(p => p.IdUsuario)
                .OnDelete(DeleteBehavior.Cascade);

            /// Estudiantes y Tutores

            modelBuilder.Entity<TutorEstudiante>()
                .HasKey(te => new { te.IdTutor, te.IdEstudiante });

            modelBuilder.Entity<TutorEstudiante>()
                .HasOne(te => te.Tutor)
                .WithMany(t => t.TutorEstudiantes)
                .HasForeignKey(te => te.IdTutor);

            modelBuilder.Entity<TutorEstudiante>()
                .HasOne(te => te.Estudiante)
                .WithMany(e => e.TutorEstudiantes)
                .HasForeignKey(te => te.IdEstudiante);

            /// Estructura Académica

            modelBuilder.Entity<Curso>(entity =>
            {
                entity.HasOne(c => c.Anio)
                      .WithMany(a => a.Cursos)
                      .HasForeignKey(c => c.IdAnio)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(c => c.Division)
                      .WithMany(d => d.Cursos)
                      .HasForeignKey(c => c.IdDivision)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<DetalleCursado>(entity =>
            {
                entity.HasOne(d => d.Estudiante)
                      .WithMany(e => e.DetallesCursado)
                      .HasForeignKey(d => d.IdEstudiante)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(d => d.Curso)
                      .WithMany(c => c.DetallesCursado)
                      .HasForeignKey(d => d.IdCurso)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<EspacioCurricular>(entity =>
            {
                entity.HasOne(e => e.Curso)
                      .WithMany(c => c.EspaciosCurriculares)
                      .HasForeignKey(e => e.IdCurso)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Curricula)
                      .WithMany(c => c.EspaciosCurriculares)
                      .HasForeignKey(e => e.IdCurricula)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Docente)
                      .WithMany(d => d.EspaciosCurriculares)
                      .HasForeignKey(e => e.IdDocente)
                      .IsRequired(false)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            // Preceptor 1:N Curso — el FK nullable vive en Curso
            modelBuilder.Entity<Curso>()
                .HasOne(c => c.Preceptor)
                .WithMany(p => p.Cursos)
                .HasForeignKey(c => c.IdPreceptor)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            // Historial de asignaciones docente ↔ EC
            modelBuilder.Entity<DocenteEspacioCurricular>(entity =>
            {
                entity.HasOne(d => d.Docente)
                      .WithMany(doc => doc.DocentesEspaciosCurriculares)
                      .HasForeignKey(d => d.IdDocente)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(d => d.EspacioCurricular)
                      .WithMany(ec => ec.DocentesEspaciosCurriculares)
                      .HasForeignKey(d => d.IdEC)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Historial de asignaciones preceptor ↔ Curso
            modelBuilder.Entity<PreceptorCurso>(entity =>
            {
                entity.HasOne(p => p.Preceptor)
                      .WithMany(pr => pr.PreceptoresCursos)
                      .HasForeignKey(p => p.IdPreceptor)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(p => p.Curso)
                      .WithMany(c => c.PreceptoresCursos)
                      .HasForeignKey(p => p.IdCurso)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Horario>(entity =>
            {
                entity.HasOne(h => h.EspacioCurricular)
                      .WithMany(ec => ec.Horarios)
                      .HasForeignKey(h => h.IdEC)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(h => h.Curso)
                      .WithMany(c => c.Horarios)
                      .HasForeignKey(h => h.IdCurso)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            /// Asistencia

            modelBuilder.Entity<ClaseDictada>(entity =>
            {
                entity.HasOne(c => c.EspacioCurricular)
                      .WithMany(ec => ec.ClasesDictadas)
                      .HasForeignKey(c => c.IdEC)
                      .OnDelete(DeleteBehavior.Cascade); // Si se borra el espacio curricular, se borran las clases dictadas asociadas a este.

                // Horario 1:N Clases Dictadas — cada slot de horario tiene su propio registro
                entity.HasOne(c => c.Horario)
                      .WithMany(h => h.ClasesDictadas)
                      .HasForeignKey(c => c.IdHorario)
                      .OnDelete(DeleteBehavior.Restrict); // No borrar el horario si tiene clases dictadas.

                // Un slot de horario solo puede tener un registro de clase dictada por día
                entity.HasIndex(c => new { c.IdHorario, c.Fecha }).IsUnique();

                entity.HasIndex(c => new { c.IdEC, c.Fecha });
            });

            modelBuilder.Entity<AsistenciaPorEspacio>(entity =>
            {
                entity.HasOne(ae => ae.ClaseDictada)
                      .WithMany(cd => cd.Asistencias)
                      .HasForeignKey(ae => ae.IdClaseDictada)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(ae => ae.Estudiante)
                      .WithMany(e => e.AsistenciasPorEspacio)
                      .HasForeignKey(ae => ae.IdEstudiante)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(ae => new { ae.IdClaseDictada, ae.IdEstudiante }).IsUnique();
            });

            modelBuilder.Entity<Asistencia>(entity =>
            {
                entity.HasOne(a => a.Estudiante)
                      .WithMany()
                      .HasForeignKey(a => a.EstudianteId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(a => new { a.EstudianteId, a.Fecha }).IsUnique();
            });

            // RetiroAnticipado → Asistencia (N:1 — máx. uno por (Asistencia, Turno) vía unique index)
            modelBuilder.Entity<RetiroAnticipado>()
                .HasOne(r => r.Asistencia)
                .WithMany()
                .HasForeignKey(r => r.IdAsistencia)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<RetiroAnticipado>()
                .HasIndex(r => new { r.IdAsistencia, r.Turno })
                .IsUnique();

            // RetiroAnticipado → Tutor (nullable, SetNull on delete)
            modelBuilder.Entity<RetiroAnticipado>()
                .HasOne(r => r.Tutor)
                .WithMany(t => t.RetirosAnticipados)
                .HasForeignKey(r => r.IdTutor)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            // RetiroAnticipado → Estudiante (Restrict)
            modelBuilder.Entity<RetiroAnticipado>()
                .HasOne(r => r.Estudiante)
                .WithMany()
                .HasForeignKey(r => r.IdEstudiante)
                .OnDelete(DeleteBehavior.Restrict);

            // Parte Diario
            modelBuilder.Entity<ParteDiario>(entity =>
            {
                entity.HasOne(p => p.Curso)
                      .WithMany()
                      .HasForeignKey(p => p.IdCurso)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(p => new { p.IdCurso, p.Fecha }).IsUnique();
            });

            // Comentario Parte
            modelBuilder.Entity<ComentarioParte>(entity =>
            {
                entity.HasOne(c => c.ParteDiario)
                      .WithMany(p => p.Comentarios)
                      .HasForeignKey(c => c.IdParte)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Auditoría de asistencia por espacio curricular
            modelBuilder.Entity<AuditoriaAsistenciaEC>(entity =>
            {
                entity.HasOne(a => a.Estudiante)
                      .WithMany()
                      .HasForeignKey(a => a.IdEstudiante)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(a => a.ClaseDictada)
                      .WithMany()
                      .HasForeignKey(a => a.IdClaseDictada)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(a => a.Usuario)
                      .WithMany()
                      .HasForeignKey(a => a.IdUsuario)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(a => new { a.IdEstudiante, a.IdClaseDictada, a.FechaRegistro });
            });

            /// Umbrales de asistencia

            modelBuilder.Entity<AsistenciaResumenAnual>(entity =>
            {
                entity.ToTable("AsistenciaResumenAnual");

                entity.HasOne(r => r.Estudiante)
                      .WithMany()
                      .HasForeignKey(r => r.IdEstudiante)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(r => new { r.IdEstudiante, r.AnioLectivo })
                      .IsUnique();
            });

            modelBuilder.Entity<AsistenciaUmbralNotificacion>(entity =>
            {
                entity.ToTable("AsistenciaUmbralNotificacion");

                entity.HasOne(n => n.Estudiante)
                      .WithMany()
                      .HasForeignKey(n => n.IdEstudiante)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(n => new { n.IdEstudiante, n.AnioLectivo, n.Umbral })
                      .IsUnique();
            });

            /// Programas

            modelBuilder.Entity<Programa>(entity =>
            {
                entity.HasOne(p => p.Docente)
                      .WithMany()
                      .HasForeignKey(p => p.IdDocente)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(p => p.Curso)
                      .WithMany()
                      .HasForeignKey(p => p.IdCurso)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(p => p.EspacioCurricular)
                      .WithMany()
                      .HasForeignKey(p => p.IdEC)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(p => new { p.IdEC, p.AnioLectivo }).IsUnique();
            });

            modelBuilder.Entity<ObjetivoPrograma>(entity =>
            {
                entity.HasOne(o => o.Programa)
                      .WithMany(p => p.Objetivos)
                      .HasForeignKey(o => o.IdPrograma)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Unidad>(entity =>
            {
                entity.HasOne(u => u.Programa)
                      .WithMany(p => p.Unidades)
                      .HasForeignKey(u => u.IdPrograma)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Tema>(entity =>
            {
                entity.HasOne(t => t.Unidad)
                      .WithMany(u => u.Temas)
                      .HasForeignKey(t => t.IdUnidad)
                      .OnDelete(DeleteBehavior.Cascade);
            });


            /// Planificaciones

            modelBuilder.Entity<BloquePrograma>(entity =>
            {
                entity.HasOne(b => b.Programa)
                      .WithMany()
                      .HasForeignKey(b => b.IdPrograma)
                      .OnDelete(DeleteBehavior.Cascade);

                // NoAction para evitar múltiples rutas de cascada desde Programa→Unidad→BloquePrograma
                entity.HasOne(b => b.Unidad)
                      .WithMany()
                      .HasForeignKey(b => b.IdUnidad)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(b => b.Tema)
                      .WithMany()
                      .HasForeignKey(b => b.IdTema)
                      .IsRequired(false)
                      .OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<Planificacion>(entity =>
            {
                entity.HasOne(p => p.Docente)
                      .WithMany()
                      .HasForeignKey(p => p.IdDocente)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<ClaseBloquePrograma>(entity =>
            {
                entity.HasKey(cb => new { cb.IdClasePlanificacion, cb.IdBloquePrograma });

                entity.HasOne(cb => cb.Planificacion)
                      .WithMany(p => p.ClasesBloquePrograma)
                      .HasForeignKey(cb => cb.IdClasePlanificacion)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(cb => cb.BloquePrograma)
                      .WithMany(b => b.ClasesBloquePrograma)
                      .HasForeignKey(cb => cb.IdBloquePrograma)
                      .OnDelete(DeleteBehavior.Cascade);

            /// Calificaciones

            modelBuilder.Entity<InstanciaEvaluativa>(entity =>
            {
                entity.ToTable("InstanciaEvaluativa");

                entity.HasOne(i => i.EspacioCurricular)
                      .WithMany(ec => ec.InstanciasEvaluativas)
                      .HasForeignKey(i => i.IdEC)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(i => new { i.IdEC, i.Nro })
                      .IsUnique();
            });

            modelBuilder.Entity<ArchivoIE>(entity =>
            {
                entity.ToTable("ArchivoIE");

                entity.HasOne(a => a.InstanciaEvaluativa)
                      .WithMany(i => i.Archivos)
                      .HasForeignKey(a => a.IdIE)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(a => a.UsuarioCarga)
                      .WithMany()
                      .HasForeignKey(a => a.IdUsuarioCarga)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(a => a.ArchivoAnterior)
                      .WithMany(a => a.VersionesSiguientes)
                      .HasForeignKey(a => a.IdArchivoIEAnterior)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(a => new { a.IdIE, a.TipoCalificacion, a.FechaCarga });

                entity.HasIndex(a => new { a.IdIE, a.TipoCalificacion })
                      .HasFilter("\"Habilitada\" = TRUE")
                      .IsUnique();
            });

            modelBuilder.Entity<Calificacion>(entity =>
            {
                entity.ToTable("Calificacion");

                entity.HasOne(c => c.InstanciaEvaluativa)
                      .WithMany(i => i.Calificaciones)
                      .HasForeignKey(c => c.IdIE)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(c => c.Estudiante)
                      .WithMany(e => e.Calificaciones)
                      .HasForeignKey(c => c.IdEstudiante)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(c => c.ArchivoIE)
                      .WithMany(a => a.Calificaciones)
                      .HasForeignKey(c => c.IdArchivoIE)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(c => c.UsuarioCarga)
                      .WithMany()
                      .HasForeignKey(c => c.IdUsuarioCarga)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(c => c.CalificacionAnterior)
                      .WithMany(c => c.VersionesSiguientes)
                      .HasForeignKey(c => c.IdCalificacionAnterior)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(c => new { c.IdIE, c.IdEstudiante });
                entity.HasIndex(c => new { c.IdIE, c.TipoCalificacion });

                entity.HasIndex(c => new { c.IdIE, c.IdEstudiante, c.TipoCalificacion })
                      .HasFilter("\"Habilitada\" = TRUE")
                      .IsUnique();
            });

            modelBuilder.Entity<AuditoriaCalificacionSesion>(entity =>
            {
                entity.ToTable("AuditoriaCalificacionSesion");

                entity.HasOne(s => s.EspacioCurricular)
                      .WithMany()
                      .HasForeignKey(s => s.IdEC)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(s => s.Usuario)
                      .WithMany()
                      .HasForeignKey(s => s.IdUsuario)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(s => new { s.IdEC, s.FechaRegistro });
            });

            modelBuilder.Entity<AuditoriaCalificacionDetalle>(entity =>
            {
                entity.ToTable("AuditoriaCalificacionDetalle");

                entity.HasOne(d => d.Sesion)
                      .WithMany(s => s.Detalles)
                      .HasForeignKey(d => d.IdSesionAuditoria)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(d => d.InstanciaEvaluativa)
                      .WithMany()
                      .HasForeignKey(d => d.IdIE)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(d => d.Estudiante)
                      .WithMany()
                      .HasForeignKey(d => d.IdEstudiante)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(d => d.CalificacionAnterior)
                      .WithMany()
                      .HasForeignKey(d => d.IdCalificacionAnterior)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(d => d.CalificacionNueva)
                      .WithMany()
                      .HasForeignKey(d => d.IdCalificacionNueva)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(d => d.IdSesionAuditoria);
                entity.HasIndex(d => new { d.IdIE, d.IdEstudiante });

            });
        }
    }
}
