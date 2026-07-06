namespace TesisGestorApi.DTOs.Calendario;

public class EventoDocenteDto
{
    public string Id { get; set; } = null!;
    public string Tipo { get; set; } = null!;          // "ClasePlanificada" | "InstanciaEvaluativa"
    public int TipoEventoNumero { get; set; }          // 7 = Clase, 8 = IE
    public string Titulo { get; set; } = null!;
    public string? Descripcion { get; set; }
    public string Fecha { get; set; } = null!;         // yyyy-MM-dd
    public string? FechaFin { get; set; }
    public string Estado { get; set; } = null!;
    public string NombreMateria { get; set; } = null!;
    public string NombreCurso { get; set; } = null!;
    public Guid IdCurso { get; set; }
    public Guid IdEC { get; set; }

    // Solo para IE
    public string? TipoIE { get; set; }
    public string? TipoCalificacion { get; set; }
    public int? NroInstancia { get; set; }

    /// true = evento del propio EC del docente; false = IE de otro EC del mismo curso
    public bool EsPropioDocente { get; set; }
}
