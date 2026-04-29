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
        public int LlegadasTarde { get; set; }
        public int AusentePorLLT { get; set; }
        /// <summary>Cantidad de eventos RA (Retiro Anticipado) en cualquier turno.</summary>
        public int RetirosAnticipados { get; set; }
        /// <summary>Cantidad de eventos RE (Retiro Express) en cualquier turno.</summary>
        public int RetirosExpress { get; set; }
        /// <summary>Cantidad de eventos RAE (Retiro Anticipado Extendido) en cualquier turno.</summary>
        public int RetirosAnticipadosExtendidos { get; set; }
        /// <summary>
        /// Suma de inasistencias generadas por retiros anticipados:
        /// RA (cualquier turno) = 0,5 · RAE turno mañana = 1,0 · RAE turno tarde = 0,5.
        /// </summary>
        public decimal AusentePorRA { get; set; }
        /// <summary>Inasistencias generadas exclusivamente por código A (ausente al establecimiento), en unidades de 0.5 por turno.</summary>
        public decimal AusenciasPuras { get; set; }
        /// <summary>Cantidad de registros ANC (Ausencia No Computable) en cualquier turno.</summary>
        public int AusentesNoComputables { get; set; }
        public decimal PorcentajeAsistencia { get; set; }
        public bool TeaGeneral { get; set; }
    }
}
