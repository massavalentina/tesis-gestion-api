using TesisGestorApi.DTOs;

namespace TesisGestorApi.Interfaces
{
    public interface IQrCredentialGenerationService
    {
        Task<QrCredentialSummaryDto> GetSummaryAsync(Guid? cursoId, CancellationToken ct = default);
        Task<QrCredentialGenerationProgressDto> StartGenerationJobAsync(QrCredentialGenerationRequestDto req, CancellationToken ct = default);
    }
}
