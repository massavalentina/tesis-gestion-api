using System.ComponentModel.DataAnnotations;

namespace TesisGestorApi.Entities
{
    public class ImportacionCalificaciones
    {
        [Key]
        public Guid IdImportacionCalificaciones { get; set; }

        public Guid IdEC { get; set; }
        public EspacioCurricular EspacioCurricular { get; set; } = null!;

        public Guid IdCurso { get; set; }
        public Curso Curso { get; set; } = null!;

        public Guid IdDocente { get; set; }
        public Docente Docente { get; set; } = null!;

        public Guid IdUsuario { get; set; }
        public Usuario Usuario { get; set; } = null!;

        public int AnioLectivo { get; set; }

        public EstadoImportacionCalificaciones Estado { get; set; } = EstadoImportacionCalificaciones.Analizada;

        [MaxLength(300)]
        public string NombreArchivoOriginal { get; set; } = null!;

        [MaxLength(100)]
        public string ContentType { get; set; } = "application/pdf";

        public long TamanioArchivoBytes { get; set; }

        [MaxLength(128)]
        public string HashArchivoSha256 { get; set; } = null!;

        [MaxLength(100)]
        public string MotorLectura { get; set; } = "PdfPig";

        public byte[]? ArchivoTemporalContenido { get; set; }

        [MaxLength(2000)]
        public string? RutaArchivoFinal { get; set; }

        public string ResumenAnalisisJson { get; set; } = "{}";
        public string? RevisionJson { get; set; }
        public string? ResumenConfirmacionJson { get; set; }
        public string? ErrorTecnico { get; set; }

        public DateTime FechaCreacion { get; set; }
        public DateTime FechaUltimaActualizacion { get; set; }
        public DateTime? FechaConfirmacion { get; set; }

        public ICollection<Calificacion> Calificaciones { get; set; } = new List<Calificacion>();
        public ICollection<AuditoriaCalificacionSesion> Auditorias { get; set; } = new List<AuditoriaCalificacionSesion>();
    }
}
