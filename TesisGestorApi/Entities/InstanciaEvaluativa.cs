using System.ComponentModel.DataAnnotations;

namespace TesisGestorApi.Entities
{
    public class InstanciaEvaluativa
    {
        [Key]
        public Guid IdIE { get; set; }

        public Guid IdEC { get; set; }
        public EspacioCurricular EspacioCurricular { get; set; } = null!;

        public int Nro { get; set; }

        public EstadoInstanciaEvaluativa Estado { get; set; } = EstadoInstanciaEvaluativa.Pendiente;

        public DateTime FechaCreacion { get; set; }
        public DateTime FechaModificacion { get; set; }

        public ICollection<ArchivoIE> Archivos { get; set; } = new List<ArchivoIE>();
        public ICollection<Calificacion> Calificaciones { get; set; } = new List<Calificacion>();
    }
}
