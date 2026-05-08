using System.ComponentModel.DataAnnotations;

namespace TesisGestorApi.Entities
{
    public class RolPermiso
    {
        [Key]
        public Guid IdRolPermiso { get; set; }

        public Guid IdRol { get; set; }
        public Rol Rol { get; set; } = null!;

        public Guid IdPermiso { get; set; }
        public Permiso Permiso { get; set; } = null!;
    }
}
