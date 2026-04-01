using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TesisGestorApi.Data;
using TesisGestorApi.DTOs;

namespace TesisGestorApi.Controllers
{
    [ApiController]
    [Route("api/reporte-asistencia")]
    public class ReporteAsistenciaController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public ReporteAsistenciaController(ApplicationDbContext db)
        {
            _db = db;
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
            var estudianteIds = await _db.DetallesCursado
                .Where(dc => dc.IdCurso == cursoId && dc.Estado)
                .Select(dc => dc.IdEstudiante)
                .ToListAsync(ct);

            if (!estudianteIds.Any())
                return Ok(new { totalDiasDictados = 0, estudiantes = new List<ReporteAsistenciaItemDto>() });

            var query = _db.Asistencias
                .AsNoTracking()
                .Include(a => a.TipoManiana)
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

                // Presencias: days with a registration that is NOT a full absence
                var presencias = asistenciasEst
                    .Count(a => a.TipoManiana != null &&
                                !string.Equals(a.TipoManiana.Codigo, "A", StringComparison.OrdinalIgnoreCase));

                // Inasistencias: sum of ValorTotalInasistencia in the period
                var inasistencias = asistenciasEst.Sum(a => a.ValorTotalInasistencia);

                // Llegadas tarde: LLT o LLTE (parciales, no implican inasistencia completa por sí solas)
                var nLLT = asistenciasEst
                    .Count(a => a.TipoManiana != null &&
                                string.Equals(a.TipoManiana.Codigo, "LLT", StringComparison.OrdinalIgnoreCase));
                var nLLTE = asistenciasEst
                    .Count(a => a.TipoManiana != null &&
                                string.Equals(a.TipoManiana.Codigo, "LLTE", StringComparison.OrdinalIgnoreCase));
                var llegadasTarde = nLLT + nLLTE;

                // Ausente por LLT: inasistencias enteras acumuladas por llegadas tardes
                // LLT = 0.25 falta, LLTE = 0.50 falta → cada 1.0 acumulada = 1 inasistencia completa
                var ausentePorLLT = (int)Math.Floor(nLLT * 0.25 + nLLTE * 0.5);

                // Retiros anticipados: RA or RAE explicitly registered
                var retirosAnticipados = asistenciasEst
                    .Count(a => a.TipoManiana != null &&
                                (string.Equals(a.TipoManiana.Codigo, "RA", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(a.TipoManiana.Codigo, "RAE", StringComparison.OrdinalIgnoreCase)));

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
                    LlegadasTarde = llegadasTarde,
                    AusentePorLLT = ausentePorLLT,
                    RetirosAnticipados = retirosAnticipados,
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
                    .Where(a => estudianteIds.Contains(a.EstudianteId) && fechasClases.Contains(a.Fecha))
                    .ToListAsync(ct)
                : new List<Entities.Asistencia>();

            var resultado = estudiantesInfo.Select(est =>
            {
                var presencias = asistenciasPorEspacio
                    .Count(ae => ae.IdEstudiante == est.IdEstudiante && ae.Presente);

                var inasistencias = totalClasesDictadas - presencias;

                var asistenciasEst = asistenciasGenerales
                    .Where(a => a.EstudianteId == est.IdEstudiante)
                    .ToList();

                var nLLT = asistenciasEst.Count(a => a.TipoManiana != null &&
                    string.Equals(a.TipoManiana.Codigo, "LLT", StringComparison.OrdinalIgnoreCase));
                var nLLTE = asistenciasEst.Count(a => a.TipoManiana != null &&
                    string.Equals(a.TipoManiana.Codigo, "LLTE", StringComparison.OrdinalIgnoreCase));
                var llegadasTarde = nLLT + nLLTE;

                var retirosAnticipados = asistenciasEst.Count(a => a.TipoManiana != null &&
                    (string.Equals(a.TipoManiana.Codigo, "RA", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(a.TipoManiana.Codigo, "RAE", StringComparison.OrdinalIgnoreCase)));

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
                    .Where(a => a.EstudianteId == estudianteId && fechasClases.Contains(a.Fecha))
                    .ToListAsync(ct)
                : new List<Entities.Asistencia>();

            var resultado = clases.Select(clase =>
            {
                var asisEspacio = asistenciasPorEspacio
                    .FirstOrDefault(ae => ae.IdClaseDictada == clase.IdClaseDictada);
                var asisGeneral = asistenciasGenerales
                    .FirstOrDefault(a => a.Fecha == clase.Fecha);

                return new DetalleDocenteRegistroDto
                {
                    Fecha = clase.Fecha.ToString("yyyy-MM-dd"),
                    Dictada = clase.Dictada,
                    Presente = asisEspacio?.Presente,
                    Codigo = asisGeneral?.TipoManiana?.Codigo,
                    HoraEntrada = asisGeneral?.HoraEntradaManana.HasValue == true
                        ? asisGeneral.HoraEntradaManana!.Value.ToString(@"hh\:mm")
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
            var query = _db.Asistencias
                .AsNoTracking()
                .Include(a => a.TipoManiana)
                .Include(a => a.TipoTarde)
                .Where(a => a.EstudianteId == estudianteId);

            if (desde.HasValue)
                query = query.Where(a => a.Fecha >= desde.Value);
            if (hasta.HasValue)
                query = query.Where(a => a.Fecha <= hasta.Value);

            var registros = await query
                .OrderByDescending(a => a.Fecha)
                .Select(a => new DetalleAsistenciaEstudianteDto
                {
                    Fecha = a.Fecha,
                    CodigoManana = a.TipoManiana != null ? a.TipoManiana.Codigo : "-",
                    CodigoTarde = a.TipoTarde != null ? a.TipoTarde.Codigo : "-",
                    ValorTotal = a.ValorTotalInasistencia,
                    HoraEntradaManana = a.HoraEntradaManana.HasValue
                        ? a.HoraEntradaManana.Value.ToString(@"hh\:mm")
                        : null,
                    HoraSalidaManana = a.HoraSalidaManana.HasValue
                        ? a.HoraSalidaManana.Value.ToString(@"hh\:mm")
                        : null
                })
                .ToListAsync(ct);

            return Ok(registros);
        }
    }
}
