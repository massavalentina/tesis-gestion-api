using Microsoft.AspNetCore.Mvc;
using TesisGestorApi.DTOs;
using TesisGestorApi.Interfaces;
using TesisGestorApi.Services;

namespace TesisGestorApi.Controllers
{
    [ApiController]
    [Route("api/qr-credentials")]
    public class QrCredentialGenerationController : ControllerBase
    {
        private readonly IQrCredentialGenerationService _service;
        private readonly QrCredentialGenerationProgressStore _progressStore;

        public QrCredentialGenerationController(
            IQrCredentialGenerationService service,
            QrCredentialGenerationProgressStore progressStore)
        {
            _service = service;
            _progressStore = progressStore;
        }

        [HttpGet("summary")]
        public async Task<ActionResult<QrCredentialSummaryDto>> Summary([FromQuery] Guid? cursoId, CancellationToken ct)
        {
            try
            {
                var result = await _service.GetSummaryAsync(cursoId, ct);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("generation/start-job")]
        public async Task<ActionResult<object>> StartJob([FromBody] QrCredentialGenerationRequestDto req, CancellationToken ct)
        {
            if (req.IdCurso == Guid.Empty)
                return BadRequest("IdCurso inválido.");

            try
            {
                var job = await _service.StartGenerationJobAsync(req, ct);
                return Ok(new { jobId = job.JobId });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("generation/progress/{jobId:guid}")]
        public ActionResult<QrCredentialGenerationProgressDto> Progress([FromRoute] Guid jobId)
        {
            if (!_progressStore.TryGet(jobId, out var dto))
                return NotFound("Job no encontrado.");

            return Ok(dto);
        }
    }
}
