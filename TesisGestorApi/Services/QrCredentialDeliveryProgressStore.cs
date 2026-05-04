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

        public bool TryGetState(Guid jobId, out QrCredentialDeliveryJobState state)
            => _jobs.TryGetValue(jobId, out state!);

        public void Update(Guid jobId, Action<QrCredentialDeliveryProgressDto> update)
        {
            if (!_jobs.TryGetValue(jobId, out var state))
                return;

            lock (state.SyncRoot)
            {
                update(state.Progress);
            }
        }

        public bool RequestCancellation(Guid jobId, out QrCredentialDeliveryProgressDto progress)
        {
            if (!_jobs.TryGetValue(jobId, out var state))
            {
                progress = default!;
                return false;
            }

            lock (state.SyncRoot)
            {
                if (state.Progress.Estado is "COMPLETED" or "FAILED" or "CANCELLED")
                {
                    progress = Clone(state.Progress);
                    return true;
                }

                state.CancellationRequested = true;
                state.PauseRequested = false;
                state.PauseReleaseSource?.TrySetResult(true);
                state.PauseReleaseSource = null;

                if (state.Progress.Estado is "RUNNING" or "PAUSING" or "PAUSED" or "CANCELLING")
                {
                    state.Progress.Estado = "CANCELLING";
                    state.Progress.UltimoMensaje = "Cancelación solicitada. Se completará el envío en curso y luego se detendrán los pendientes.";
                }

                progress = Clone(state.Progress);
                return true;
            }
        }

        public bool RequestPause(Guid jobId, out QrCredentialDeliveryProgressDto progress)
        {
            if (!_jobs.TryGetValue(jobId, out var state))
            {
                progress = default!;
                return false;
            }

            lock (state.SyncRoot)
            {
                if (state.Progress.Estado == "RUNNING")
                {
                    state.PauseRequested = true;
                    state.Progress.Estado = "PAUSING";
                    state.Progress.UltimoMensaje = "Pausa solicitada. Se completará el envío en curso antes de pausar.";
                }

                progress = Clone(state.Progress);
                return true;
            }
        }

        public bool Pause(Guid jobId, out QrCredentialDeliveryProgressDto progress)
        {
            if (!_jobs.TryGetValue(jobId, out var state))
            {
                progress = default!;
                return false;
            }

            lock (state.SyncRoot)
            {
                state.PauseRequested = false;
                state.PauseReleaseSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                state.Progress.Estado = "PAUSED";
                state.Progress.UltimoMensaje = "Proceso pausado. Elegí si querés continuarlo o cancelarlo.";
                progress = Clone(state.Progress);
                return true;
            }
        }

        public bool Resume(Guid jobId, out QrCredentialDeliveryProgressDto progress)
        {
            if (!_jobs.TryGetValue(jobId, out var state))
            {
                progress = default!;
                return false;
            }

            lock (state.SyncRoot)
            {
                if (state.Progress.Estado == "PAUSED")
                {
                    state.Progress.Estado = "RUNNING";
                    state.Progress.UltimoMensaje = "Proceso reanudado.";
                    state.PauseReleaseSource?.TrySetResult(true);
                    state.PauseReleaseSource = null;
                }

                progress = Clone(state.Progress);
                return true;
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
        public bool PauseRequested { get; set; }
        public bool CancellationRequested { get; set; }
        public TaskCompletionSource<bool>? PauseReleaseSource { get; set; }
    }
}
