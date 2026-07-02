namespace TesisGestorApi.DTOs.Retiro
{
    public class RetiroReporteItemDto
    {
        public Guid     IdRetiro    { get; set; }
        public DateOnly Fecha       { get; set; }

        // Estudiante
        public Guid   EstudianteId  { get; set; }
        public string Nombre        { get; set; } = null!;
        public string Apellido      { get; set; } = null!;
        public string Documento     { get; set; } = null!;
        public string Curso         { get; set; } = null!;

        // Retiro
        public string  Turno                   { get; set; } = null!;
        public string  HorarioRetiro           { get; set; } = null!;
        public bool    ConReingreso            { get; set; }
        public string? HorarioLimiteReingreso  { get; set; }
        public string? HorarioReingreso        { get; set; }
        public string? EtiquetaEstado          { get; set; }
        public string? TipoRetiro              { get; set; }
        public string? Motivo                  { get; set; }
        public string? NombrePreceptor         { get; set; }

        // Responsable
        public Guid?   IdTutor             { get; set; }
        public string? NombreResponsable   { get; set; }
        public string? ApellidoResponsable { get; set; }
        public string? DniResponsable      { get; set; }
        public string? RelacionResponsable { get; set; }
        public string? TelefonoResponsable { get; set; }
        public string? CorreoResponsable   { get; set; }
    }
}
