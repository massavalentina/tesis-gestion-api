using System.ComponentModel.DataAnnotations;

namespace TesisGestorApi.Entities
{
    public enum TipoEventoAuditoriaEC
    {
        RegistroGeneral = 1,
        Retiro          = 2,
        CambioManual    = 3
    }

    public class AuditoriaAsistenciaEC
    {
        [Key]
        public Guid IdAuditoria { get; set; }

        public Guid IdEstudiante { get; set; }
        public Estudiante Estudiante { get; set; } = null!;

        public Guid IdClaseDictada { get; set; }
        public ClaseDictada ClaseDictada { get; set; } = null!;

        public TipoEventoAuditoriaEC TipoEvento { get; set; }

        /// <summary>null = sin registro previo (ej. primera vez que se registra asistencia para este EC)</summary>
        public bool? EstadoAnterior { get; set; }

        public bool EstadoNuevo { get; set; }

        /// <summary>Hora del evento: hora de llegada, hora de retiro o timestamp del cambio manual.</summary>
        public TimeSpan HorarioEvento { get; set; }

        public Guid IdUsuario { get; set; }
        public Usuario Usuario { get; set; } = null!;

        /// <summary>Timestamp UTC del servidor en el momento del registro.</summary>
        public DateTime FechaRegistro { get; set; }
    }
}
