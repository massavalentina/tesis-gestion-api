namespace TesisGestorApi.DTOs.Programas;

public class ProgramaResumenDto
{
    public Guid IdPrograma { get; set; }
    public string Titulo { get; set; } = null!;
    public int AnioLectivo { get; set; }
    public string Estado { get; set; } = null!;
    public string Origen { get; set; } = null!;
    public string NombreMateria { get; set; } = null!;
    public string CodigoCurso { get; set; } = null!;
    public int HorasCatedra { get; set; }
    public DateTime FechaCreacion { get; set; }
    public int CantidadUnidades { get; set; }
    public int CantidadTemas { get; set; }
    public int CantidadObjetivos { get; set; }
    public string NombreDocente { get; set; } = null!;
}
