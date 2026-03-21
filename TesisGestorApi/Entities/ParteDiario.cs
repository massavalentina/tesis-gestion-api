using System.ComponentModel.DataAnnotations;

namespace RepoDB.Entities
{
    public class ParteDiario
    {
        [Key]
        public Guid IdParte { get; set; }

        public Guid IdCurso { get; set; }
        public Curso Curso { get; set; } = null!;

        public DateOnly Fecha { get; set; }

        public ICollection<ComentarioParte> Comentarios { get; set; } = new List<ComentarioParte>();
    }
}
