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
                await _scannerService.ConfirmarAsync(request);
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

        [HttpGet("cursosscanner")]
        public async Task<IActionResult> GetCursos()
            => Ok(await _scannerService.ObtenerCursosScannerAsync());

        [HttpGet("turnos")]
        public IActionResult GetTurnos()
            => Ok(_scannerService.ObtenerTurnos());

        [HttpGet("tipos-asistencia")]
        public async Task<IActionResult> GetTiposAsistencia()
            => Ok(await _scannerService.ObtenerTiposAsistenciaAsync());


    }
}
