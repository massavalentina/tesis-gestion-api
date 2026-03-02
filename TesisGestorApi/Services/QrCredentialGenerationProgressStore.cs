using System.Collections.Concurrent;
using TesisGestorApi.DTOs;

namespace TesisGestorApi.Services
{
    public class QrCredentialGenerationProgressStore
    {
        private readonly ConcurrentDictionary<Guid, QrCredentialGenerationProgressDto> _jobs = new();

        public QrCredentialGenerationProgressDto Create(int total)
        {
            var job = new QrCredentialGenerationProgressDto
            {
                JobId = Guid.NewGuid(),
                Total = total,
                Inicio = DateTime.UtcNow,
                Estado = "RUNNING"
            };

            _jobs[job.JobId] = job;
            return job;
        }

        public bool TryGet(Guid jobId, out QrCredentialGenerationProgressDto dto) => _jobs.TryGetValue(jobId, out dto!);

        public void Update(Guid jobId, Action<QrCredentialGenerationProgressDto> update)
        {
            if (_jobs.TryGetValue(jobId, out var job))
                update(job);
        }
    }
}
