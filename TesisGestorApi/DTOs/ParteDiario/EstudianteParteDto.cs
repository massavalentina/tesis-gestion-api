using TesisGestorApi.DTOs.Retiro;

namespace TesisGestorApi.DTOs.ParteDiario
{
    public class EstudianteParteDto
    {
        public Guid IdEstudiante { get; set; }
        public string Nombre { get; set; } = null!;
        public string Apellido { get; set; } = null!;
        public string Documento { get; set; } = null!;

        /// <summary>Presente | Ausente | Retirado | SinRegistro</summary>
        public string Estado { get; set; } = null!;
        public string? CodigoAsistencia { get; set; }
        public string? HoraEntrada { get; set; }
        public string? HoraSalida { get; set; }

        /// <summary>Código de llegada del turno mañana (LLT/LLTE/LLTC/P/A) — null si no hay llegada tardía registrada.</summary>
        public string? CodigoLlegadaManiana { get; set; }

        /// <summary>Retiro activo del día (null si no tiene retiro registrado).</summary>
        public RetiroActivoDto? RetiroActivo { get; set; }
    }
}
