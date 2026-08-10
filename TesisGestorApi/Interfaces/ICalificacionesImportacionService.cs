using TesisGestorApi.DTOs.CalificacionesImportacion;

namespace TesisGestorApi.Interfaces
{
    public interface ICalificacionesImportacionService
    {
        Task<ImportacionCalificacionesAnalisisDto> AnalizarAsync(Guid idEC, Guid idDocente, AnalizarImportacionCalificacionesDto dto, CancellationToken ct);
        Task<ConfirmarImportacionCalificacionesResponseDto> ConfirmarAsync(Guid idEC, Guid idDocente, ConfirmarImportacionCalificacionesDto dto, CancellationToken ct);
    }
}
