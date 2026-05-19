using TesisGestorApi.DTOs.Asignaciones;

namespace TesisGestorApi.Interfaces
{
    public interface IDocenteService
    {
        Task AsignarEspacioCurricularAsync(Guid idDocente, AsignarECDto dto, CancellationToken ct = default);
        Task DesasignarEspaciosCurricularesAsync(Guid idDocente, string motivo, CancellationToken ct = default);
        Task DesasignarEspacioCurricularAsync(Guid idDocente, Guid idDocenteEC, string motivo, CancellationToken ct = default);
        Task<DocenteECsResponseDto> GetEspaciosCurricularesAsync(Guid idDocente, CancellationToken ct = default);
        Task<List<ECsinDocenteDto>> GetEspaciosCurricularesSinDocenteAsync(CancellationToken ct = default);
    }
}
