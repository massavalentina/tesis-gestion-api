namespace TesisGestorApi.DTOs
{
    public class DetalleDocenteRegistroDto
    {
        public string Fecha { get; set; } = null!;
        public bool Dictada { get; set; }
        public bool? Presente { get; set; }
        /// <summary>Código actual del turno mañana (puede ser RA/RAE/RE si hubo retiro).</summary>
        public string? Codigo { get; set; }
        /// <summary>Código de llegada original (LLT/LLTE/LLTC/P/A/ANC). Nulo si no difiere de Codigo.</summary>
        public string? CodigoLlegada { get; set; }
        public string? HoraEntrada { get; set; }
        /// <summary>Hora de salida anticipada (retiro) del turno mañana.</summary>
        public string? HoraSalida { get; set; }
        /// <summary>Hora real de reingreso (del registro de RetiroAnticipado, si aplica).</summary>
        public string? HoraReingreso { get; set; }
    }
}
