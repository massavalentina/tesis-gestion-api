using Microsoft.AspNetCore.Mvc;
using TesisGestorApi.DTOs;
using TesisGestorApi.Interfaces;
using TesisGestorApi.Services;
using Microsoft.Extensions.DependencyInjection;

namespace TesisGestorApi.Controllers
{
    [ApiController]
    [Route("api/qr-email")]
    public class QrEmailController : ControllerBase
    {
        private readonly IQrEmailService _service;
        private readonly IEmailSender _emailSender;
        private readonly QrEmailProgressStore _progressStore;
        public QrEmailController(IQrEmailService service, IEmailSender emailSender, QrEmailProgressStore progressStore)
        {
            _service = service;
            _emailSender = emailSender;
            _progressStore = progressStore;
        }

        [HttpPost("resumen")]
        public async Task<ActionResult<QrEmailResumenDto>> Resumen([FromBody] QrEmailResumenRequestDto req, CancellationToken ct)
        {
            if (req.IdCurso == Guid.Empty)
                return BadRequest("IdCurso inválido.");

            var result = await _service.GetResumenAsync(req, ct);
            return Ok(result);
        }

        [HttpPost("start")]
        public async Task<ActionResult<QrEmailStartResponseDto>> Start([FromBody] QrEmailStartRequestDto req, CancellationToken ct)
        {
            if (req.IdCurso == Guid.Empty)
                return BadRequest("IdCurso inválido.");

            var result = await _service.StartEnvioAsync(req, ct);
            return Ok(result);
        }

        [HttpGet("test-mail")]
        public async Task<IActionResult> TestMail()
        {
            await _emailSender.SendAsync(
                "fhiacc@gmail.com",
                "TEST SMTP",
                "<h2>Si lees esto, funciona 🚀</h2>"
            );

            return Ok("Mail enviado");
        }

        [HttpPost("start-job")]
        public async Task<ActionResult<object>> StartJob([FromBody] QrEmailStartRequestDto req, CancellationToken ct)
        {
            if (req.IdCurso == Guid.Empty)
                return BadRequest("IdCurso inválido.");

            var job = await _service.StartEnvioJobAsync(req, ct);
            return Ok(new { jobId = job.JobId });
        }

        [HttpGet("progress/{jobId:guid}")]
        public ActionResult<QrEmailProgressDto> Progress([FromRoute] Guid jobId)
        {
            if (!_progressStore.TryGet(jobId, out var dto))
                return NotFound("Job no encontrado.");

            return Ok(dto);
        }

        [HttpGet("alumnos")]
        public async Task<IActionResult> Alumnos([FromQuery] Guid? cursoId, [FromQuery] string? estado, [FromQuery] int anioLectivo = 2026, CancellationToken ct = default)
        {
            var result = await _service.GetAlumnosEstadoAsync(cursoId, estado, anioLectivo, ct);
            return Ok(result);
        }

    }
}
