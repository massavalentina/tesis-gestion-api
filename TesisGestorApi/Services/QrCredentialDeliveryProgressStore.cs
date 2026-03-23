using System.Collections.Concurrent;
using TesisGestorApi.DTOs;

namespace TesisGestorApi.Services
{
    public sealed class QrCredentialDeliveryProgressStore
    {
        private readonly ConcurrentDictionary<Guid, QrCredentialDeliveryJobState> _jobs = new();

        public QrCredentialDeliveryProgressDto Create(int total)
        {
            var progress = new QrCredentialDeliveryProgressDto
            {
                JobId = Guid.NewGuid(),
                Total = total,
                Inicio = DateTime.UtcNow,
                Estado = "RUNNING"
            };

            _jobs[progress.JobId] = new QrCredentialDeliveryJobState(progress);
            return Clone(progress);
        }

        public bool TryGet(Guid jobId, out QrCredentialDeliveryProgressDto dto)
        {
            if (_jobs.TryGetValue(jobId, out var state))
            {
                lock (state.SyncRoot)
                {
                    dto = Clone(state.Progress);
                    return true;
                }
            }

            dto = default!;
            return false;
        }

        public void Update(Guid jobId, Action<QrCredentialDeliveryProgressDto> update)
        {
            if (!_jobs.TryGetValue(jobId, out var state))
                return;

            lock (state.SyncRoot)
            {
                update(state.Progress);
            }
        }

        private static QrCredentialDeliveryProgressDto Clone(QrCredentialDeliveryProgressDto dto)
        {
            return new QrCredentialDeliveryProgressDto
            {
                JobId = dto.JobId,
                Estado = dto.Estado,
                Total = dto.Total,
                Procesados = dto.Procesados,
                Enviados = dto.Enviados,
                Omitidos = dto.Omitidos,
                Errores = dto.Errores,
                UltimoDestino = dto.UltimoDestino,
                UltimoEstudiante = dto.UltimoEstudiante,
                UltimoMensaje = dto.UltimoMensaje,
                DetallesErrores = dto.DetallesErrores.ToList(),
                Inicio = dto.Inicio,
                Fin = dto.Fin
            };
        }
    }

    public sealed class QrCredentialDeliveryJobState
    {
        public QrCredentialDeliveryJobState(QrCredentialDeliveryProgressDto progress)
        {
            Progress = progress;
        }

        public object SyncRoot { get; } = new();
        public QrCredentialDeliveryProgressDto Progress { get; }
    }
}
