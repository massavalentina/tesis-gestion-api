using Microsoft.EntityFrameworkCore;
using RepoDB.Entities;

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

        // ===== Seguridad / Otros =====
        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<CredencialQR> CredencialesQR { get; set; }

        public DbSet<Rol> Roles { get; set; }

        public DbSet<UsuarioRol> UsuariosRoles { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Seguridad y usuarios

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

            // Usuario <-> Docente
            modelBuilder.Entity<Docente>()
                .HasOne(d => d.Usuario)
                .WithOne(u => u.Docente)
                .HasForeignKey<Docente>(d => d.IdUsuario)
                .OnDelete(DeleteBehavior.Cascade); // Composición. Si borro el usuario, borro el docente asociado.

            // Usuario <-> Preceptor
            modelBuilder.Entity<Preceptor>()
                .HasOne(p => p.Usuario)
                .WithOne(u => u.Preceptor)
                .HasForeignKey<Preceptor>(p => p.IdUsuario)
                .OnDelete(DeleteBehavior.Cascade);

            // Estudiantes y Tutores
            // N:M Tutor <-> Estudiante
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

            // Estructura Académica
            // Espacio Curricular concreto - Matemática 1ero A
            modelBuilder.Entity<EspacioCurricular>(entity =>
            {
                // Curso 1:N Espacios Curriculares
                entity.HasOne(e => e.Curso)
                      .WithMany(c => c.EspaciosCurriculares) 
                      .HasForeignKey(e => e.IdCurso)
                      .OnDelete(DeleteBehavior.Cascade); // Composición. Si borro el curso se borran los espacios 

                // Currícula 1:N Espacios Curriculares
                entity.HasOne(e => e.Curricula)
                      .WithMany(c => c.EspaciosCurriculares)
                      .HasForeignKey(e => e.IdCurricula)
                      .OnDelete(DeleteBehavior.Restrict); // Composición. Si borro la currícula se borra el espacio

                // Docente 1:N Espacios Curriculares
                entity.HasOne(e => e.Docente)
                      .WithMany(d => d.EspaciosCurriculares) 
                      .HasForeignKey(e => e.IdDocente)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // Horario Semanal
            modelBuilder.Entity<Horario>(entity =>
            {
                // Horario 1:1 Espacio Curricular
                entity.HasOne(h => h.EspacioCurricular)
                      .WithMany(ec => ec.Horarios)
                      .HasForeignKey(h => h.IdEC)
                      .OnDelete(DeleteBehavior.Cascade);

                // Horario 1:1 Curso
                entity.HasOne(h => h.Curso)
                      .WithMany(c => c.Horarios)
                      .HasForeignKey(h => h.IdCurso)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // Asistencia

            // Clase Dictada
            modelBuilder.Entity<ClaseDictada>(entity =>
            {
                // Espacio Curricular 1:N Clases Dictadas
                entity.HasOne(c => c.EspacioCurricular)
                      .WithMany(ec => ec.ClasesDictadas)
                      .HasForeignKey(c => c.IdEC)
                      .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(c => new { c.IdEC, c.Fecha });
            });

            // Asistencia por Espacio
            modelBuilder.Entity<AsistenciaPorEspacio>(entity =>
            {
                // Clas Dictada 1:N Asistencias por Espacio
                entity.HasOne(ae => ae.ClaseDictada)
                      .WithMany(cd => cd.Asistencias)
                      .HasForeignKey(ae => ae.IdClaseDictada)
                      .OnDelete(DeleteBehavior.Cascade); // Composición. Si borro la clase, se borran sus presentes

                // Estudiante 1:N Asistencias por Espacio
                entity.HasOne(ae => ae.Estudiante)
                      .WithMany(e => e.AsistenciasPorEspacio) 
                      .HasForeignKey(ae => ae.IdEstudiante)
                      .OnDelete(DeleteBehavior.Restrict); // Composición. Si borro al estudiante, se borran las asistencias de este

                // Integridad: Un alumno solo tiene 1 registro por clase dictada
                entity.HasIndex(ae => new { ae.IdClaseDictada, ae.IdEstudiante }).IsUnique();
            });

            // Asistencia General 
            modelBuilder.Entity<Asistencia>(entity =>
            {
                // Estudiante 1:N Asistencias
                entity.HasOne(a => a.Estudiante)
                      .WithMany() 
                      .HasForeignKey(a => a.EstudianteId)
                      .OnDelete(DeleteBehavior.Cascade);

                // Integridad: Un alumno solo tiene un registro por día
                entity.HasIndex(a => new { a.EstudianteId, a.Fecha }).IsUnique();
            });

            // Retiro Anticipado 1:1 
            modelBuilder.Entity<RetiroAnticipado>()
                .HasOne(r => r.Asistencia)
                .WithOne() 
                .HasForeignKey<RetiroAnticipado>(r => r.IdAsistencia)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
