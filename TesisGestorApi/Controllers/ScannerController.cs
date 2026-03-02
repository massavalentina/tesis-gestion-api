using Microsoft.AspNetCore.Mvc;
using TesisGestorApi.Dtos;
using TesisGestorApi.Exceptions;
using TesisGestorApi.Interfaces;

[ApiController]
[Route("api/asistencia/scanner")]
public class ScannerController : ControllerBase
{
    private readonly IAsistenciaService _asistenciaService;

    public ScannerController(IAsistenciaService asistenciaService)
    {
        _asistenciaService = asistenciaService;
    }

    [HttpPost("preview")]
    public async Task<IActionResult> PreviewScaneo([FromBody] PrevisualizarAsistenciaRequest request)
    {
        try
        {
            var result = await _asistenciaService.PrevisualizarAsync(request);
            return Ok(result);
        }
        catch (AsistenciaException ex)
        {
            return Conflict(new
            {
                code = ex.Code,
                message = ex.Message
            });
        }
    }


    [HttpPost("confirm")]
    public async Task<IActionResult> ConfirmarAsistenciaScan([FromBody] ConfirmarAsistenciaRequest request)
    {
        try
        {
            await _asistenciaService.ConfirmarAsync(request);
            return Ok();
        }
        catch (AsistenciaException ex)
        {
            return Conflict(new
            {
                code = ex.Code,
                message = ex.Message
            });
        }
    }

    [HttpGet("cursos")]
    public async Task<IActionResult> GetCursos()
        => Ok(await _asistenciaService.ObtenerCursosAsync());

    [HttpGet("turnos")]
    public IActionResult GetTurnos()
        => Ok(_asistenciaService.ObtenerTurnos());

    [HttpGet("tipos-asistencia")]
    public async Task<IActionResult> GetTiposAsistencia()
        => Ok(await _asistenciaService.ObtenerTiposAsistenciaAsync());


}
