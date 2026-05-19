namespace TesisGestorApi.DTOs.Asignaciones
{
    public record PreceptorCursoActivoDto(
        Guid IdPreceptorCurso,
        Guid IdCurso,
        string CodigoCurso,
        int Anio,
        char Division,
        DateTime FechaDesde
    );
}
