using TesisGestorApi.DTOs.Calificaciones;

namespace TesisGestorApi.Interfaces
{
    public interface ICalificacionesService
    {
        Task<List<InstanciaEvaluativaResumenDto>> GetInstanciasPorECAsync(Guid idEC, Guid idDocente, CancellationToken ct);
        Task<List<GestionManualEstudianteDto>> GetEstudiantesPorECAsync(Guid idEC, Guid idDocente, CancellationToken ct);
        Task<List<CalificacionVigenteDto>> GetCalificacionesVigentesPorECAsync(Guid idEC, Guid idDocente, CancellationToken ct);
        Task<GuardarCalificacionesManualResponseDto> GuardarGestionManualAsync(Guid idEC, Guid idDocente, GuardarCalificacionesManualDto dto, CancellationToken ct);
        Task<AuditoriaCalificacionesResponseDto> GetAuditoriaAsync(Guid idEC, Guid idDocente, int skip, int take, CancellationToken ct);
    }
}
