using Microsoft.AspNetCore.Mvc;
using TesisGestorApi.Dtos;
using TesisGestorApi.Services;
using TesisGestorApi.Exceptions;

[ApiController]
[Route("api/attendance")]
public class AttendanceController : ControllerBase
{
    private readonly AttendanceService _attendanceService;

    public AttendanceController(AttendanceService attendanceService)
    {
        _attendanceService = attendanceService;
    }

    [HttpPost("scan")]
    public async Task<IActionResult> ScanAttendance([FromBody] AttendanceScanRequest request)
    {
        try
        {
            var result = await _attendanceService.ScanAsync(request);
            return Ok(result);
        }
        catch (AttendanceException ex)
        {
            return Conflict(new
            {
                code = ex.Code,
                message = ex.Message
            });
        }
    }

    [HttpPost("preview")]
    public async Task<IActionResult> PreviewAttendance([FromBody] AttendancePreviewRequest request)
    {
        try
        {
            var result = await _attendanceService.PreviewAsync(request);
            return Ok(result);
        }
        catch (AttendanceException ex)
        {
            return Conflict(new
            {
                code = ex.Code,
                message = ex.Message
            });
        }
    }


    [HttpPost("confirm")]
    public async Task<IActionResult> ConfirmAttendance([FromBody] AttendanceConfirmRequest request)
    {
        try
        {
            await _attendanceService.ConfirmAsync(request);
            return Ok();
        }
        catch (AttendanceException ex)
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
        => Ok(await _attendanceService.GetCursosAsync());

    [HttpGet("turnos")]
    public IActionResult GetTurnos()
        => Ok(_attendanceService.GetTurnos());

    [HttpGet("tipos-asistencia")]
    public async Task<IActionResult> GetTiposAsistencia()
        => Ok(await _attendanceService.GetTiposAsistenciaAsync());


}
