namespace TesisGestorApi.DTOs
{
    public class DetalleAsistenciaEstudianteDto
    {
        public DateOnly Fecha { get; set; }
        /// <summary>Código de llegada (P, A, LLT, LLTE, LLTC, ANC…). Usa TipoLlegadaManiana cuando existe.</summary>
        public string CodigoManana { get; set; } = null!;
        /// <summary>Código de retiro (RA, RAE, RE) cuando el turno mañana tiene llegada + retiro en el mismo día.</summary>
        public string? CodigoRetiroManana { get; set; }
        public string CodigoTarde { get; set; } = null!;
        public decimal ValorTotal { get; set; }
        public string? HoraEntradaManana { get; set; }
        public string? HoraSalidaManana { get; set; }
        public string? HoraSalidaTarde { get; set; }
    }
}
