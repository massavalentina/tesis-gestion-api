using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TesisGestorApi.Data;
using TesisGestorApi.DTOs.Calendario;
using TesisGestorApi.Entities;
using TesisGestorApi.Interfaces;

namespace TesisGestorApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/calendario")]
    public class CalendarioController : ControllerBase
    {
        private readonly ICalendarioInstitucionalService _service;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CalendarioController> _logger;

        private static readonly Dictionary<int, string> TipoEventoLabels = new()
        {
            { 1, "Evento Institucional" },
            { 2, "Evento Extraordinario" },
            { 3, "Evento Festivo" },
            { 4, "Feriado" },
            { 5, "Período de Clases" },
            { 6, "Período de Evaluación" },
        };

        public CalendarioController(
            ICalendarioInstitucionalService service,
            ApplicationDbContext context,
            ILogger<CalendarioController> logger)
        {
            _service = service;
            _context = context;
            _logger = logger;
        }

        // GET /api/calendario/eventos?anioLectivo=2026
        [HttpGet("eventos")]
        public async Task<IActionResult> ObtenerEventos(
            [FromQuery] int anioLectivo = 2026,
            CancellationToken ct = default)
        {
            try
            {
                var (idUsuario, roles, esAdmin) = ObtenerDatosUsuario();
                var eventos = await _service.ObtenerEventosAsync(anioLectivo, idUsuario, roles, esAdmin, ct);
                return Ok(eventos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener eventos del calendario.");
                return StatusCode(500, "Error interno al obtener los eventos.");
            }
        }

        // GET /api/calendario/eventos/{id}
        [HttpGet("eventos/{id:guid}")]
        public async Task<IActionResult> ObtenerEvento(Guid id, CancellationToken ct = default)
        {
            try
            {
                var evento = await _service.ObtenerPorIdAsync(id, ct);
                return Ok(evento);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener el evento {Id}.", id);
                return StatusCode(500, "Error interno al obtener el evento.");
            }
        }

        // POST /api/calendario/eventos
        [HttpPost("eventos")]
        public async Task<IActionResult> CrearEvento(
            [FromBody] CrearEventoInstitucionalDto dto,
            CancellationToken ct = default)
        {
            if (!PuedeGestionarEventos())
                return Forbid();

            try
            {
                var evento = await _service.CrearEventoAsync(dto, ct);
                return Ok(evento);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear evento.");
                return StatusCode(500, "Error interno al crear el evento.");
            }
        }

        // PUT /api/calendario/eventos/{id}
        [HttpPut("eventos/{id:guid}")]
        public async Task<IActionResult> ActualizarEvento(
            Guid id,
            [FromBody] ActualizarEventoInstitucionalDto dto,
            CancellationToken ct = default)
        {
            if (!PuedeGestionarEventos())
                return Forbid();

            try
            {
                var evento = await _service.ActualizarEventoAsync(id, dto, ct);
                return Ok(evento);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar evento {Id}.", id);
                return StatusCode(500, "Error interno al actualizar el evento.");
            }
        }

        // DELETE /api/calendario/eventos/{id}
        [HttpDelete("eventos/{id:guid}")]
        public async Task<IActionResult> EliminarEvento(Guid id, CancellationToken ct = default)
        {
            if (!PuedeGestionarEventos())
                return Forbid();

            try
            {
                await _service.EliminarEventoAsync(id, ct);
                return NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar evento {Id}.", id);
                return StatusCode(500, "Error interno al eliminar el evento.");
            }
        }

        // GET /api/calendario/eventos/{id}/auditoria
        [HttpGet("eventos/{id:guid}/auditoria")]
        public async Task<IActionResult> ObtenerAuditoriaEvento(Guid id, CancellationToken ct = default)
        {
            try
            {
                var auditoria = await _service.ObtenerAuditoriaEventoAsync(id, ct);
                return Ok(auditoria);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener auditoría del evento {Id}.", id);
                return StatusCode(500, "Error interno al obtener la auditoría.");
            }
        }

        // GET /api/calendario/auditoria?anioLectivo=2026
        [HttpGet("auditoria")]
        public async Task<IActionResult> ObtenerAuditoriaGeneral(
            [FromQuery] int anioLectivo = 2026,
            CancellationToken ct = default)
        {
            try
            {
                var auditoria = await _service.ObtenerAuditoriaGeneralAsync(anioLectivo, ct);
                return Ok(auditoria);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener auditoría general.");
                return StatusCode(500, "Error interno al obtener la auditoría.");
            }
        }

        // GET /api/calendario/tipos-evento
        [HttpGet("tipos-evento")]
        public IActionResult ObtenerTiposEvento()
        {
            var tipos = TipoEventoLabels.Select(t => new { id = t.Key, label = t.Value }).ToList();
            return Ok(tipos);
        }

        // GET /api/calendario/cursos?anioLectivo=2026
        [HttpGet("cursos")]
        public async Task<IActionResult> ObtenerCursos(
            [FromQuery] int anioLectivo = 2026,
            CancellationToken ct = default)
        {
            try
            {
                var (idUsuario, roles, esAdmin) = ObtenerDatosUsuario();
                var cursos = await _service.ObtenerCursosUsuarioAsync(anioLectivo, idUsuario, roles, esAdmin, ct);
                return Ok(cursos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener cursos para el calendario.");
                return StatusCode(500, "Error interno al obtener los cursos.");
            }
        }

        private bool PuedeGestionarEventos()
        {
            var roles = User.FindAll("roles").Select(c => c.Value).ToList();
            var esAdmin = User.FindFirstValue("es_admin") == "true";
            return esAdmin
                || roles.Contains("Equipo Directivo")
                || roles.Contains("Secretario");
        }

        private (Guid? idUsuario, List<string> roles, bool esAdmin) ObtenerDatosUsuario()
        {
            var idUsuarioStr = User.FindFirstValue("idUsuario");
            var idUsuario = idUsuarioStr != null ? Guid.Parse(idUsuarioStr) : (Guid?)null;
            var roles = User.FindAll("roles").Select(c => c.Value).ToList();
            var esAdmin = User.FindFirstValue("es_admin") == "true";
            return (idUsuario, roles, esAdmin);
        }
    }
}
