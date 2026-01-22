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
    public DbSet<Curricula> Curriculas { get; set; }
    public DbSet<EspacioCurricular> EspaciosCurriculares { get; set; }

    // ===== Asistencias =====
    public DbSet<Asistencia> Asistencias { get; set; }
    public DbSet<RetiroAnticipado> RetirosAnticipados { get; set; }
    public DbSet<AsistenciaPorEspacio> AsistenciasPorEspacio { get; set; }
    public DbSet<TipoAsistencia> TiposAsistencia { get; set; }

    // ===== Seguridad / Otros =====
    public DbSet<Usuario> Usuarios { get; set; }
    public DbSet<CredencialQR> CredencialesQR { get; set; }

    public DbSet<Rol> Roles { get; set; }

    public DbSet<UsuarioRol> UsuariosRoles { get; set; }


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ============================
        // Usuario - Rol (N a N)
        // ============================
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

        // ============================
        // Usuario - Docente (1 a 1)
        // Docente depende de Usuario
        // ============================
        modelBuilder.Entity<Docente>()
            .HasOne(d => d.Usuario)
            .WithOne(u => u.Docente)
            .HasForeignKey<Docente>(d => d.IdUsuario)
            .OnDelete(DeleteBehavior.Cascade);

        // ============================
        // Usuario - Preceptor (1 a 1)
        // Preceptor depende de Usuario
        // ============================
        modelBuilder.Entity<Preceptor>()
            .HasOne(p => p.Usuario)
            .WithOne(u => u.Preceptor)
            .HasForeignKey<Preceptor>(p => p.IdUsuario)
            .OnDelete(DeleteBehavior.Cascade);

        // ============================
        // Asistencia - RetiroAnticipado (1 a 1)
        // RetiroAnticipado depende de Asistencia
        // ============================
        modelBuilder.Entity<Asistencia>()
            .HasOne(a => a.RetiroAnticipado)
            .WithOne(r => r.Asistencia)
            .HasForeignKey<RetiroAnticipado>(r => r.IdAsistencia)
            .OnDelete(DeleteBehavior.Cascade);

        // ============================
        // Tutor - Estudiante (N a N)
        // Tabla intermedia: TutorEstudiante
        // La clave primaria es compuesta
        // ============================


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


    }


}
}
