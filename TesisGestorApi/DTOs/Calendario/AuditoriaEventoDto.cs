namespace TesisGestorApi.DTOs.Calendario
{
    public class AuditoriaEventoDto
    {
        public Guid IdAuditoria { get; set; }
        public string TipoOperacion { get; set; } = null!;
        public string? ValoresAnteriores { get; set; }
        public string? ValoresNuevos { get; set; }
        public string NombreUsuario { get; set; } = null!;
        public string ApellidoUsuario { get; set; } = null!;
        public DateTime FechaRegistro { get; set; }
    }
}
