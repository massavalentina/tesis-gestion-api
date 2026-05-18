using System.ComponentModel.DataAnnotations;

namespace TesisGestorApi.Entities
{
    public class Curso
    {
        [Key]
        public Guid IdCurso { get; set; }

        public string Codigo { get; set; } = null!;
        public bool Estado { get; set; }
        public DateTime AñoLectivo { get; set; }

        public Guid IdAnio { get; set; }
        public Anio Anio { get; set; } = null!;

        public Guid IdDivision { get; set; }
        public Division Division { get; set; } = null!;

        // Preceptor asignado (nullable)
        public Guid? IdPreceptor { get; set; }
        public Preceptor? Preceptor { get; set; }

        public ICollection<DetalleCursado> DetallesCursado { get; set; }
            = new List<DetalleCursado>();

        public ICollection<EspacioCurricular> EspaciosCurriculares { get; set; }
            = new List<EspacioCurricular>();

        public ICollection<Horario> Horarios { get; set; }
            = new List<Horario>();

        public ICollection<PreceptorCurso> PreceptoresCursos { get; set; }
            = new List<PreceptorCurso>();
    }
}
