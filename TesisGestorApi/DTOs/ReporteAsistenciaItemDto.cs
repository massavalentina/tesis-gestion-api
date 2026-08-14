namespace TesisGestorApi.DTOs
{
    public class ReporteAsistenciaItemDto
    {
        public Guid IdEstudiante { get; set; }
        public string Nombre { get; set; } = null!;
        public string Apellido { get; set; } = null!;
        public string Documento { get; set; } = null!;
        public int Presencias { get; set; }
        public decimal Inasistencias { get; set; }
        /// <summary>Sumatoria de inasistencias por llegadas tarde: LLT×0.25 + LLTE×0.5 + LLTC×1.0</summary>
        public decimal AusentePorLLT { get; set; }
        /// <summary>
        /// Suma de inasistencias generadas por retiros anticipados:
        /// RA (cualquier turno) = 0,5 · RAE turno mañana = 1,0 · RAE turno tarde = 0,5.
        /// </summary>
        public decimal AusentePorRA { get; set; }
        /// <summary>Inasistencias generadas exclusivamente por código A (ausente al establecimiento).</summary>
        public decimal AusenciasPuras { get; set; }
        public decimal PorcentajeAsistencia { get; set; }
        public bool TeaGeneral { get; set; }
    }
}
