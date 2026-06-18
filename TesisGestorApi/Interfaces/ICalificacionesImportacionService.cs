using TesisGestorApi.DTOs.CalificacionesImportacion;

namespace TesisGestorApi.Interfaces
{
    public interface ICalificacionesImportacionService
    {
        Task<ImportacionCalificacionesDetalleDto?> GetActivaPorECAsync(Guid idEC, Guid idDocente, CancellationToken ct);
        Task<ImportacionCalificacionesDetalleDto> AnalizarAsync(Guid idEC, Guid idDocente, AnalizarImportacionCalificacionesDto dto, CancellationToken ct);
        Task<ImportacionCalificacionesDetalleDto> GetDetalleAsync(Guid idImportacion, Guid idDocente, CancellationToken ct);
        Task<ImportacionCalificacionesDetalleDto> ReanalizarAsync(Guid idImportacion, Guid idDocente, CancellationToken ct);
        Task<ImportacionRevisionDto> GetRevisionAsync(Guid idImportacion, Guid idDocente, CancellationToken ct);
        Task<ImportacionRevisionDto> GuardarRevisionAsync(Guid idImportacion, Guid idDocente, ActualizarImportacionRevisionDto dto, CancellationToken ct);
        Task<ImportacionConfirmacionDto> GetConfirmacionAsync(Guid idImportacion, Guid idDocente, CancellationToken ct);
        Task<ConfirmarImportacionCalificacionesResponseDto> ConfirmarAsync(Guid idImportacion, Guid idDocente, CancellationToken ct);
        Task CancelarAsync(Guid idImportacion, Guid idDocente, CancellationToken ct);
    }
}
