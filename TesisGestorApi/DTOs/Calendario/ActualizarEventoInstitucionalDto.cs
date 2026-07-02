using System.ComponentModel.DataAnnotations;

namespace TesisGestorApi.DTOs.Calendario
{
    public class ActualizarEventoInstitucionalDto
    {
        [Required, MaxLength(300)]
        public string Titulo { get; set; } = null!;

        [MaxLength(2000)]
        public string? Descripcion { get; set; }

        [Required]
        public int TipoEvento { get; set; }

        [Required]
        public string FechaInicio { get; set; } = null!;

        [Required]
        public string FechaFin { get; set; } = null!;

        public bool ContabilizaAsistencia { get; set; } = true;
        public bool CambioActividad { get; set; }

        [MaxLength(2000)]
        public string? ComentarioCambioActividad { get; set; }

        public List<Guid>? CursoIds { get; set; }
    }
}
