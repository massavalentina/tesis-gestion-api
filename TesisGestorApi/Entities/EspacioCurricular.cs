using System.ComponentModel.DataAnnotations;

namespace RepoDB.Entities
{
    public class EspacioCurricular
    {
        [Key]
        public Guid IdEC { get; set; }

        public string Nombre { get; set; } = null!;
        public string Descripcion { get; set; } = null!;
        public string Codigo { get; set; } = null!;
        public string Estado { get; set; } = null!; //Puede ser false, ya que puede ser de anios anteriores
        public bool EsContraturno { get; set; } //Osea pertenece al turno tarde

        public ICollection<Curricula> Curriculas { get; set; } = new List<Curricula>();
    }

}
