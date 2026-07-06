namespace TesisGestorApi.DTOs
{
    public class UpdateEstudianteDto
    {
        public string Nombre { get; set; } = null!;
        public string Apellido { get; set; } = null!;
        public string Documento { get; set; } = null!;
        public string FechaNacimiento { get; set; } = null!;
        public string? Domicilio { get; set; }
        public string? Sexo { get; set; } // "M" o "F" (null = sin dato)
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

    public class LibretaEspacioDto
    {
        public Guid IdEC { get; set; }
        public string NombreMateria { get; set; } = null!;
        public List<LibretaInstanciaDto> Instancias { get; set; } = new();
    }

    public class LibretaInstanciaDto
    {
        public int Nro { get; set; }
        public int? N { get; set; }
        public int? R1 { get; set; }
        public int? R2 { get; set; }
    }
}
