namespace TesisGestorApi.DTOs.Asignaciones
{
    public record DocenteECsResponseDto(
        List<DocenteECActivoDto> Activos,
        List<DocenteECHistorialDto> Historial
    );
}
