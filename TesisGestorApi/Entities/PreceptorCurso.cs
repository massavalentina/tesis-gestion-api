using System.ComponentModel.DataAnnotations;

namespace TesisGestorApi.Entities
{
    public class PreceptorCurso
    {
        [Key]
        public Guid IdPreceptorCurso { get; set; }

        public Guid IdPreceptor { get; set; }
        public Preceptor Preceptor { get; set; } = null!;

        public Guid IdCurso { get; set; }
        public Curso Curso { get; set; } = null!;

        public DateTime FechaDesde { get; set; }
        public DateTime? FechaHasta { get; set; }
        public string? Motivo { get; set; }
    }
}
