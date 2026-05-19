namespace TesisGestorApi.DTOs.Asignaciones
{
    public record PreceptorCursosResponseDto(
        List<PreceptorCursoActivoDto> Activos,
        List<PreceptorCursoHistorialDto> Historial
    );
}
