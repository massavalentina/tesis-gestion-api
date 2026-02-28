using TesisGestorApi.DTOs;

namespace TesisGestorApi.Interfaces
{
    public interface IQrEmailService
    {
        Task<QrEmailResumenDto> GetResumenAsync(QrEmailResumenRequestDto req, CancellationToken ct = default);
        Task<QrEmailStartResponseDto> StartEnvioAsync(QrEmailStartRequestDto req, CancellationToken ct = default);

        Task<QrEmailProgressDto> StartEnvioJobAsync(QrEmailStartRequestDto req, CancellationToken ct = default);

        Task<List<QrAlumnoEstadoDto>> GetAlumnosEstadoAsync(Guid? cursoId, string? estado, int anioLectivo, CancellationToken ct = default);
    }
}
