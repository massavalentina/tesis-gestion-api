using TesisGestorApi.DTOs;

namespace TesisGestorApi.Interfaces
{
    public interface IQrCredentialGenerationService
    {
        Task<QrCredentialSummaryDto> GetSummaryAsync(Guid? cursoId, CancellationToken ct = default);
        Task<QrCredentialRegenerationResponseDto> RegenerateStudentCredentialAsync(Guid estudianteId, CancellationToken ct = default);
        Task<QrCredentialStudentStatusDto> GetStudentCredentialStatusAsync(Guid estudianteId, CancellationToken ct = default);
        Task<QrCredentialGenerationProgressDto> StartGenerationJobAsync(QrCredentialGenerationRequestDto req, CancellationToken ct = default);
        Task<QrCredentialGenerationProgressDto> PauseGenerationJobAsync(Guid jobId, CancellationToken ct = default);
        Task<QrCredentialGenerationProgressDto> ResumeGenerationJobAsync(Guid jobId, CancellationToken ct = default);
        Task<QrCredentialGenerationProgressDto> CancelGenerationJobAsync(Guid jobId, bool mantenerGenerados, CancellationToken ct = default);
    }
}
