using System.ComponentModel.DataAnnotations;

namespace TesisGestorApi.Entities
{
    public class DocenteEspacioCurricular
    {
        [Key]
        public Guid IdDocenteEC { get; set; }

        public Guid IdDocente { get; set; }
        public Docente Docente { get; set; } = null!;

        public Guid IdEC { get; set; }
        public EspacioCurricular EspacioCurricular { get; set; } = null!;

        public DateTime FechaDesde { get; set; }
        public DateTime? FechaHasta { get; set; }
        public string? Motivo { get; set; }
    }
}
