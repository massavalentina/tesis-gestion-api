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
                .OnDelete(DeleteBehavior.Cascade); // Composición. Si borro el Usuario, borro sus roles asociados.

            modelBuilder.Entity<UsuarioRol>()
                .HasOne(ur => ur.Rol)
                .WithMany(r => r.UsuarioRoles)
                .HasForeignKey(ur => ur.IdRol)
                .OnDelete(DeleteBehavior.Cascade); // Composición. Si borro el Rol se borran las asociaciones con los usuarios.

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
                .OnDelete(DeleteBehavior.Cascade); // Composición. Si borro el usuario, borro el preceptor asociado.

            /// Estudiantes y Tutores
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

            /// Estructura Académica
            
            // Cursos Concretos - 1ero A 2026
            modelBuilder.Entity<Curso>(entity =>
            {
                // Anio 1:N Cursos
                entity.HasOne(c => c.Anio)
                      .WithMany(a => a.Cursos)
                      .HasForeignKey(c => c.IdAnio) 
                      .OnDelete(DeleteBehavior.Restrict); // No se puede borrar el año si hay cursos asociados a este.

                // División 1:N Cursos
                entity.HasOne(c => c.Division)
                      .WithMany(d => d.Cursos)
                      .HasForeignKey(c => c.IdDivision)
                      .OnDelete(DeleteBehavior.Restrict); // No se puede borrar la división si hay cursos asociados a esta. 
            });

            // Detalles de Cursado - Inscripciones concretas
            modelBuilder.Entity<DetalleCursado>(entity =>
            {
                // Estudiante 1:N Detalles de Cursado
                entity.HasOne(d => d.Estudiante)
                      .WithMany(e => e.DetallesCursado)
                      .HasForeignKey(d => d.IdEstudiante)
                      .OnDelete(DeleteBehavior.Cascade); // Si se borra el Estudiante, se borran sus detalles de cursado.

                // Curso 1:N Detalles de Cursado
                entity.HasOne(d => d.Curso)
                      .WithMany(c => c.DetallesCursado)
                      .HasForeignKey(d => d.IdCurso)    
                      .OnDelete(DeleteBehavior.Cascade); // Si se borra el Curso, se borran los detalles asociados a este Curso. 
            });

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
                      .OnDelete(DeleteBehavior.Restrict); // Agregación. No se puede borrar la Currícula si hay Espacios asociados a esta.

                // Docente 1:N Espacios Curriculares
                entity.HasOne(e => e.Docente)
                      .WithMany(d => d.EspaciosCurriculares) 
                      .HasForeignKey(e => e.IdDocente)
                      .OnDelete(DeleteBehavior.Restrict); // Agregación. No se puede borrar el docente si hay espacios asociados a este.
            });

            // Horario Semanal
            modelBuilder.Entity<Horario>(entity =>
            {
                // Espacio Curricular 1:1 Horario
                entity.HasOne(h => h.EspacioCurricular)
                      .WithMany(ec => ec.Horarios)
                      .HasForeignKey(h => h.IdEC)
                      .OnDelete(DeleteBehavior.Cascade); // Si se borra el espacio curricular, el horario se borra también.

                // Curso 1:1 Horario
                entity.HasOne(h => h.Curso)
                      .WithMany(c => c.Horarios)
                      .HasForeignKey(h => h.IdCurso)
                      .OnDelete(DeleteBehavior.Restrict); // No se puede borrar el curso si hay horarios asociados a este.
            });

            /// Asistencia

            // Clase Dictada
            modelBuilder.Entity<ClaseDictada>(entity =>
            {
                // Espacio Curricular 1:N Clases Dictadas
                entity.HasOne(c => c.EspacioCurricular)
                      .WithMany(ec => ec.ClasesDictadas)
                      .HasForeignKey(c => c.IdEC)
                      .OnDelete(DeleteBehavior.Cascade); // Si se borra el espacio curricular, se borran las clases dictadas asociadas a este.
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
                      .OnDelete(DeleteBehavior.Restrict); // Agregación. No se puede borrar al usuario si tiene asistencias.

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
                      .OnDelete(DeleteBehavior.Cascade); // Si borras al alumno, borras sus asistencias diarias

                // Integridad: Un alumno solo tiene un registro por día
                entity.HasIndex(a => new { a.EstudianteId, a.Fecha }).IsUnique();
            });

            // Asistencias 1:1 Retiro Anticipado
            modelBuilder.Entity<RetiroAnticipado>()
                .HasOne(r => r.Asistencia)
                .WithOne()
                .HasForeignKey<RetiroAnticipado>(r => r.IdAsistencia)
                .OnDelete(DeleteBehavior.Cascade); // Si borro la asistencia, se borra el retiro

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
        }
    }
}
