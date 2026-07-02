using System.ComponentModel.DataAnnotations;

namespace TesisGestorApi.Entities
{
    public class EventoInstitucional
    {
        [Key]
        public Guid IdEvento { get; set; }

        [Required, MaxLength(300)]
        public string Titulo { get; set; } = null!;

        [MaxLength(2000)]
        public string? Descripcion { get; set; }

        public TipoEventoInstitucional TipoEvento { get; set; }

        public DateOnly FechaInicio { get; set; }
        public DateOnly FechaFin { get; set; }

        public int AnioLectivo { get; set; }

        public bool ContabilizaAsistencia { get; set; } = true;

        public bool CambioActividad { get; set; }

        [MaxLength(2000)]
        public string? ComentarioCambioActividad { get; set; }

        public bool Activo { get; set; } = true;

        public DateTime FechaCreacion { get; set; }
        public DateTime? FechaModificacion { get; set; }

        public Guid IdUsuarioCreacion { get; set; }
        public Usuario UsuarioCreacion { get; set; } = null!;

        public ICollection<EventoInstitucionalCurso> Cursos { get; set; } = new List<EventoInstitucionalCurso>();
        public ICollection<AuditoriaEventoInstitucional> Auditorias { get; set; } = new List<AuditoriaEventoInstitucional>();
    }
}
