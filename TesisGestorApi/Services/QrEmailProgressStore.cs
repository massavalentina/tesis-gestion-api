using System.Collections.Concurrent;
using TesisGestorApi.DTOs;

namespace TesisGestorApi.Services
{
    public class QrEmailProgressStore
    {
        private readonly ConcurrentDictionary<Guid, QrEmailProgressDto> _jobs = new();



        public QrEmailProgressDto Create(int total)
        {
            var job = new QrEmailProgressDto
            {
                JobId = Guid.NewGuid(),
                Total = total,
                Inicio = DateTime.UtcNow,
                Estado = "RUNNING"
            };
            _jobs[job.JobId] = job;
            return job;
        }

        public bool TryGet(Guid jobId, out QrEmailProgressDto dto) => _jobs.TryGetValue(jobId, out dto!);

        public void Update(Guid jobId, Action<QrEmailProgressDto> update)
        {
            if (_jobs.TryGetValue(jobId, out var job))
                update(job);
        }

        public void Complete(Guid jobId)
        {
            Update(jobId, j =>
            {
                j.Estado = "COMPLETED";
                j.Fin = DateTime.UtcNow;
            });
        }

        public void Fail(Guid jobId, string msg)
        {
            Update(jobId, j =>
            {
                j.Estado = "FAILED";
                j.UltimoMensaje = msg;
                j.Fin = DateTime.UtcNow;
            });
        }
    }
}