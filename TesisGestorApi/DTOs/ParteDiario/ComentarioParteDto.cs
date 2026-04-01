namespace TesisGestorApi.DTOs.ParteDiario
{
    public class ComentarioParteDto
    {
        public Guid     IdComentario { get; set; }
        public DateTime Timestamp    { get; set; }
        public string   Contenido    { get; set; } = null!;
        public string   Tipo         { get; set; } = null!; // "Comentario" | "Evento"
        public string   SubTipo      { get; set; } = null!; // "NOTA" | "ASISTENCIA" | "HORARIO"
        public string?  Titulo       { get; set; }
        public string?  Detalle      { get; set; }
        public string   Autor        { get; set; } = null!;
    }
}
