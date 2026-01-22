using System.ComponentModel.DataAnnotations;

namespace RepoDB.Entities
{
    public class Preceptor
    {
        [Key]
        public Guid IdPreceptor { get; set; }

        public string Nombre { get; set; } = null!;
        public string Apellido { get; set; } = null!;
        public string Documento { get; set; } = null!;

        public Guid IdCurso { get; set; }
        public Curso Curso { get; set; } = null!;
        public Guid IdUsuario { get; set; }
        public Usuario Usuario { get; set; }
    }


}
