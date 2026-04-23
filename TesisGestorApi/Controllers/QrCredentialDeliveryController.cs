using Microsoft.AspNetCore.Mvc;
using TesisGestorApi.DTOs;
using TesisGestorApi.Interfaces;
using TesisGestorApi.Services;

namespace TesisGestorApi.Controllers
{
    [ApiController]
    [Route("api/qr-credentials/delivery")]
    public class QrCredentialDeliveryController : ControllerBase
    {
        private readonly IQrCredentialDeliveryService _service;
        private readonly QrCredentialDeliveryProgressStore _progressStore;

        public QrCredentialDeliveryController(
            IQrCredentialDeliveryService service,
            QrCredentialDeliveryProgressStore progressStore)
        {
            _service = service;
            _progressStore = progressStore;
        }

        [HttpGet("summary")]
        public async Task<ActionResult<QrCredentialDeliverySummaryDto>> Summary(
            [FromQuery] Guid cursoId,
            [FromQuery] string? alcance,
            CancellationToken ct)
        {
            if (cursoId == Guid.Empty)
                return BadRequest("IdCurso inválido.");

            try
            {
                var result = await _service.GetSummaryAsync(cursoId, alcance, ct);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("start-job")]
        public async Task<ActionResult<object>> StartJob([FromBody] QrCredentialDeliveryRequestDto req, CancellationToken ct)
        {
            if (req.IdCurso == Guid.Empty)
                return BadRequest("IdCurso inválido.");

            try
            {
                var job = await _service.StartDeliveryJobAsync(req, ct);
                return Ok(new { jobId = job.JobId });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("progress/{jobId:guid}")]
        public ActionResult<QrCredentialDeliveryProgressDto> Progress([FromRoute] Guid jobId)
        {
            if (!_progressStore.TryGet(jobId, out var dto))
                return NotFound("Job no encontrado.");

            return Ok(dto);
        }

        [HttpGet("students")]
        public async Task<ActionResult<QrCredentialDeliveryStudentsPageDto>> Students(
            [FromQuery] Guid cursoId,
            [FromQuery] string? estado,
            [FromQuery] string? busqueda,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken ct = default)
        {
            if (cursoId == Guid.Empty)
                return BadRequest("IdCurso inválido.");

            try
            {
                var result = await _service.GetStudentsPageAsync(cursoId, estado, busqueda, page, pageSize, ct);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("student/{estudianteId:guid}/send")]
        public async Task<ActionResult<QrCredentialDeliverySingleResponseDto>> SendStudent(
            [FromRoute] Guid estudianteId,
            [FromBody] QrCredentialDeliverySingleRequestDto req,
            CancellationToken ct)
        {
            if (estudianteId == Guid.Empty)
                return BadRequest("IdEstudiante inválido.");

            if (req.IdCurso == Guid.Empty)
                return BadRequest("IdCurso inválido.");

            try
            {
                var result = await _service.SendStudentAsync(estudianteId, req, ct);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("student/{estudianteId:guid}/qr-image")]
        public async Task<IActionResult> StudentQrImage([FromRoute] Guid estudianteId, CancellationToken ct)
        {
            if (estudianteId == Guid.Empty)
                return BadRequest("IdEstudiante inválido.");

            try
            {
                var image = await _service.GetStudentQrImageAsync(estudianteId, ct);
                return File(image.Bytes, "image/png", image.FileName);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(ex.Message);
            }
        }
    }
}
