namespace TesisGestorApi.DTOs.Retiro
{
    public class RetiroActivoDto
    {
        public Guid IdRetiro { get; set; }

        /// <summary>"MANANA" | "TARDE"</summary>
        public string Turno { get; set; } = null!;

        /// <summary>Hora del retiro en formato "HH:mm".</summary>
        public string HorarioRetiro { get; set; } = null!;

        public bool ConReingreso { get; set; }

        /// <summary>Hora límite de reingreso en "HH:mm" (null si sin reingreso).</summary>
        public string? HorarioLimiteReingreso { get; set; }

        /// <summary>Hora real de reingreso en "HH:mm" (null hasta que se registre).</summary>
        public string? HorarioReingreso { get; set; }

        /// <summary>"ConReingreso" | "ReingresoVencido" | "Reingresado" | null</summary>
        public string? EtiquetaEstado { get; set; }

        /// <summary>"RE" | "RA" | "RAE" — tipo calculado de inasistencia.</summary>
        public string? TipoRetiro { get; set; }

        public string? NombrePreceptor { get; set; }

        /// <summary>Motivo del retiro.</summary>
        public string? Motivo { get; set; }

        // ── Responsable ──────────────────────────────────────────────────────────

        /// <summary>Id del tutor si el responsable es un tutor registrado.</summary>
        public Guid? IdTutor { get; set; }

        /// <summary>Nombre del responsable (tutor o contingente — primer nombre).</summary>
        public string? NombreResponsable { get; set; }

        /// <summary>Apellido del responsable.</summary>
        public string? ApellidoResponsable { get; set; }

        /// <summary>DNI del responsable (tutor usa Documento; contingente usa DNIResponsable).</summary>
        public string? DniResponsable { get; set; }

        /// <summary>Relación del responsable con el estudiante.</summary>
        public string? RelacionResponsable { get; set; }

        /// <summary>Teléfono del responsable.</summary>
        public string? TelefonoResponsable { get; set; }

        /// <summary>Correo del responsable.</summary>
        public string? CorreoResponsable { get; set; }
    }
}
