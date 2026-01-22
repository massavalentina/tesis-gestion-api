using System.ComponentModel.DataAnnotations;

namespace RepoDB.Entities
{
    public class CredencialQR
    {
        [Key]
        public Guid IdQR { get; set; }
        public Guid Codigo { get; set; }
        public DateTime AñoLectivo { get; set; }
        public bool Activo { get; set; }
        public bool Enviado { get; set; }
        public DateTime FechaGeneracion { get; set; }
        public DateTime FechaExpiracion { get; set; }

        public Guid IdEstudiante { get; set; } 
        public Estudiante Estudiante { get; set; } = null!;
    }

}
