using Microsoft.AspNetCore.Mvc;
using TesisGestorApi.Dtos;
using TesisGestorApi.Exceptions;
using TesisGestorApi.Interfaces;

namespace TesisGestorApi.Controllers
{
    [ApiController]
    [Route("api/asistencia/scanner")]
    public class ScannerController : ControllerBase
    {
        private readonly IScannerService _scannerService;

        public ScannerController(IScannerService scannerService)
        {
            _scannerService = scannerService;
        }

        [HttpPost("preview")]
        public async Task<IActionResult> PreviewScaneo([FromBody] PrevisualizarAsistenciaRequest request)
        {
            try
            {
                var result = await _scannerService.PrevisualizarAsync(request);
                return Ok(result);
            }
            catch (AsistenciaException ex)
            {
                return Conflict(CrearErrorRespuesta(ex));
            }
        }


        [HttpPost("confirm")]
        public async Task<IActionResult> ConfirmarAsistenciaScan([FromBody] ConfirmarAsistenciaRequest request)
        {
            try
            {
                await _scannerService.ConfirmarAsync(request);
                return Ok();
            }
            catch (AsistenciaException ex)
            {
                return Conflict(CrearErrorRespuesta(ex));
            }
        }

        [HttpGet("session-turno")]
        public IActionResult GetTurnoSesion([FromQuery] string? turno)
            => Ok(_scannerService.ObtenerTurnoSesion(turno));

        [HttpGet("turnos")]
        public IActionResult GetTurnos()
            => Ok(_scannerService.ObtenerTurnos());

        [HttpGet("tipos-asistencia")]
        public async Task<IActionResult> GetTiposAsistencia()
            => Ok(await _scannerService.ObtenerTiposAsistenciaAsync());

        private static object CrearErrorRespuesta(AsistenciaException ex)
            => ex.Details is null
                ? new { code = ex.Code, message = ex.Message }
                : new { code = ex.Code, message = ex.Message, details = ex.Details };

    }
}
