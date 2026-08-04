using TesisGestorApi.DTOs;

namespace TesisGestorApi.Services
{
    public sealed class QrCredentialDeliveryActiveJobException : InvalidOperationException
    {
        public QrCredentialDeliveryActiveJobException(QrCredentialDeliveryActiveJobDto activeJob)
            : base($"Ya existe un envío activo para el curso {activeJob.CursoCodigo}.")
        {
            ActiveJob = activeJob;
        }

        public QrCredentialDeliveryActiveJobDto ActiveJob { get; }
    }
}
