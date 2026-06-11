using System.ComponentModel.DataAnnotations;

namespace TesisGestorApi.Entities
{
    public class AuditoriaCalificacionDetalle
    {
        [Key]
        public Guid IdDetalleAuditoria { get; set; }

        public Guid IdSesionAuditoria { get; set; }
        public AuditoriaCalificacionSesion Sesion { get; set; } = null!;

        public Guid IdIE { get; set; }
        public InstanciaEvaluativa InstanciaEvaluativa { get; set; } = null!;

        public Guid IdEstudiante { get; set; }
        public Estudiante Estudiante { get; set; } = null!;

        public TipoCalificacion TipoCalificacion { get; set; }

        public int? ValorAnterior { get; set; }
        public int? ValorNuevo { get; set; }

        public Guid? IdCalificacionAnterior { get; set; }
        public Calificacion? CalificacionAnterior { get; set; }

        public Guid IdCalificacionNueva { get; set; }
        public Calificacion CalificacionNueva { get; set; } = null!;
    }
}
