namespace TesisGestorApi.DTOs
{
    public class VerificarManualesEcRequestDto
    {
        public Guid EstudianteId { get; set; }
        public DateOnly Fecha { get; set; }
        public string Turno { get; set; } = null!; // "MANANA" | "TARDE"
    }

    public class VerificarManualesEcResultDto
    {
        public Guid EstudianteId { get; set; }
        public string Turno { get; set; } = null!;
        public bool TieneManuales { get; set; }
    }
}
