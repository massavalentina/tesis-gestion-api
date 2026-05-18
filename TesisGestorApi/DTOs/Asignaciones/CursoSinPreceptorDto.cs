namespace TesisGestorApi.DTOs.Asignaciones
{
    public record CursoSinPreceptorDto(
        Guid IdCurso,
        string Codigo,
        int Anio,
        char Division,
        bool TieneHistorial
    );
}
