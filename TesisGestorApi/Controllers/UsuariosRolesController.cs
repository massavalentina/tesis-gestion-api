using Microsoft.AspNetCore.Mvc;
using TesisGestorApi.DTOs.Usuarios;
using TesisGestorApi.Interfaces;

[ApiController]
[Route("api/usuarios-roles")]
public class UsuariosRolesController : ControllerBase
{
    private readonly IUsuariosRolesService _service;

    public UsuariosRolesController(IUsuariosRolesService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<List<UsuarioConRolesDto>>> GetUsuarios(CancellationToken ct)
    {
        var usuarios = await _service.GetUsuariosConRolesAsync(ct);
        return Ok(usuarios);
    }

    [HttpGet("roles")]
    public async Task<ActionResult<List<RolDto>>> GetRoles(CancellationToken ct)
    {
        var roles = await _service.GetRolesDisponiblesAsync(ct);
        return Ok(roles);
    }

    [HttpPost("{idUsuario:guid}/roles/{idRol:guid}")]
    public async Task<IActionResult> AsignarRol(Guid idUsuario, Guid idRol, CancellationToken ct)
    {
        await _service.AsignarRolAsync(idUsuario, idRol, ct);
        return NoContent();
    }

    [HttpDelete("{idUsuario:guid}/roles/{idRol:guid}")]
    public async Task<IActionResult> QuitarRol(Guid idUsuario, Guid idRol, CancellationToken ct)
    {
        await _service.QuitarRolAsync(idUsuario, idRol, ct);
        return NoContent();
    }

    [HttpPatch("{idUsuario:guid}/preceptor-delegado")]
    public async Task<IActionResult> ActualizarDelegado(
        Guid idUsuario,
        [FromBody] ActualizarDelegadoDto dto,
        CancellationToken ct)
    {
        await _service.ActualizarDelegadoAsync(idUsuario, dto.EsDelegado, ct);
        return NoContent();
    }
}
