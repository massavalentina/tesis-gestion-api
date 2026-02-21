

public class QrEmailResumenDto
{
    public int AnioLectivo { get; set; }
    public Guid IdCurso { get; set; }
    public string CursoCodigo { get; set; } = "";

    public int TotalAlumnosActivos { get; set; }
    public int ConQrPendiente { get; set; }
    public int YaEnviados { get; set; }
    public int SinQrGenerado { get; set; }

    public int EstimacionSegundos { get; set; }
    public bool PuedeIniciarEnvio { get; set; }
    public string Mensaje { get; set; } = "";
}
