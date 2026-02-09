namespace TesisGestorApi.DTOs
{
    public class RegistrarAsistenciaDto
    {
        public Guid EstudianteId { get; set; }
        public DateTime Fecha { get; set; }

        // Mañana o Tarde (Para saber qué propiedad tocar)
        public string Turno { get; set; }

        // El Tipo de Asistencia Seleccionado
        public Guid TipoAsistenciaId { get; set; }
    }

    // Response al Front
    public class AsistenciaResponseDto
    {
        public Guid Id { get; set; }
        public decimal ValorTotal { get; set; } // 0, 0.5, 1.0, 1.5
        public string Mensaje { get; set; }
    }
}
