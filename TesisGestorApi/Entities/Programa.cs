using System.ComponentModel.DataAnnotations;

namespace TesisGestorApi.Entities
{
    public class Programa
    {
        [Key]
        public Guid IdPrograma { get; set; }

        public Guid IdDocente { get; set; }
        public Docente Docente { get; set; } = null!;

        public Guid IdCurso { get; set; }
        public Curso Curso { get; set; } = null!;

        public Guid IdEC { get; set; }
        public EspacioCurricular EspacioCurricular { get; set; } = null!;

        public int AnioLectivo { get; set; }

        [Required]
        [MaxLength(300)]
        public string Titulo { get; set; } = null!;

        [MaxLength(2000)]
        public string? Descripcion { get; set; }

        public int HorasCatedra { get; set; }

        [MaxLength(500)]
        public string? Url { get; set; }

        public OrigenPrograma Origen { get; set; }
        public EstadoPrograma Estado { get; set; }

        public DateOnly FechaVencimiento { get; set; }
        public DateTime FechaCreacion { get; set; }
        public DateTime FechaUltimaModificacion { get; set; }

        public ICollection<ObjetivoPrograma> Objetivos { get; set; } = new List<ObjetivoPrograma>();
        public ICollection<Unidad> Unidades { get; set; } = new List<Unidad>();
    }
}
