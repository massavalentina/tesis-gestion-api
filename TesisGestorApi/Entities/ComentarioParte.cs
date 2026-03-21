using System.ComponentModel.DataAnnotations;

namespace RepoDB.Entities
{
    public enum TipoComentarioParte { Comentario = 0, Evento = 1 }

    public class ComentarioParte
    {
        [Key]
        public Guid IdComentario { get; set; }

        public Guid IdParte { get; set; }
        public ParteDiario ParteDiario { get; set; } = null!;

        public DateTime Timestamp { get; set; }
        public string Contenido { get; set; } = null!;
        public TipoComentarioParte Tipo { get; set; }
        public string Autor { get; set; } = null!;
    }
}
