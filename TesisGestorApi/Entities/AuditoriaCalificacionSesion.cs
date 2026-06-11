using System.ComponentModel.DataAnnotations;

namespace TesisGestorApi.Entities
{
    public class AuditoriaCalificacionSesion
    {
        [Key]
        public Guid IdSesionAuditoria { get; set; }

        public Guid IdEC { get; set; }
        public EspacioCurricular EspacioCurricular { get; set; } = null!;

        public Guid IdUsuario { get; set; }
        public Usuario Usuario { get; set; } = null!;

        public OrigenCarga Origen { get; set; } = OrigenCarga.Manual;

        public DateTime FechaRegistro { get; set; }

        public ICollection<AuditoriaCalificacionDetalle> Detalles { get; set; } = new List<AuditoriaCalificacionDetalle>();
    }
}
