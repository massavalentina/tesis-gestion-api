namespace TesisGestorApi.DTOs
{
    public class QrCredentialDeliverySummaryDto
    {
        public Guid IdCurso { get; set; }
        public string CursoCodigo { get; set; } = string.Empty;
        public int AnioLectivo { get; set; }
        public string Alcance { get; set; } = "PENDIENTES";

        public int TotalAlumnosActivos { get; set; }
        public int TotalTutoresPrincipales { get; set; }
        public int TotalQrEnviados { get; set; }
        public int TotalQrPendientesEnvio { get; set; }
        public int TotalSinQrGenerado { get; set; }
        public int TotalSinTutorPrincipal { get; set; }
        public int TotalEmailInvalido { get; set; }

        public int TotalCandidatosSegunAlcance { get; set; }
        public int EstimacionSegundos { get; set; }
        public bool PuedeIniciarEnvio { get; set; }
        public string Mensaje { get; set; } = string.Empty;
    }
}
