using System.ComponentModel.DataAnnotations;

namespace TesisGestorApi.Entities
{
    public class RetiroAnticipado
    {
        [Key]
        public Guid IdRetiro { get; set; }

        /// <summary>"MANANA" | "TARDE"</summary>
        public string Turno { get; set; } = null!;

        public DateTime HorarioRetiro { get; set; }
        public bool ConReingreso { get; set; }

        /// <summary>Hora límite de reingreso (solo cuando ConReingreso = true).</summary>
        public DateTime? HorarioLimiteReingreso { get; set; }

        /// <summary>Hora real de reingreso (null hasta que el preceptor lo registre).</summary>
        public DateTime? HorarioReingreso { get; set; }

        /// <summary>Motivo del retiro (obligatorio).</summary>
        public string Motivo { get; set; } = null!;

        /// <summary>Nombre del preceptor que autoriza (CDA006 — sin auth todavía).</summary>
        public string NombrePreceptor { get; set; } = null!;

        // ── Estudiante ────────────────────────────────────────────────────────
        public Guid IdEstudiante { get; set; }
        public Estudiante Estudiante { get; set; } = null!;

        // ── Responsable: tutor registrado (opcional) ─────────────────────────
        public Guid? IdTutor { get; set; }
        public Tutor? Tutor { get; set; }

        // ── Responsable: persona contingente (si IdTutor es null) ────────────
        public string? NombreResponsable { get; set; }
        public string? ApellidoResponsable { get; set; }
        public string? DNIResponsable { get; set; }
        public string? RelacionResponsable { get; set; }
        public string? TelefonoResponsable { get; set; }
        public string? CorreoResponsable { get; set; }

        // ── Asistencia asociada ───────────────────────────────────────────────
        public Guid IdAsistencia { get; set; }
        public Asistencia Asistencia { get; set; } = null!;
    }
}
