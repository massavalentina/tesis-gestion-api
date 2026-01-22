using System.ComponentModel.DataAnnotations;

namespace RepoDB.Entities
{
    public class Anio
    {
        [Key]
        public Guid IdAnio { get; set; }
        public int Numero { get; set; }

        public ICollection<Curso> Cursos { get; set; } = new List<Curso>();
    }

}
