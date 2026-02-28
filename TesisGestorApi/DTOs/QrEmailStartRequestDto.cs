public class QrEmailStartRequestDto
{
    public Guid IdCurso { get; set; }
    public bool IncluirYaEnviados { get; set; } = false;
    public int? AnioLectivo { get; set; }
}