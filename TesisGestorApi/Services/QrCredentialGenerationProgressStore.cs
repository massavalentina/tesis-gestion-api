using System.Collections.Concurrent;
using TesisGestorApi.DTOs;

namespace TesisGestorApi.Services
{
    public sealed class QrCredentialGenerationProgressStore
    {
        private readonly ConcurrentDictionary<Guid, QrCredentialGenerationJobState> _jobs = new();

        public QrCredentialGenerationProgressDto Create(int total)
        {
            var progress = new QrCredentialGenerationProgressDto
            {
                JobId = Guid.NewGuid(),
                Total = total,
                Inicio = DateTime.UtcNow,
                Estado = "RUNNING"
            };

            _jobs[progress.JobId] = new QrCredentialGenerationJobState(progress);
            return Clone(progress);
        }

        public bool TryGet(Guid jobId, out QrCredentialGenerationProgressDto dto)
        {
            if (_jobs.TryGetValue(jobId, out var job))
            {
                lock (job.SyncRoot)
                {
                    dto = Clone(job.Progress);
                    return true;
                }
            }

            dto = default!;
            return false;
        }

        public bool TryGetState(Guid jobId, out QrCredentialGenerationJobState state)
            => _jobs.TryGetValue(jobId, out state!);

        public void Update(Guid jobId, Action<QrCredentialGenerationProgressDto> update)
        {
            if (!_jobs.TryGetValue(jobId, out var job))
                return;

            lock (job.SyncRoot)
            {
                update(job.Progress);
            }
        }

        public void RecordGenerated(Guid jobId, Guid credencialId)
        {
            if (!_jobs.TryGetValue(jobId, out var job))
                return;

            lock (job.SyncRoot)
            {
                job.GeneratedCredentialIds.Add(credencialId);
            }
        }

        public void RecordDeactivated(Guid jobId, IEnumerable<Guid> credencialesIds)
        {
            if (!_jobs.TryGetValue(jobId, out var job))
                return;

            lock (job.SyncRoot)
            {
                job.DeactivatedCredentialIds.AddRange(credencialesIds);
            }
        }

        public bool RequestCancellation(Guid jobId, bool keepGenerated, out QrCredentialGenerationProgressDto progress)
        {
            if (!_jobs.TryGetValue(jobId, out var job))
            {
                progress = default!;
                return false;
            }

            lock (job.SyncRoot)
            {
                job.CancellationRequested = true;
                job.KeepGeneratedOnCancellation = keepGenerated;

                if (job.Progress.Estado is "RUNNING" or "PAUSING" or "PAUSED")
                {
                    job.Progress.Estado = "CANCELLING";
                    job.Progress.UltimoMensaje = keepGenerated
                        ? "Cancelación solicitada. Se completará el alumno en curso y se conservarán los QRs ya generados."
                        : "Cancelación solicitada. Se completará el alumno en curso y luego se revertirán los cambios realizados.";
                }

                job.PauseRequested = false;
                job.PauseReleaseSource?.TrySetResult(true);
                job.PauseReleaseSource = null;

                progress = Clone(job.Progress);
                return true;
            }
        }

        public bool RequestPause(Guid jobId, out QrCredentialGenerationProgressDto progress)
        {
            if (!_jobs.TryGetValue(jobId, out var job))
            {
                progress = default!;
                return false;
            }

            lock (job.SyncRoot)
            {
                if (job.Progress.Estado == "RUNNING")
                {
                    job.PauseRequested = true;
                    job.Progress.Estado = "PAUSING";
                    job.Progress.UltimoMensaje = "Pausa solicitada. Se completará el alumno en curso antes de detener temporalmente el proceso.";
                }

                progress = Clone(job.Progress);
                return true;
            }
        }

        public bool Pause(Guid jobId, out QrCredentialGenerationProgressDto progress)
        {
            if (!_jobs.TryGetValue(jobId, out var job))
            {
                progress = default!;
                return false;
            }

            lock (job.SyncRoot)
            {
                job.PauseRequested = false;
                job.PauseReleaseSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                job.Progress.Estado = "PAUSED";
                job.Progress.UltimoMensaje = "Proceso pausado. Elegí si querés continuarlo o cancelarlo.";
                progress = Clone(job.Progress);
                return true;
            }
        }

        public bool Resume(Guid jobId, out QrCredentialGenerationProgressDto progress)
        {
            if (!_jobs.TryGetValue(jobId, out var job))
            {
                progress = default!;
                return false;
            }

            lock (job.SyncRoot)
            {
                if (job.Progress.Estado == "PAUSED")
                {
                    job.Progress.Estado = "RUNNING";
                    job.Progress.UltimoMensaje = "Proceso reanudado.";
                    job.PauseReleaseSource?.TrySetResult(true);
                    job.PauseReleaseSource = null;
                }

                progress = Clone(job.Progress);
                return true;
            }
        }

        public bool TryMarkCompleted(Guid jobId, out QrCredentialGenerationProgressDto progress)
        {
            if (!_jobs.TryGetValue(jobId, out var job))
            {
                progress = default!;
                return false;
            }

            lock (job.SyncRoot)
            {
                if (job.Progress.Estado != "RUNNING")
                {
                    progress = Clone(job.Progress);
                    return false;
                }

                job.Progress.Estado = "COMPLETED";
                job.Progress.Fin = DateTime.UtcNow;
                job.Progress.UltimoMensaje = "Proceso finalizado.";
                progress = Clone(job.Progress);
                return true;
            }
        }

        private static QrCredentialGenerationProgressDto Clone(QrCredentialGenerationProgressDto progress)
        {
            return new QrCredentialGenerationProgressDto
            {
                JobId = progress.JobId,
                Estado = progress.Estado,
                Total = progress.Total,
                Procesados = progress.Procesados,
                Generados = progress.Generados,
                Desactivados = progress.Desactivados,
                Omitidos = progress.Omitidos,
                Errores = progress.Errores,
                UltimoEstudiante = progress.UltimoEstudiante,
                UltimoMensaje = progress.UltimoMensaje,
                Inicio = progress.Inicio,
                Fin = progress.Fin
            };
        }
    }

    public sealed class QrCredentialGenerationJobState
    {
        public QrCredentialGenerationJobState(QrCredentialGenerationProgressDto progress)
        {
            Progress = progress;
        }

        public object SyncRoot { get; } = new();
        public QrCredentialGenerationProgressDto Progress { get; }
        public bool PauseRequested { get; set; }
        public bool CancellationRequested { get; set; }
        public bool KeepGeneratedOnCancellation { get; set; }
        public TaskCompletionSource<bool>? PauseReleaseSource { get; set; }
        public List<Guid> GeneratedCredentialIds { get; } = new();
        public List<Guid> DeactivatedCredentialIds { get; } = new();
    }
}
