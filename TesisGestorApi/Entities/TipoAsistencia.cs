using System.ComponentModel.DataAnnotations;

namespace RepoDB.Entities
{
    public class TipoAsistencia
    {
        [Key]
        public Guid IdTipo { get; set; }

        public string Codigo { get; set; } = null!; //Presente, ausente, llegada tarde

        public decimal? ValorAsistenciaMañana { get; set; }  // 1, 0.5, 0.25
        public decimal? ValorAsistenciaTarde { get; set; }   // 1, 0.5, 0.25
    }
}
