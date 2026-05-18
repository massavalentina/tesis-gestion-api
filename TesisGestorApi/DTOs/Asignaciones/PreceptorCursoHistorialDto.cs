namespace TesisGestorApi.DTOs.Asignaciones
{
    public record PreceptorCursoHistorialDto(
        Guid IdPreceptorCurso,
        Guid IdCurso,
        string CodigoCurso,
        int Anio,
        char Division,
        DateTime FechaDesde,
        DateTime? FechaHasta,
        string? Motivo
    );
}
