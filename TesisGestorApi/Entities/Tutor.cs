using System.ComponentModel.DataAnnotations;

namespace TesisGestorApi.Entities
{
    public class Tutor
    {
        [Key]
        public Guid IdTutor { get; set; }

        public string Nombre { get; set; } = null!;
        public string Apellido { get; set; } = null!;
        public string Documento { get; set; } = null!;
        public long Telefono { get; set; }
        public string Correo { get; set; } = null!;
        public string RelacionEstudiante { get; set; } = null!; //Puede ser cualquier valor
        public DateTime FechaNacimiento { get; set; }
        public string? Domicilio { get; set; }
        public string Disponibilidad { get; set; } = null!;

        // Fecha en que se actualizaron por última vez los datos del tutor.
        // Se usa para detectar tutores desactualizados (más de 6 meses sin cambios).
        public DateTime FechaUltimaActualizacion { get; set; } = DateTime.UtcNow;

        // Fecha en que se envió por última vez una notificación de actualización al tutor.
        // Null si nunca se envió. Permite limitar el reenvío a una vez cada 6 meses,
        // independientemente de si el tutor actualizó o no sus datos.
        public DateTime? FechaUltimaNotificacion { get; set; }


        public ICollection<TutorEstudiante> TutorEstudiantes { get; set; }
        = new List<TutorEstudiante>();

        public ICollection<RetiroAnticipado> RetirosAnticipados { get; set; }
            = new List<RetiroAnticipado>();
    }

}
