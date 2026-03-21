namespace TesisGestorApi.DTOs.ParteDiario
{
    public class AgregarComentarioDto
    {
        public Guid CursoId { get; set; }
        public DateOnly Fecha { get; set; }
        public string Contenido { get; set; } = null!;
        public string Autor { get; set; } = "Preceptor";
    }
}
