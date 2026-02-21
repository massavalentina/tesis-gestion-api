public class QrEmailStartResponseDto
{
    public int AnioLectivo { get; set; }
    public Guid IdCurso { get; set; }

    public int TotalAlumnosActivos { get; set; }
    public int Procesados { get; set; }
    public int Enviados { get; set; }
    public int Omitidos { get; set; }
    public int Errores { get; set; }

    public List<string> DetallesOmitidos { get; set; } = new();
    public List<string> DetallesErrores { get; set; } = new();

    public string Mensaje { get; set; } = "";
}