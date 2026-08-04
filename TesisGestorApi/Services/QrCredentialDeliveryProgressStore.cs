using System.Collections.Concurrent;
using TesisGestorApi.DTOs;

namespace TesisGestorApi.Services
{
    public sealed class QrCredentialDeliveryProgressStore
    {
        private readonly ConcurrentDictionary<Guid, QrCredentialDeliveryJobState> _jobs = new();
        private readonly object _jobCreationSync = new();

        public bool TryCreate(
            Guid cursoId,
            string cursoCodigo,
            string alcance,
            int total,
            out QrCredentialDeliveryProgressDto progress,
            out QrCredentialDeliveryActiveJobDto? activeJob)
        {
            lock (_jobCreationSync)
            {
                if (TryGetActiveByCourseInternal(cursoId, out var currentActiveJob))
                {
                    progress = default!;
                    activeJob = currentActiveJob;
                    return false;
                }

                var progressDto = new QrCredentialDeliveryProgressDto
                {
                    JobId = Guid.NewGuid(),
                    Total = total,
                    Inicio = DateTime.UtcNow,
                    Estado = "RUNNING"
                };

                var state = new QrCredentialDeliveryJobState(progressDto)
                {
                    CursoId = cursoId,
                    CursoCodigo = cursoCodigo,
                    Alcance = alcance
                };

                _jobs[progressDto.JobId] = state;
                progress = Clone(progressDto);
                activeJob = CloneActiveJob(state);
                return true;
            }
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

        public IReadOnlyList<QrCredentialDeliveryActiveJobDto> GetActiveJobs(Guid? cursoId = null)
        {
            var items = new List<QrCredentialDeliveryActiveJobDto>();

            foreach (var state in _jobs.Values)
            {
                lock (state.SyncRoot)
                {
                    if (!IsActiveState(state.Progress.Estado))
                    {
                        continue;
                    }

                    if (cursoId.HasValue && state.CursoId != cursoId.Value)
                    {
                        continue;
                    }

                    items.Add(CloneActiveJob(state));
                }
            }

            return items
                .OrderByDescending(x => x.Inicio)
                .ToList();
        }

        public bool TryGetActiveByCourse(Guid cursoId, out QrCredentialDeliveryActiveJobDto activeJob)
        {
            lock (_jobCreationSync)
            {
                return TryGetActiveByCourseInternal(cursoId, out activeJob);
            }
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

        private bool TryGetActiveByCourseInternal(Guid cursoId, out QrCredentialDeliveryActiveJobDto activeJob)
        {
            foreach (var state in _jobs.Values)
            {
                lock (state.SyncRoot)
                {
                    if (state.CursoId != cursoId || !IsActiveState(state.Progress.Estado))
                    {
                        continue;
                    }

                    activeJob = CloneActiveJob(state);
                    return true;
                }
            }

            activeJob = default!;
            return false;
        }

        private static bool IsActiveState(string? estado)
            => estado is "RUNNING" or "PAUSING" or "PAUSED" or "CANCELLING";

        private static QrCredentialDeliveryActiveJobDto CloneActiveJob(QrCredentialDeliveryJobState state)
        {
            return new QrCredentialDeliveryActiveJobDto
            {
                JobId = state.Progress.JobId,
                IdCurso = state.CursoId,
                CursoCodigo = state.CursoCodigo,
                Alcance = state.Alcance,
                Estado = state.Progress.Estado,
                Total = state.Progress.Total,
                Procesados = state.Progress.Procesados,
                Enviados = state.Progress.Enviados,
                Omitidos = state.Progress.Omitidos,
                Errores = state.Progress.Errores,
                UltimoMensaje = state.Progress.UltimoMensaje,
                Inicio = state.Progress.Inicio
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
        public Guid CursoId { get; init; }
        public string CursoCodigo { get; init; } = string.Empty;
        public string Alcance { get; init; } = "PENDIENTES";
        public bool PauseRequested { get; set; }
        public bool CancellationRequested { get; set; }
        public TaskCompletionSource<bool>? PauseReleaseSource { get; set; }
    }
}
