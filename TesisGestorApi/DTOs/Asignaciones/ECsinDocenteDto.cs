namespace TesisGestorApi.DTOs.Asignaciones
{
    public record ECsinDocenteDto(
        Guid IdEC,
        string NombreCurricula,
        string CodigoCurricula,
        string CodigoCurso,
        bool TieneHistorial
    );
}
