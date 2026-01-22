using System.ComponentModel.DataAnnotations;

namespace RepoDB.Entities
{
    public class Usuario
    {
        [Key]
        public Guid IdUsuario { get; set; }

        public string Mail { get; set; } = null!;
        public string Contraseña { get; set; } = null!;
        public bool Activo { get; set; }
        public bool Verificado { get; set; }
        public DateTime FechaCreacion { get; set; }

        // Roles (permisos)
        public ICollection<UsuarioRol> UsuarioRoles { get; set; } 
            = new List<UsuarioRol>();

        // Pueden ser o no x roles.
        public Docente? Docente { get; set; }   
        public Preceptor? Preceptor { get; set; }
    }

}
