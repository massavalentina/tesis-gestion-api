using TesisGestorApi.DTOs.Asignaciones;

namespace TesisGestorApi.Interfaces
{
    public interface IPreceptorService
    {
        Task AsignarCursoAsync(Guid idPreceptor, AsignarCursoDto dto, CancellationToken ct = default);
        Task DesasignarCursosAsync(Guid idPreceptor, string motivo, CancellationToken ct = default);
        Task DesasignarCursoAsync(Guid idPreceptor, Guid idPreceptorCurso, string motivo, CancellationToken ct = default);
        Task<PreceptorCursosResponseDto> GetCursosAsync(Guid idPreceptor, CancellationToken ct = default);
        Task<List<CursoSinPreceptorDto>> GetCursosSinPreceptorAsync(CancellationToken ct = default);
    }
}
