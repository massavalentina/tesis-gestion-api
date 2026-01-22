using System.ComponentModel.DataAnnotations;

namespace RepoDB.Entities
{
    public class Division
    {
        [Key]
        public Guid IdDivision { get; set; }
        public char Nombre { get; set; }

        public ICollection<Curso> Cursos { get; set; } = new List<Curso>();
    }

}
