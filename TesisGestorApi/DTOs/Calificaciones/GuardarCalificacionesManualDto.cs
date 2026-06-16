using System.ComponentModel.DataAnnotations;

namespace TesisGestorApi.DTOs.Calificaciones
{
    public class GuardarCalificacionesManualDto
    {
        [Required]
        [MinLength(1)]
        public List<GuardarCalificacionCambioDto> Cambios { get; set; } = new();
    }

    public class GuardarCalificacionCambioDto
    {
        public Guid IdIE { get; set; }
        public Guid IdEstudiante { get; set; }

        [Required]
        public string TipoCalificacion { get; set; } = null!;

        public int? Puntaje { get; set; }
    }

    public class GuardarCalificacionesManualResponseDto
    {
        public int CambiosAplicados { get; set; }
        public List<Guid> InstanciasAfectadas { get; set; } = new();
        public AuditoriaCalificacionSesionDto? SesionAuditoria { get; set; }
    }
}
