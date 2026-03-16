namespace TesisGestorApi.DTOs;
public class ActualizarAsistenciaEspacioDto
{
    public Guid EstudianteId    { get; set; }
    public Guid IdClaseDictada  { get; set; }
    public bool Presente        { get; set; }
}
