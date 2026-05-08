using Microsoft.AspNetCore.Mvc;
using TesisGestorApi.DTOs.Usuario;
using TesisGestorApi.Interfaces;

namespace TesisGestorApi.Controllers
{
    [ApiController]
    [Route("api/usuario")]
    public class UsuarioController : ControllerBase
    {
        private readonly IUsuarioService _usuarioService;
        private readonly ILogger<UsuarioController> _logger;

        public UsuarioController(IUsuarioService usuarioService, ILogger<UsuarioController> logger)
        {
            _usuarioService = usuarioService;
            _logger = logger;
        }

        // POST /api/usuario
        // Alta: genera contraseña provisoria, asigna rol, crea perfil Docente/Preceptor si aplica, envía email.
        [HttpPost]
        public async Task<ActionResult<CrearUsuarioResultDto>> Crear([FromBody] CrearUsuarioDto dto)
        {
            try
            {
                var resultado = await _usuarioService.CrearAsync(dto);
                // 201 Created con la URL del recurso creado en el header Location
                return CreatedAtAction(nameof(ObtenerPorId), new { id = resultado.Usuario.IdUsuario }, resultado);
            }
            catch (ArgumentException ex)
            {
                // Rol no encontrado u otro problema de argumento
                return BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                // Email o documento duplicado
                return Conflict(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear usuario.");
                return StatusCode(500, new { error = "Error interno al crear el usuario." });
            }
        }

        // GET /api/usuario/{id}
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<UsuarioDto>> ObtenerPorId(Guid id)
        {
            try
            {
                return Ok(await _usuarioService.ObtenerPorIdAsync(id));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener usuario {Id}.", id);
                return StatusCode(500, new { error = "Error interno." });
            }
        }

        // GET /api/usuario
        [HttpGet]
        public async Task<ActionResult<List<UsuarioDto>>> ObtenerTodos()
        {
            try
            {
                return Ok(await _usuarioService.ObtenerTodosAsync());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al listar usuarios.");
                return StatusCode(500, new { error = "Error interno." });
            }
        }

        // PATCH /api/usuario/{id}/activar
        [HttpPatch("{id:guid}/activar")]
        public async Task<IActionResult> Activar(Guid id)
        {
            try
            {
                await _usuarioService.ActivarAsync(id);
                return NoContent();
            }
            catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
            catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al activar usuario {Id}.", id);
                return StatusCode(500, new { error = "Error interno." });
            }
        }

        // GET /api/usuario/verificar-email?email=...
        // Valida disponibilidad del email antes de crear el usuario.
        [HttpGet("verificar-email")]
        public async Task<IActionResult> VerificarEmail([FromQuery] string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return BadRequest(new { error = "El email es requerido." });
            bool existe = await _usuarioService.EmailExisteAsync(email);
            return existe
                ? Conflict(new { error = "Este email ya está registrado." })
                : Ok();
        }

        // GET /api/usuario/verificar-documento?documento=...
        // Valida disponibilidad del documento antes de crear el usuario.
        [HttpGet("verificar-documento")]
        public async Task<IActionResult> VerificarDocumento([FromQuery] string documento)
        {
            if (string.IsNullOrWhiteSpace(documento))
                return BadRequest(new { error = "El documento es requerido." });
            bool existe = await _usuarioService.DocumentoExisteAsync(documento);
            return existe
                ? Conflict(new { error = "Este documento ya está registrado." })
                : Ok();
        }

        // PATCH /api/usuario/{id}/desactivar
        // Baja lógica: marca Activo=false, desvincula de ECs y Cursos.
        [HttpPatch("{id:guid}/desactivar")]
        public async Task<IActionResult> Desactivar(Guid id)
        {
            try
            {
                await _usuarioService.DesactivarAsync(id);
                return NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al desactivar usuario {Id}.", id);
                return StatusCode(500, new { error = "Error interno." });
            }
        }
    }
}
