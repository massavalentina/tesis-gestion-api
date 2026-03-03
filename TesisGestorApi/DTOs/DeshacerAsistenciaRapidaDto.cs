namespace TesisGestorApi.DTOs
{
    public class DeshacerAsistenciaRapidaDto
    {
        public Guid EstudianteId { get; set; }
        public DateOnly Fecha { get; set; }
        public string Turno { get; set; } = "MANANA"; // MANANA | TARDE
    }
}
