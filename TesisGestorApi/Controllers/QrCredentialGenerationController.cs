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

        [HttpPost("student/{estudianteId:guid}/regenerate")]
        public async Task<ActionResult<QrCredentialRegenerationResponseDto>> RegenerateStudentCredential(
            [FromRoute] Guid estudianteId,
            CancellationToken ct)
        {
            try
            {
                var result = await _service.RegenerateStudentCredentialAsync(estudianteId, ct);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("student/{estudianteId:guid}/status")]
        public async Task<ActionResult<QrCredentialStudentStatusDto>> GetStudentCredentialStatus(
            [FromRoute] Guid estudianteId,
            CancellationToken ct)
        {
            try
            {
                var result = await _service.GetStudentCredentialStatusAsync(estudianteId, ct);
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

        [HttpPost("generation/pause/{jobId:guid}")]
        public async Task<ActionResult<QrCredentialGenerationProgressDto>> PauseJob([FromRoute] Guid jobId, CancellationToken ct)
        {
            try
            {
                var progress = await _service.PauseGenerationJobAsync(jobId, ct);
                return Ok(progress);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("generation/resume/{jobId:guid}")]
        public async Task<ActionResult<QrCredentialGenerationProgressDto>> ResumeJob([FromRoute] Guid jobId, CancellationToken ct)
        {
            try
            {
                var progress = await _service.ResumeGenerationJobAsync(jobId, ct);
                return Ok(progress);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("generation/cancel/{jobId:guid}")]
        public async Task<ActionResult<QrCredentialGenerationProgressDto>> CancelJob(
            [FromRoute] Guid jobId,
            [FromBody] QrCredentialGenerationCancelRequestDto req,
            CancellationToken ct)
        {
            try
            {
                var progress = await _service.CancelGenerationJobAsync(jobId, req.MantenerGenerados, ct);
                return Ok(progress);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
