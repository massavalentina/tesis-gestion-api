namespace TesisGestorApi.DTOs
{
    public class UpdateEstudianteDto
    {
        public string Nombre { get; set; } = null!;
        public string Apellido { get; set; } = null!;
        public string Documento { get; set; } = null!;
        public string FechaNacimiento { get; set; } = null!;
        public string? Domicilio { get; set; }
    }

    public class UpdateTutorDto
    {
        public string Nombre { get; set; } = null!;
        public string Apellido { get; set; } = null!;
        public string Documento { get; set; } = null!;
        public long Telefono { get; set; }
        public string Correo { get; set; } = null!;
        public string RelacionEstudiante { get; set; } = null!;
        public string? Disponibilidad { get; set; }
        public string? Domicilio { get; set; }
    }

    public class CreateTutorDto
    {
        public string Nombre { get; set; } = null!;
        public string Apellido { get; set; } = null!;
        public string Documento { get; set; } = null!;
        public long Telefono { get; set; }
        public string Correo { get; set; } = null!;
        public string RelacionEstudiante { get; set; } = null!;
        public string? Disponibilidad { get; set; }
        public string? Domicilio { get; set; }
        public string FechaNacimiento { get; set; } = null!;
        public bool EsPrincipal { get; set; }
    }
}
