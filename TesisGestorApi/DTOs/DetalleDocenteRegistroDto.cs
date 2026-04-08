namespace TesisGestorApi.DTOs
{
    public class DetalleDocenteRegistroDto
    {
        public string Fecha { get; set; } = null!;
        public bool Dictada { get; set; }
        public bool? Presente { get; set; }
        public string? Codigo { get; set; }
        public string? HoraEntrada { get; set; }
    }
}
