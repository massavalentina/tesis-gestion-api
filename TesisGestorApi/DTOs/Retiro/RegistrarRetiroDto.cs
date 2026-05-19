namespace TesisGestorApi.DTOs.Retiro
{
    public class RegistrarRetiroDto
    {
        public Guid EstudianteId { get; set; }
        public DateOnly Fecha { get; set; }

        /// <summary>"MANANA" | "TARDE"</summary>
        public string Turno { get; set; } = null!;

        public TimeSpan HorarioRetiro { get; set; }
        public bool ConReingreso { get; set; }

        /// <summary>Requerido cuando ConReingreso = true.</summary>
        public TimeSpan? HorarioLimiteReingreso { get; set; }

        // ── Responsable: tutor registrado ─────────────────────────────────────
        public Guid? IdTutor { get; set; }

        // ── Responsable: persona contingente (si IdTutor es null) ────────────
        public string? NombreResponsable { get; set; }
        public string? ApellidoResponsable { get; set; }
        public string? DNIResponsable { get; set; }
        public string? RelacionResponsable { get; set; }
        public string? TelefonoResponsable { get; set; }
        public string? CorreoResponsable { get; set; }

        /// <summary>Motivo del retiro (obligatorio).</summary>
        public string Motivo { get; set; } = null!;
    }
}
