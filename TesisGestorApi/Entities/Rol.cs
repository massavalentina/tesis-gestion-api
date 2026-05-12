using System.ComponentModel.DataAnnotations;

namespace TesisGestorApi.Entities
{
    public class Rol
    {
        [Key]
        public Guid IdRol { get; set; }

        public string Nombre { get; set; } = null!; // Puede ser Admin, Docente, Preceptor, Equipo Directivo, Secretario


        public ICollection<UsuarioRol> UsuarioRoles { get; set; } //Puede tener varios
            = new List<UsuarioRol>();

        public ICollection<RolPermiso> RolPermisos { get; set; } = new List<RolPermiso>();
    }

}
