using TesisGestorApi.DTOs.Evaluaciones;

namespace TesisGestorApi.Interfaces;

public interface IEvaluacionesService
{
    Task<GestionEvaluacionesDto> GetGestionAsync(Guid idEC, Guid idDocente, CancellationToken ct);

    Task<InstanciaEvaluativaSlotDto> GuardarArchivoAsync(
        Guid idEC,
        Guid idDocente,
        int nro,
        string tipoCalificacion,
        GuardarArchivoIEFormDto dto,
        string? urlArchivo,
        CancellationToken ct);

    Task<InstanciaEvaluativaSlotDto> CambiarEstadoAsync(
        Guid idEC,
        Guid idDocente,
        int nro,
        CambiarEstadoIEFormDto dto,
        CancellationToken ct);

    Task EliminarArchivoAsync(Guid idEC, Guid idDocente, int nro, string tipoCalificacion, CancellationToken ct);
}
