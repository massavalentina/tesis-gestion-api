namespace TesisGestorApi.DTOs.ParteDiario
{
    public class ComentarioParteDto
    {
        public Guid IdComentario { get; set; }
        public DateTime Timestamp { get; set; }
        public string Contenido { get; set; } = null!;
        public string Tipo { get; set; } = null!; // "Comentario" | "Evento"
        public string Autor { get; set; } = null!;
    }
}
