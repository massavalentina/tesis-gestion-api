namespace TesisGestorApi.DTOs.Asignaciones
{
    public record DocenteECHistorialDto(
        Guid IdDocenteEC,
        Guid IdEC,
        string NombreCurricula,
        string CodigoCurricula,
        string CodigoCurso,
        DateTime FechaDesde,
        DateTime? FechaHasta,
        string? Motivo
    );
}
