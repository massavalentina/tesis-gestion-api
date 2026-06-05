namespace TesisGestorApi.DTOs
{
    public class AuditoriaAsistenciaECDto
    {
        public Guid   IdAuditoria     { get; set; }
        public int    TipoEvento      { get; set; }
        public string TipoEventoLabel { get; set; } = string.Empty;
        public string NombreMateria   { get; set; } = string.Empty;

        /// <summary>null = sin registro previo al evento.</summary>
        public bool?  EstadoAnterior  { get; set; }
        public bool   EstadoNuevo     { get; set; }

        /// <summary>"HH:mm" — hora de llegada, retiro o del cambio manual.</summary>
        public string HorarioEvento   { get; set; } = string.Empty;

        public DateTime FechaRegistro  { get; set; }
        public string   NombreUsuario  { get; set; } = string.Empty;
        public string   ApellidoUsuario{ get; set; } = string.Empty;
    }
}
