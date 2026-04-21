namespace TesisGestorApi.DTOs.Retiro
{
    public class TutorEstudianteDto
    {
        public Guid IdTutor { get; set; }
        public string Nombre { get; set; } = null!;
        public string Apellido { get; set; } = null!;
        public string Documento { get; set; } = null!;
        public string RelacionEstudiante { get; set; } = null!;
        public bool EsPrincipal { get; set; }
        /// <summary>Teléfono (convertido a string desde int en la entidad).</summary>
        public string? Telefono { get; set; }
        public string? Correo { get; set; }
    }
}
