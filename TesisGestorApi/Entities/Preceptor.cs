using System.ComponentModel.DataAnnotations;

namespace TesisGestorApi.Entities
{
    public class Preceptor
    {
        [Key]
        public Guid IdPreceptor { get; set; }

        public bool EsDelegado { get; set; } = false;

        public Guid IdUsuario { get; set; }
        public Usuario Usuario { get; set; } = null!;

        public ICollection<Curso> Cursos { get; set; }
            = new List<Curso>();

        public ICollection<PreceptorCurso> PreceptoresCursos { get; set; }
            = new List<PreceptorCurso>();
    }
}
