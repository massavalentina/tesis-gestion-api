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

        // ===== Asistencias =====
        public DbSet<Asistencia> Asistencias { get; set; }
        public DbSet<RetiroAnticipado> RetirosAnticipados { get; set; }
        public DbSet<AsistenciaPorEspacio> AsistenciasPorEspacio { get; set; }
        public DbSet<TipoAsistencia> TiposAsistencia { get; set; }
        public DbSet<ClaseDictada> ClasesDictadas { get; set; }
        public DbSet<AsistenciaResumenAnual> AsistenciasResumenAnual { get; set; }
        public DbSet<AsistenciaUmbralNotificacion> AsistenciasUmbralNotificacion { get; set; }

        // ===== Parte Diario =====
        public DbSet<ParteDiario> PartesDiarios { get; set; }
        public DbSet<ComentarioParte> ComentariosParte { get; set; }

        // ===== Seguridad / Otros =====
        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<CredencialQR> CredencialesQR { get; set; }
        public DbSet<Rol> Roles { get; set; }
        public DbSet<UsuarioRol> UsuariosRoles { get; set; }

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
                      .OnDelete(DeleteBehavior.Restrict);
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
        }
    }
}
