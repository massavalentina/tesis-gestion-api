namespace TesisGestorApi.DTOs
{
    public class QrAlumnoEstadoDto
    {
        public Guid IdEstudiante { get; set; }
        public string NombreCompleto { get; set; } = "";
        public string Dni { get; set; } = "";
        public string Estado { get; set; } = "NO_GENERADO";
    }
}
