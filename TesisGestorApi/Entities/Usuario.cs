using System.ComponentModel.DataAnnotations;

namespace TesisGestorApi.Entities
{
    public class Usuario
    {
        [Key]
        public Guid IdUsuario { get; set; }

        public string Nombre { get; set; } = null!;
        public string Apellido { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string Documento { get; set; } = null!;
        public string? Telefono { get; set; }
        public string Contraseña { get; set; } = null!;
        public bool Activo { get; set; }
        public DateTime FechaCreacion { get; set; }

        // Control de intentos de login
        public int IntentosFailidos { get; set; } = 0;
        public DateTime? BloqueadoHasta { get; set; }

        // Control de contraseña provisoria
        public bool RequiereCambioContrasena { get; set; } = false;
        public DateTime? FechaVencimientoContrasena { get; set; }
        public DateTime? UltimoLogin { get; set; }

        // Roles
        public ICollection<UsuarioRol> UsuarioRoles { get; set; }
            = new List<UsuarioRol>();

        // Perfil asociado (según rol)
        public Docente? Docente { get; set; }
        public Preceptor? Preceptor { get; set; }
    }
}
