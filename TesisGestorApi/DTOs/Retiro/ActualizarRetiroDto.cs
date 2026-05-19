namespace TesisGestorApi.DTOs.Retiro
{
    public class ActualizarRetiroDto
    {
        public TimeSpan HorarioRetiro { get; set; }

        /// <summary>Motivo del retiro (null = no cambiar).</summary>
        public string? Motivo { get; set; }

        // ── Reingreso ─────────────────────────────────────────────────────────

        /// <summary>Actualiza el flag ConReingreso (null = no cambiar).</summary>
        public bool? ConReingreso { get; set; }

        /// <summary>Nueva hora límite de reingreso (requerida si ConReingreso=true).</summary>
        public TimeSpan? HorarioLimiteReingreso { get; set; }

        /// <summary>
        /// Si se provee, actualiza (o registra) el horario de reingreso efectivo.
        /// Debe ser mayor que HorarioRetiro.
        /// </summary>
        public TimeSpan? HorarioReingreso { get; set; }

        // ── Responsable contingente (solo si IdTutor es null) ─────────────────

        public string? NombreResponsable { get; set; }
        public string? ApellidoResponsable { get; set; }
        public string? DNIResponsable { get; set; }
        public string? RelacionResponsable { get; set; }
        public string? TelefonoResponsable { get; set; }
        public string? CorreoResponsable { get; set; }
    }
}
