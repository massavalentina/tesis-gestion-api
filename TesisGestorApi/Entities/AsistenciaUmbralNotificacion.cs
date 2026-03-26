
using System.ComponentModel.DataAnnotations;

namespace TesisGestorApi.Entities
{
    public class AsistenciaUmbralNotificacion
    {
        [Key]
        public Guid IdNotif { get; set; }

        public Guid IdEstudiante { get; set; }
        public Estudiante Estudiante { get; set; } = null!;

        public int AnioLectivo { get; set; }
        public int Umbral { get; set; } // 10/15/20/25

        public int CantidadEnviados { get; set; } = 0; // 0..3
        public DateTime ProximoEnvioUtc { get; set; }

        [MaxLength(20)]
        public string Estado { get; set; } = "PENDIENTE"; // PENDIENTE/COMPLETADO

        public DateTime CreadoUtc { get; set; }
        public DateTime? UltimoEnvioUtc { get; set; }
        public string? UltimoError { get; set; }

    }
}
