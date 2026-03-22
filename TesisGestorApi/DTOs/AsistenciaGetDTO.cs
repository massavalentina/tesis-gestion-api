namespace TesisGestorApi.DTOs
{
    public class AsistenciaGetDTO
    {
        public Guid Id { get; set; }
        public DateOnly Fecha { get; set; }
        public decimal ValorTotal { get; set; }
        public string NombreCompleto { get; set; }
        public string Documento { get; set; }
        // Código del estado general del turno mañana (puede ser el retiro si hubo uno)
        public string CodigoManana { get; set; }
        // Código de la llegada al turno mañana (LLT/LLTE/LLTC/P/A/ANC)
        // Útil para reporting de llegadas tarde independientemente de si hubo retiro.
        public string? CodigoLlegadaManana { get; set; }
        public string CodigoTarde { get; set; }
    }
}
