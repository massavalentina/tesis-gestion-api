using System.ComponentModel.DataAnnotations;

namespace TesisGestorApi.Entities
{
    public class Permiso
    {
        [Key]
        public Guid IdPermiso { get; set; }

        public string Modulo { get; set; } = null!;
        public string Accion { get; set; } = null!;       // "Lectura" | "Escritura"
        public string Codigo { get; set; } = null!;       // ej: ASISTENCIA_MANUAL_RW
        public string Nombre { get; set; } = null!;
        public string Descripcion { get; set; } = null!;

        public ICollection<RolPermiso> RolPermisos { get; set; } = new List<RolPermiso>();
    }
}
