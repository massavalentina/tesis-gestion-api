namespace TesisGestorApi.DTOs
{
    public class QrCredentialGenerationRequestDto
    {
        public Guid IdCurso { get; set; }
        public string Alcance { get; set; } = "ACTIVOS";
    }
}
