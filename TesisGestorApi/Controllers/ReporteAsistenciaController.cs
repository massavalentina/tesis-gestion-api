using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TesisGestorApi.Data;
using TesisGestorApi.DTOs;

namespace TesisGestorApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/reporte-asistencia")]
    public class ReporteAsistenciaController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public ReporteAsistenciaController(ApplicationDbContext db)
        {
            _db = db;
        }

        private async Task<Guid?> GetIdDocenteAsync(CancellationToken ct)
        {
            var esDocente = User.FindAll("roles").Any(c => c.Value == "Docente");
            if (!esDocente) return null;
            var idUsuarioStr = User.FindFirstValue("idUsuario");
            if (idUsuarioStr == null) return Guid.Empty;
            var idUsuario = Guid.Parse(idUsuarioStr);
            var docente = await _db.Docentes
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.IdUsuario == idUsuario, ct);
            return docente?.IdDocente ?? Guid.Empty;
        }

        // GET /api/reporte-asistencia/curso/{cursoId}?desde=yyyy-MM-dd&hasta=yyyy-MM-dd&anioLectivo=2026
        [HttpGet("curso/{cursoId:guid}")]
        public async Task<IActionResult> GetReporteCurso(
            Guid cursoId,
            [FromQuery] DateOnly? desde,
            [FromQuery] DateOnly? hasta,
            [FromQuery] int anioLectivo = 2026,
            CancellationToken ct = default)
        {
            var idDocente = await GetIdDocenteAsync(ct);
            if (idDocente.HasValue)
            {
                var tieneAcceso = await _db.EspaciosCurriculares
                    .AnyAsync(ec => ec.IdDocente == idDocente && ec.IdCurso == cursoId, ct);
                if (!tieneAcceso) return Forbid();
            }
            var estudianteIds = await _db.DetallesCursado
                .Where(dc => dc.IdCurso == cursoId && dc.Estado)
                .Select(dc => dc.IdEstudiante)
                .ToListAsync(ct);

            if (!estudianteIds.Any())
                return Ok(new { totalDiasDictados = 0, estudiantes = new List<ReporteAsistenciaItemDto>() });

            var query = _db.Asistencias
                .AsNoTracking()
                .Include(a => a.TipoManiana)
                .Include(a => a.TipoLlegadaManiana)
                .Include(a => a.TipoTarde)
                .Where(a => estudianteIds.Contains(a.EstudianteId));

            if (desde.HasValue)
                query = query.Where(a => a.Fecha >= desde.Value);
            if (hasta.HasValue)
                query = query.Where(a => a.Fecha <= hasta.Value);

            var asistencias = await query.ToListAsync(ct);

            // Total school days = distinct dates where any student in the course has a record
            var totalDiasDictados = asistencias
                .Select(a => a.Fecha)
                .Distinct()
                .Count();

            var estudiantesInfo = await _db.DetallesCursado
                .Where(dc => dc.IdCurso == cursoId && dc.Estado)
                .OrderBy(dc => dc.Estudiante.Apellido)
                .ThenBy(dc => dc.Estudiante.Nombre)
                .Select(dc => new
                {
                    dc.Estudiante.IdEstudiante,
                    dc.Estudiante.Nombre,
                    dc.Estudiante.Apellido,
                    dc.Estudiante.Documento,
                    TeaGeneral = _db.AsistenciasResumenAnual
                        .Where(r => r.IdEstudiante == dc.Estudiante.IdEstudiante && r.AnioLectivo == anioLectivo)
                        .Select(r => r.TeaGeneral)
                        .FirstOrDefault()
                })
                .ToListAsync(ct);

            var resultado = estudiantesInfo.Select(est =>
            {
                var asistenciasEst = asistencias
                    .Where(a => a.EstudianteId == est.IdEstudiante)
                    .ToList();

                // Presencias: días distintos donde el código de llegada es P, LLT, LLTE o LLTC
                var presencias = asistenciasEst
                    .Where(a =>
                    {
                        var llegada = a.TipoLlegadaManiana ?? a.TipoManiana;
                        var cod = llegada?.Codigo?.ToUpper();
                        return cod is "P" or "LLT" or "LLTE" or "LLTC";
                    })
                    .Select(a => a.Fecha)
                    .Distinct()
                    .Count();

                // Ausencias: A mañana = 1.0 (con o sin tarde), solo A tarde = 0.5
                var ausenciasPuras = asistenciasEst.Sum(a =>
                {
                    var llegada = a.TipoLlegadaManiana ?? a.TipoManiana;
                    bool ausenteM = string.Equals(llegada?.Codigo, "A", StringComparison.OrdinalIgnoreCase);
                    bool ausenteT = string.Equals(a.TipoTarde?.Codigo, "A", StringComparison.OrdinalIgnoreCase);
                    if (ausenteM) return 1.0m;
                    if (ausenteT) return 0.5m;
                    return 0m;
                });

                // Conteo de llegadas tarde por tipo
                var nLLT = asistenciasEst.Count(a =>
                {
                    var llegada = a.TipoLlegadaManiana ?? a.TipoManiana;
                    return string.Equals(llegada?.Codigo, "LLT", StringComparison.OrdinalIgnoreCase);
                });
                var nLLTE = asistenciasEst.Count(a =>
                {
                    var llegada = a.TipoLlegadaManiana ?? a.TipoManiana;
                    return string.Equals(llegada?.Codigo, "LLTE", StringComparison.OrdinalIgnoreCase);
                });
                var nLLTC = asistenciasEst.Count(a =>
                {
                    var llegada = a.TipoLlegadaManiana ?? a.TipoManiana;
                    return string.Equals(llegada?.Codigo, "LLTC", StringComparison.OrdinalIgnoreCase);
                });

                // Ausencia por LLT: sumatoria sin floor
                var ausentePorLLT = nLLT * 0.25m + nLLTE * 0.5m + nLLTC * 1.0m;

                // Inasistencias por retiro anticipado:
                // RA (cualquier turno) = 0,5 · RAE turno mañana = 1,0 · RAE turno tarde = 0,5
                var ausentePorRA = asistenciasEst.Sum(a =>
                {
                    decimal v = 0m;
                    var codigoM = a.TipoManiana?.Codigo?.ToUpper();
                    var codigoT = a.TipoTarde?.Codigo?.ToUpper();
                    if (codigoM == "RA")  v += 0.5m;
                    if (codigoM == "RAE") v += 1.0m;
                    if (codigoT == "RA")  v += 0.5m;
                    if (codigoT == "RAE") v += 0.5m;
                    return v;
                });

                // Inasistencias totales (mantenido para compatibilidad)
                var inasistencias = asistenciasEst.Sum(a => a.ValorTotalInasistencia);

                var porcentaje = totalDiasDictados > 0
                    ? Math.Round((decimal)presencias / totalDiasDictados * 100, 0)
                    : 0m;

                return new ReporteAsistenciaItemDto
                {
                    IdEstudiante = est.IdEstudiante,
                    Nombre = est.Nombre,
                    Apellido = est.Apellido,
                    Documento = est.Documento,
                    Presencias = presencias,
                    Inasistencias = inasistencias,
                    AusentePorLLT = ausentePorLLT,
                    AusentePorRA = ausentePorRA,
                    AusenciasPuras = ausenciasPuras,
                    PorcentajeAsistencia = porcentaje,
                    TeaGeneral = est.TeaGeneral
                };
            }).ToList();

            return Ok(new { totalDiasDictados, estudiantes = resultado });
        }

        // GET /api/reporte-asistencia/espacio/{idEC}?cursoId=...&desde=...&hasta=...&anioLectivo=2026
        [HttpGet("espacio/{idEC:guid}")]
        public async Task<IActionResult> GetReporteEspacio(
            Guid idEC,
            [FromQuery] Guid cursoId,
            [FromQuery] DateOnly? desde,
            [FromQuery] DateOnly? hasta,
            [FromQuery] int anioLectivo = 2026,
            CancellationToken ct = default)
        {
            var idDocente = await GetIdDocenteAsync(ct);
            if (idDocente.HasValue)
            {
                var tieneAcceso = await _db.EspaciosCurriculares
                    .AnyAsync(ec => ec.IdEC == idEC && ec.IdDocente == idDocente, ct);
                if (!tieneAcceso) return Forbid();
            }

            var ec = await _db.EspaciosCurriculares
                .AsNoTracking()
                .Include(e => e.Curricula)
                .FirstOrDefaultAsync(e => e.IdEC == idEC, ct);
            if (ec == null) return NotFound();

            var nombreEspacio = ec.Curricula.Nombre;

            var clasesDictadasQuery = _db.ClasesDictadas
                .AsNoTracking()
                .Where(cd => cd.IdEC == idEC && cd.Dictada);

            if (desde.HasValue) clasesDictadasQuery = clasesDictadasQuery.Where(cd => cd.Fecha >= desde.Value);
            if (hasta.HasValue) clasesDictadasQuery = clasesDictadasQuery.Where(cd => cd.Fecha <= hasta.Value);

            var clasesDictadas = await clasesDictadasQuery.ToListAsync(ct);
            var idsClases = clasesDictadas.Select(cd => cd.IdClaseDictada).ToList();
            var fechasClases = clasesDictadas.Select(cd => cd.Fecha).Distinct().ToList();
            int totalClasesDictadas = clasesDictadas.Count;

            var estudiantesInfo = await _db.DetallesCursado
                .Where(dc => dc.IdCurso == cursoId && dc.Estado)
                .OrderBy(dc => dc.Estudiante.Apellido)
                .ThenBy(dc => dc.Estudiante.Nombre)
                .Select(dc => new
                {
                    dc.Estudiante.IdEstudiante,
                    dc.Estudiante.Nombre,
                    dc.Estudiante.Apellido,
                    dc.Estudiante.Documento,
                    TeaGeneral = _db.AsistenciasResumenAnual
                        .Where(r => r.IdEstudiante == dc.Estudiante.IdEstudiante && r.AnioLectivo == anioLectivo)
                        .Select(r => r.TeaGeneral)
                        .FirstOrDefault()
                })
                .ToListAsync(ct);

            var estudianteIds = estudiantesInfo.Select(e => e.IdEstudiante).ToList();

            var asistenciasPorEspacio = await _db.AsistenciasPorEspacio
                .AsNoTracking()
                .Where(ae => idsClases.Contains(ae.IdClaseDictada) && estudianteIds.Contains(ae.IdEstudiante))
                .ToListAsync(ct);

            var asistenciasGenerales = fechasClases.Any()
                ? await _db.Asistencias
                    .AsNoTracking()
                    .Include(a => a.TipoManiana)
                    .Include(a => a.TipoLlegadaManiana)
                    .Where(a => estudianteIds.Contains(a.EstudianteId) && fechasClases.Contains(a.Fecha))
                    .ToListAsync(ct)
                : new List<Entities.Asistencia>();

            // Mapear IdClaseDictada → Fecha para cruzar ausencias EC con asistencia general
            var claseFechaMap = clasesDictadas.ToDictionary(cd => cd.IdClaseDictada, cd => cd.Fecha);

            var resultado = estudiantesInfo.Select(est =>
            {
                var presencias = asistenciasPorEspacio
                    .Count(ae => ae.IdEstudiante == est.IdEstudiante && ae.Presente);

                // Registros explícitos con Presente=false en este EC
                var ausenciasEC = asistenciasPorEspacio
                    .Where(ae => ae.IdEstudiante == est.IdEstudiante && !ae.Presente)
                    .ToList();

                var asistenciasEst = asistenciasGenerales
                    .Where(a => a.EstudianteId == est.IdEstudiante)
                    .ToDictionary(a => a.Fecha);

                // Clasificar cada ausencia EC según el tipo de la asistencia general de ese día
                int llegadasTarde = 0;
                int retirosAnticipados = 0;
                int ausenciasEstandar = 0;

                foreach (var ausencia in ausenciasEC)
                {
                    if (!claseFechaMap.TryGetValue(ausencia.IdClaseDictada, out var fecha)) continue;
                    if (!asistenciasEst.TryGetValue(fecha, out var asistGen))
                    {
                        // Sin registro general → ausencia estándar
                        ausenciasEstandar++;
                        continue;
                    }

                    var llegada = asistGen.TipoLlegadaManiana ?? asistGen.TipoManiana;
                    var codLlegada = llegada?.Codigo?.ToUpper();
                    var codEstado = asistGen.TipoManiana?.Codigo?.ToUpper();

                    // ANC no cuenta como ausencia (es no computable)
                    if (codLlegada == "ANC") continue;

                    if (codLlegada is "LLT" or "LLTE" or "LLTC")
                        llegadasTarde++;
                    else if (codEstado != codLlegada && codEstado is "RA" or "RAE" or "RE")
                        retirosAnticipados++;
                    else
                        ausenciasEstandar++;
                }

                // Inasistencias = total (ausencias + llegadas tarde + retiros)
                var inasistencias = ausenciasEstandar + llegadasTarde + retirosAnticipados;

                var porcentaje = totalClasesDictadas > 0
                    ? Math.Round((decimal)presencias / totalClasesDictadas * 100, 0)
                    : 0m;

                return new ReporteDocenteItemDto
                {
                    IdEstudiante = est.IdEstudiante,
                    Nombre = est.Nombre,
                    Apellido = est.Apellido,
                    Documento = est.Documento,
                    Presencias = presencias,
                    Inasistencias = inasistencias,
                    LlegadasTarde = llegadasTarde,
                    RetirosAnticipados = retirosAnticipados,
                    PorcentajeAsistencia = porcentaje,
                    TeaGeneral = est.TeaGeneral
                };
            }).ToList();

            return Ok(new { totalClasesDictadas, nombreEspacio, estudiantes = resultado });
        }

        // GET /api/reporte-asistencia/espacio/{idEC}/estudiante/{estudianteId}?desde=...&hasta=...
        [HttpGet("espacio/{idEC:guid}/estudiante/{estudianteId:guid}")]
        public async Task<IActionResult> GetDetalleEstudianteEspacio(
            Guid idEC,
            Guid estudianteId,
            [FromQuery] DateOnly? desde,
            [FromQuery] DateOnly? hasta,
            CancellationToken ct = default)
        {
            var idDocente = await GetIdDocenteAsync(ct);
            if (idDocente.HasValue)
            {
                var tieneAcceso = await _db.EspaciosCurriculares
                    .AnyAsync(ec => ec.IdEC == idEC && ec.IdDocente == idDocente, ct);
                if (!tieneAcceso) return Forbid();
            }

            var clasesQuery = _db.ClasesDictadas
                .AsNoTracking()
                .Where(cd => cd.IdEC == idEC);

            if (desde.HasValue) clasesQuery = clasesQuery.Where(cd => cd.Fecha >= desde.Value);
            if (hasta.HasValue) clasesQuery = clasesQuery.Where(cd => cd.Fecha <= hasta.Value);

            var clases = await clasesQuery.OrderByDescending(cd => cd.Fecha).ToListAsync(ct);
            var idsClases = clases.Select(cd => cd.IdClaseDictada).ToList();
            var fechasClases = clases.Select(cd => cd.Fecha).Distinct().ToList();

            var asistenciasPorEspacio = idsClases.Any()
                ? await _db.AsistenciasPorEspacio
                    .AsNoTracking()
                    .Where(ae => idsClases.Contains(ae.IdClaseDictada) && ae.IdEstudiante == estudianteId)
                    .ToListAsync(ct)
                : new List<Entities.AsistenciaPorEspacio>();

            var asistenciasGenerales = fechasClases.Any()
                ? await _db.Asistencias
                    .AsNoTracking()
                    .Include(a => a.TipoManiana)
                    .Include(a => a.TipoLlegadaManiana)
                    .Where(a => a.EstudianteId == estudianteId && fechasClases.Contains(a.Fecha))
                    .ToListAsync(ct)
                : new List<Entities.Asistencia>();

            var asisIds = asistenciasGenerales.Select(a => a.Id).ToList();
            var retiros = asisIds.Any()
                ? await _db.RetirosAnticipados
                    .AsNoTracking()
                    .Where(r => asisIds.Contains(r.IdAsistencia))
                    .ToListAsync(ct)
                : new List<Entities.RetiroAnticipado>();

            var resultado = clases.Select(clase =>
            {
                var asisEspacio = asistenciasPorEspacio
                    .FirstOrDefault(ae => ae.IdClaseDictada == clase.IdClaseDictada);
                var asisGeneral = asistenciasGenerales
                    .FirstOrDefault(a => a.Fecha == clase.Fecha);
                var retiro = asisGeneral is not null
                    ? retiros.FirstOrDefault(r => r.IdAsistencia == asisGeneral.Id)
                    : null;

                return new DetalleDocenteRegistroDto
                {
                    Fecha = clase.Fecha.ToString("yyyy-MM-dd"),
                    Dictada = clase.Dictada,
                    Presente = asisEspacio?.Presente,
                    Codigo = asisGeneral?.TipoManiana?.Codigo,
                    CodigoLlegada = asisGeneral?.TipoLlegadaManiana?.Codigo
                                    ?? asisGeneral?.TipoManiana?.Codigo,
                    HoraEntrada = asisGeneral?.HoraEntradaManana.HasValue == true
                        ? asisGeneral.HoraEntradaManana!.Value.ToString(@"hh\:mm")
                        : null,
                    HoraSalida = asisGeneral?.HoraSalidaManana.HasValue == true
                        ? asisGeneral.HoraSalidaManana!.Value.ToString(@"hh\:mm")
                        : null,
                    HoraReingreso = retiro?.HorarioReingreso.HasValue == true
                        ? retiro.HorarioReingreso!.Value.ToString(@"HH\:mm")
                        : null
                };
            }).ToList();

            return Ok(resultado);
        }

        // GET /api/reporte-asistencia/estudiante/{estudianteId}?desde=yyyy-MM-dd&hasta=yyyy-MM-dd
        [HttpGet("estudiante/{estudianteId:guid}")]
        public async Task<IActionResult> GetDetalleEstudiante(
            Guid estudianteId,
            [FromQuery] DateOnly? desde,
            [FromQuery] DateOnly? hasta,
            CancellationToken ct = default)
        {
            var idDocente = await GetIdDocenteAsync(ct);
            if (idDocente.HasValue)
            {
                var tieneAcceso = await _db.EspaciosCurriculares
                    .AnyAsync(ec => ec.IdDocente == idDocente &&
                                    ec.Curso.DetallesCursado.Any(dc => dc.IdEstudiante == estudianteId && dc.Estado), ct);
                if (!tieneAcceso) return Forbid();
            }

            var query = _db.Asistencias
                .AsNoTracking()
                .Include(a => a.TipoManiana)
                .Include(a => a.TipoLlegadaManiana)
                .Include(a => a.TipoTarde)
                .Where(a => a.EstudianteId == estudianteId);

            if (desde.HasValue)
                query = query.Where(a => a.Fecha >= desde.Value);
            if (hasta.HasValue)
                query = query.Where(a => a.Fecha <= hasta.Value);

            var raw = await query.OrderByDescending(a => a.Fecha).ToListAsync(ct);

            var registros = raw.Select(a =>
            {
                // Código de llegada: usar TipoLlegadaManiana si existe, si no TipoManiana
                var llegada = a.TipoLlegadaManiana ?? a.TipoManiana;
                var codigoLlegadaM = llegada?.Codigo ?? "-";

                // Hay retiro separado si TipoManiana difiere del código de llegada
                var tieneRetiroM = a.TipoLlegadaManianaId.HasValue
                                && a.TipoManianaId.HasValue
                                && a.TipoLlegadaManianaId != a.TipoManianaId;
                var codigoRetiroM = tieneRetiroM ? a.TipoManiana?.Codigo : null;

                return new DetalleAsistenciaEstudianteDto
                {
                    Fecha = a.Fecha,
                    CodigoManana = codigoLlegadaM,
                    CodigoRetiroManana = codigoRetiroM,
                    CodigoTarde = a.TipoTarde?.Codigo ?? "-",
                    ValorTotal = a.ValorTotalInasistencia,
                    HoraEntradaManana = a.HoraEntradaManana.HasValue
                        ? a.HoraEntradaManana.Value.ToString(@"hh\:mm")
                        : null,
                    HoraSalidaManana = a.HoraSalidaManana.HasValue
                        ? a.HoraSalidaManana.Value.ToString(@"hh\:mm")
                        : null,
                    HoraSalidaTarde = a.HoraSalidaTarde.HasValue
                        ? a.HoraSalidaTarde.Value.ToString(@"hh\:mm")
                        : null
                };
            }).ToList();

            return Ok(registros);
        }
    }
}
