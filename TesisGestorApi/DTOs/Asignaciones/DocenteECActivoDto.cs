namespace TesisGestorApi.DTOs.Asignaciones
{
    public record DocenteECActivoDto(
        Guid IdDocenteEC,
        Guid IdEC,
        string NombreCurricula,
        string CodigoCurricula,
        string CodigoCurso,
        DateTime FechaDesde,
        List<HorarioDto> Horarios
    );
}
