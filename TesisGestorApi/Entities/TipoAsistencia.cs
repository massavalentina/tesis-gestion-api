using System.ComponentModel.DataAnnotations;

namespace RepoDB.Entities
{
    public class TipoAsistencia
    {
        [Key]
        public Guid IdTipo { get; set; }

        public string Codigo { get; set; } = null!; 

        public decimal? ValorAsistenciaMañana { get; set; }  
        public decimal? ValorAsistenciaTarde { get; set; }   
    }
}
