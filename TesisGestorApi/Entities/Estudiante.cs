using System.ComponentModel.DataAnnotations;

namespace TesisGestorApi.Entities
{
    public class Estudiante
    {
        [Key]
        public Guid IdEstudiante { get; set; }

        public string Nombre { get; set; } = null!;
        public string Apellido { get; set; } = null!;
        public string Documento { get; set; } = null!;
        public DateTime FechaNacimiento { get; set; }
        public string? Domicilio { get; set; }
        public string? FotoEstudiante { get; set; }
        public Sexo Sexo { get; set; }

        public bool TeaGeneral { get; set; }

        // Inscripciones a cursos
        public ICollection<DetalleCursado> DetallesCursado { get; set; }
            = new List<DetalleCursado>();

        // Asistencia general (preceptor)
        public ICollection<Asistencia> Asistencias { get; set; }
            = new List<Asistencia>();

        // Asistencia por espacio curricular
        public ICollection<AsistenciaPorEspacio> AsistenciasPorEspacio { get; set; }
            = new List<AsistenciaPorEspacio>();

        // Calificaciones por instancia evaluativa
        public ICollection<Calificacion> Calificaciones { get; set; }
            = new List<Calificacion>();

        // Relación con tutores
        public ICollection<TutorEstudiante> TutorEstudiantes { get; set; }
            = new List<TutorEstudiante>();

        // Retiros anticipados
        public ICollection<RetiroAnticipado> RetirosAnticipados { get; set; }
            = new List<RetiroAnticipado>();
    }

}
