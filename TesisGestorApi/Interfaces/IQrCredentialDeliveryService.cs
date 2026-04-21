using TesisGestorApi.DTOs;

namespace TesisGestorApi.Interfaces
{
    public interface IQrCredentialDeliveryService
    {
        Task<QrCredentialDeliverySummaryDto> GetSummaryAsync(Guid cursoId, string? alcance, CancellationToken ct = default);
        Task<QrCredentialDeliveryProgressDto> StartDeliveryJobAsync(QrCredentialDeliveryRequestDto req, CancellationToken ct = default);
        Task<QrCredentialDeliveryProgressDto> CancelDeliveryJobAsync(Guid jobId, CancellationToken ct = default);
        Task<QrCredentialDeliveryStudentsPageDto> GetStudentsPageAsync(
            Guid cursoId,
            string? estado,
            string? busqueda,
            int page,
            int pageSize,
            CancellationToken ct = default);

        Task<(byte[] Bytes, string FileName)> GetStudentQrImageAsync(Guid estudianteId, CancellationToken ct = default);
    }
}
