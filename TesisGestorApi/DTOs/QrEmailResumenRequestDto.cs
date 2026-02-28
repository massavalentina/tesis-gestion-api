

public class QrEmailResumenRequestDto
{
    public Guid IdCurso { get; set; }   

    // false = excluir ya enviados (solo pendientes)
    // true  = incluir ya enviados (todos activos)
    public bool IncluirYaEnviados { get; set; } = false;

    // opcional; si no viene, usamos hardcode
    public int? AnioLectivo { get; set; }
}

