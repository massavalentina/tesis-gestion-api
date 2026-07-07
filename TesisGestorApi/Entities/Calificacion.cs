using System.ComponentModel.DataAnnotations;

namespace TesisGestorApi.Entities
{
    public class Calificacion
    {
        [Key]
        public Guid IdCalificacion { get; set; }

        public Guid IdIE { get; set; }
        public InstanciaEvaluativa InstanciaEvaluativa { get; set; } = null!;

        public Guid IdEstudiante { get; set; }
        public Estudiante Estudiante { get; set; } = null!;

        public TipoCalificacion TipoCalificacion { get; set; }

        public Guid IdArchivoIE { get; set; }
        public ArchivoIE ArchivoIE { get; set; } = null!;

        public int? Puntaje { get; set; }

        public bool Habilitada { get; set; } = true;

        public DateTime FechaCarga { get; set; }

        public Guid IdUsuarioCarga { get; set; }
        public Usuario UsuarioCarga { get; set; } = null!;

        public OrigenCarga Origen { get; set; }

        public Guid? IdImportacionCalificaciones { get; set; }
        public ImportacionCalificaciones? ImportacionCalificaciones { get; set; }

        public Guid? IdCalificacionAnterior { get; set; }
        public Calificacion? CalificacionAnterior { get; set; }

        public ICollection<Calificacion> VersionesSiguientes { get; set; } = new List<Calificacion>();
    }
}
