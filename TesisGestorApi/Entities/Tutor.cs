using System.ComponentModel.DataAnnotations;

namespace RepoDB.Entities
{
    public class Tutor
    {
        [Key]
        public Guid IdTutor { get; set; }

        public string Nombre { get; set; } = null!;
        public string Apellido { get; set; } = null!;
        public string Documento { get; set; } = null!;
        public int Telefono { get; set; }
        public string Correo { get; set; } = null!;
        public string RelacionEstudiante { get; set; } = null!; //Puede ser cualquier valor
        public DateTime FechaNacimiento { get; set; }
        public string Disponibilidad { get; set; } = null!; 
        public bool EsPrincipal { get; set; } //Define si es el primer con quien comunicarse

 
        public ICollection<TutorEstudiante> TutorEstudiantes { get; set; }
        = new List<TutorEstudiante>();

        public ICollection<RetiroAnticipado> RetirosAnticipados { get; set; }
            = new List<RetiroAnticipado>();
    }

}
