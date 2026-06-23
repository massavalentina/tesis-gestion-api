using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TesisGestorApi.Data;
using TesisGestorApi.DTOs;

namespace TesisGestorApi.Controllers
{
    [Authorize(Roles = "Equipo Directivo,Admin")]
    [ApiController]
    [Route("api/reportes-estrategicos")]
    public class ReportesEstrategicosController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public ReportesEstrategicosController(ApplicationDbContext db)
        {
            _db = db;
        }

        // GET /api/reportes-estrategicos/asistencia?anioLectivo=2026&desde=&hasta=&cursoId=&ecId=
        [HttpGet("asistencia")]
        public async Task<IActionResult> GetDashboard(
            [FromQuery] int anioLectivo = 2026,
            [FromQuery] DateOnly? desde = null,
            [FromQuery] DateOnly? hasta = null,
            [FromQuery] Guid? cursoId = null,
            [FromQuery] Guid? ecId = null,
            CancellationToken ct = default)
        {
            // ── 1. Cursos del año lectivo ────────────────────────────────────────
            var cursosQuery = _db.Cursos
                .AsNoTracking()
                .Include(c => c.Anio)
                .Include(c => c.Division)
                .Where(c => c.AñoLectivo.Year == anioLectivo);

            if (cursoId.HasValue)
                cursosQuery = cursosQuery.Where(c => c.IdCurso == cursoId.Value);

            var cursos = await cursosQuery.ToListAsync(ct);
            if (!cursos.Any())
                return Ok(BuildEmptyDashboard());

            var cursoIds = cursos.Select(c => c.IdCurso).ToHashSet();

            // ── 2. Estudiantes activos por curso ─────────────────────────────────
            var detallesCursado = await _db.DetallesCursado
                .AsNoTracking()
                .Where(dc => cursoIds.Contains(dc.IdCurso) && dc.Estado)
                .Select(dc => new { dc.IdCurso, dc.IdEstudiante })
                .ToListAsync(ct);

            var estudianteIds = detallesCursado.Select(dc => dc.IdEstudiante).Distinct().ToHashSet();
            if (!estudianteIds.Any())
                return Ok(BuildEmptyDashboard());

            // ── 3. Asistencias generales ─────────────────────────────────────────
            var asistQuery = _db.Asistencias
                .AsNoTracking()
                .Include(a => a.TipoManiana)
                .Include(a => a.TipoLlegadaManiana)
                .Include(a => a.TipoTarde)
                .Where(a => estudianteIds.Contains(a.EstudianteId));

            if (desde.HasValue)
                asistQuery = asistQuery.Where(a => a.Fecha >= desde.Value);
            if (hasta.HasValue)
                asistQuery = asistQuery.Where(a => a.Fecha <= hasta.Value);

            var asistencias = await asistQuery.ToListAsync(ct);

            // ── 4. KPIs generales ────────────────────────────────────────────────
            var totalRegistros = asistencias.Count;

            var presenciasGenerales = asistencias.Count(a =>
            {
                var llegada = a.TipoLlegadaManiana ?? a.TipoManiana;
                if (llegada == null) return false;
                var codigo = llegada.Codigo?.ToUpper();
                return codigo != "A" && codigo != "ANC";
            });

            var porcentajeAsistenciaGeneral = totalRegistros > 0
                ? Math.Round((decimal)presenciasGenerales / totalRegistros * 100, 1)
                : 0m;

            var llegadasTarde = asistencias.Count(a =>
            {
                var llegada = a.TipoLlegadaManiana ?? a.TipoManiana;
                var codigo = llegada?.Codigo?.ToUpper();
                return codigo is "LLT" or "LLTE" or "LLTC";
            });

            var porcentajeLlegadasTarde = presenciasGenerales > 0
                ? Math.Round((decimal)llegadasTarde / presenciasGenerales * 100, 1)
                : 0m;

            var retirosAnticipados = asistencias.Count(a =>
            {
                var codigoM = a.TipoManiana?.Codigo?.ToUpper();
                var codigoT = a.TipoTarde?.Codigo?.ToUpper();
                return codigoM is "RA" or "RAE" or "RE" ||
                       codigoT is "RA" or "RAE" or "RE";
            });

            var porcentajeRetiros = presenciasGenerales > 0
                ? Math.Round((decimal)retirosAnticipados / presenciasGenerales * 100, 1)
                : 0m;

            var alumnosTeaGeneral = await _db.AsistenciasResumenAnual
                .AsNoTracking()
                .CountAsync(r => estudianteIds.Contains(r.IdEstudiante)
                              && r.AnioLectivo == anioLectivo
                              && r.FaltasAcumuladas >= 25, ct);

            // ── 5. Promedio inasistencias por curso + tendencia ──────────────────
            var estPorCurso = detallesCursado
                .GroupBy(dc => dc.IdCurso)
                .ToDictionary(g => g.Key, g => g.Select(dc => dc.IdEstudiante).ToHashSet());

            var asistPorEstudiante = asistencias
                .GroupBy(a => a.EstudianteId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var inasistenciasPorCurso = new List<CursoInasistenciaDto>();
            var tendenciaMensual = new List<TendenciaMensualDto>();

            foreach (var curso in cursos)
            {
                var label = $"{curso.Anio.Numero}{curso.Division.Nombre}";
                if (!estPorCurso.TryGetValue(curso.IdCurso, out var estIds) || estIds.Count == 0)
                    continue;

                decimal totalInasistencias = 0m;
                var inasistMesCurso = new decimal[10]; // Marzo(0)..Diciembre(9)
                bool[] mesTieneDatos = new bool[10];

                foreach (var estId in estIds)
                {
                    if (!asistPorEstudiante.TryGetValue(estId, out var asisEst)) continue;
                    foreach (var a in asisEst)
                    {
                        totalInasistencias += a.ValorTotalInasistencia;
                        var idx = a.Fecha.Month - 3;
                        if (idx >= 0 && idx < 10)
                        {
                            inasistMesCurso[idx] += a.ValorTotalInasistencia;
                            mesTieneDatos[idx] = true;
                        }
                    }
                }

                inasistenciasPorCurso.Add(new CursoInasistenciaDto
                {
                    Curso = label,
                    Promedio = Math.Round(totalInasistencias / estIds.Count, 1)
                });

                var valoresMensuales = new List<decimal?>();
                for (int i = 0; i < 10; i++)
                    valoresMensuales.Add(mesTieneDatos[i]
                        ? Math.Round(inasistMesCurso[i] / estIds.Count, 2)
                        : null);

                tendenciaMensual.Add(new TendenciaMensualDto
                {
                    Curso = label,
                    ValoresMensuales = valoresMensuales
                });
            }

            var totalInasistenciasGlobal = asistencias.Sum(a => a.ValorTotalInasistencia);
            var promedioGeneralInasistencias = estudianteIds.Count > 0
                ? Math.Round(totalInasistenciasGlobal / estudianteIds.Count, 1)
                : 0m;

            // ── 6. Asistencia por EC ─────────────────────────────────────────────
            var ecsQuery = _db.EspaciosCurriculares
                .AsNoTracking()
                .Include(ec => ec.Curricula)
                .Where(ec => cursoIds.Contains(ec.IdCurso));

            if (ecId.HasValue)
                ecsQuery = ecsQuery.Where(ec => ec.IdEC == ecId.Value);

            var ecs = await ecsQuery.ToListAsync(ct);
            var ecIds = ecs.Select(ec => ec.IdEC).ToHashSet();

            // Traer clases dictadas con join en DB en vez de IN con miles de GUIDs
            var clasesDictadasQuery = _db.ClasesDictadas
                .AsNoTracking()
                .Where(cd => ecIds.Contains(cd.IdEC) && cd.Dictada);

            if (desde.HasValue)
                clasesDictadasQuery = clasesDictadasQuery.Where(cd => cd.Fecha >= desde.Value);
            if (hasta.HasValue)
                clasesDictadasQuery = clasesDictadasQuery.Where(cd => cd.Fecha <= hasta.Value);

            var clasesDictadas = await clasesDictadasQuery.ToListAsync(ct);

            // Indexar clases por EC para acceso O(1)
            var clasesPorEc = clasesDictadas
                .GroupBy(cd => cd.IdEC)
                .ToDictionary(g => g.Key, g => g.ToList());

            var idsClases = clasesDictadas.Select(cd => cd.IdClaseDictada).ToHashSet();

            // Traer AsistenciasPorEspacio con join por EC en DB
            var asistenciasPorEspacio = idsClases.Any()
                ? await _db.AsistenciasPorEspacio
                    .AsNoTracking()
                    .Where(ae => ae.ClaseDictada != null
                              && ecIds.Contains(ae.ClaseDictada.IdEC)
                              && ae.ClaseDictada.Dictada
                              && estudianteIds.Contains(ae.IdEstudiante))
                    .ToListAsync(ct)
                : new List<Entities.AsistenciaPorEspacio>();

            // Indexar por IdClaseDictada para acceso rápido
            var aesPorClase = asistenciasPorEspacio
                .GroupBy(ae => ae.IdClaseDictada)
                .ToDictionary(g => g.Key, g => g.ToList());

            // Mapear IdClaseDictada → Fecha
            var claseFechaMap = clasesDictadas.ToDictionary(cd => cd.IdClaseDictada, cd => cd.Fecha);

            // Asistencias generales indexadas por (estudianteId, fecha)
            var asistenciasGeneralMap = asistencias
                .GroupBy(a => (a.EstudianteId, a.Fecha))
                .ToDictionary(g => g.Key, g => g.First());

            var asistenciaPorEC = new List<EcAsistenciaDto>();
            var estudiantesConBajaAsistencia = new HashSet<Guid>();
            int totalAusenciasEC = 0, totalLlegadasTardeEC = 0, totalRetirosEC = 0;

            foreach (var ec in ecs)
            {
                if (!clasesPorEc.TryGetValue(ec.IdEC, out var clasesEc) || clasesEc.Count == 0)
                    continue;

                var totalClases = clasesEc.Count;
                var idsClasesEc = clasesEc.Select(cd => cd.IdClaseDictada).ToHashSet();

                var estCurso = estPorCurso.TryGetValue(ec.IdCurso, out var ests)
                    ? ests : new HashSet<Guid>();
                if (estCurso.Count == 0) continue;

                // Contar presencias y calcular % usando el índice por clase
                int presenciasEc = 0;
                // Para TEA: presencias por estudiante
                var presenciasPorEst = new Dictionary<Guid, int>();

                foreach (var claseId in idsClasesEc)
                {
                    if (!aesPorClase.TryGetValue(claseId, out var aes)) continue;
                    foreach (var ae in aes)
                    {
                        if (!estCurso.Contains(ae.IdEstudiante)) continue;
                        if (ae.Presente)
                        {
                            presenciasEc++;
                            presenciasPorEst[ae.IdEstudiante] =
                                presenciasPorEst.GetValueOrDefault(ae.IdEstudiante) + 1;
                        }
                    }
                }

                var totalPosibles = totalClases * estCurso.Count;
                var porcentaje = totalPosibles > 0
                    ? Math.Round((decimal)presenciasEc / totalPosibles * 100, 1)
                    : 0m;

                var cursoEc = cursos.FirstOrDefault(c => c.IdCurso == ec.IdCurso);
                var labelCurso = cursoEc != null ? $" {cursoEc.Anio.Numero}{cursoEc.Division.Nombre}" : "";

                asistenciaPorEC.Add(new EcAsistenciaDto
                {
                    NombreEC = $"{ec.Curricula.Nombre}{labelCurso}",
                    PorcentajeAsistencia = porcentaje
                });

                // TEA por EC: alumnos con <75% asistencia
                foreach (var estId in estCurso)
                {
                    var presEst = presenciasPorEst.GetValueOrDefault(estId);
                    var pctEst = totalClases > 0 ? (decimal)presEst / totalClases * 100 : 0m;
                    if (pctEst < 75m)
                        estudiantesConBajaAsistencia.Add(estId);
                }

                // Distribución de inasistencias por EC: solo Presente=false
                foreach (var claseId in idsClasesEc)
                {
                    if (!aesPorClase.TryGetValue(claseId, out var aes)) continue;
                    if (!claseFechaMap.TryGetValue(claseId, out var fecha)) continue;

                    foreach (var ae in aes)
                    {
                        if (ae.Presente || !estCurso.Contains(ae.IdEstudiante)) continue;

                        if (!asistenciasGeneralMap.TryGetValue((ae.IdEstudiante, fecha), out var asistGen))
                        {
                            totalAusenciasEC++;
                            continue;
                        }

                        var llegada = asistGen.TipoLlegadaManiana ?? asistGen.TipoManiana;
                        var codLlegada = llegada?.Codigo?.ToUpper();
                        var codEstado = asistGen.TipoManiana?.Codigo?.ToUpper();

                        if (codLlegada == "ANC") continue;

                        if (codLlegada is "LLT" or "LLTE" or "LLTC")
                            totalLlegadasTardeEC++;
                        else if (codEstado != codLlegada && codEstado is "RA" or "RAE" or "RE")
                            totalRetirosEC++;
                        else
                            totalAusenciasEC++;
                    }
                }
            }

            var porcentajeAsistenciaPorEC = asistenciaPorEC.Any()
                ? Math.Round(asistenciaPorEC.Average(e => e.PorcentajeAsistencia), 1)
                : 0m;

            var alumnosTeaPorEspacio = estudiantesConBajaAsistencia.Count;

            // ── 7. Distribución de inasistencias (por valor, ambos turnos) ───────
            decimal sumaAusencias = 0m, sumaLlegadasTarde = 0m, sumaRetiros = 0m;

            foreach (var a in asistencias)
            {
                if (a.ValorTotalInasistencia == 0) continue;

                var llegadaM = a.TipoLlegadaManiana ?? a.TipoManiana;
                var codigoLlegadaM = llegadaM?.Codigo?.ToUpper();
                var codigoEstadoM = a.TipoManiana?.Codigo?.ToUpper();
                var codigoT = a.TipoTarde?.Codigo?.ToUpper();

                bool tardeGeneraInasistencia = codigoT != null
                    && codigoT != "P" && codigoT != "RE" && codigoT != "ANC";
                decimal valorTarde = tardeGeneraInasistencia ? 0.5m : 0m;
                decimal valorManiana = Math.Max(0m, a.ValorTotalInasistencia - valorTarde);
                if (valorManiana + valorTarde > a.ValorTotalInasistencia)
                    valorTarde = a.ValorTotalInasistencia - valorManiana;

                if (codigoLlegadaM == "A" || codigoLlegadaM == "ANC")
                {
                    sumaAusencias += valorManiana;
                }
                else
                {
                    decimal valorLlegada = codigoLlegadaM switch
                    {
                        "LLT"  => 0.25m,
                        "LLTE" => 0.50m,
                        "LLTC" => 1.00m,
                        _      => 0m,
                    };
                    sumaLlegadasTarde += Math.Min(valorLlegada, valorManiana);

                    var restoManiana = valorManiana - Math.Min(valorLlegada, valorManiana);
                    if (restoManiana > 0 && codigoEstadoM != codigoLlegadaM
                        && codigoEstadoM is "RA" or "RAE" or "RE")
                    {
                        sumaRetiros += restoManiana;
                    }
                }

                if (valorTarde > 0 && tardeGeneraInasistencia)
                {
                    if (codigoT == "A")
                        sumaAusencias += valorTarde;
                    else if (codigoT is "LLT" or "LLTE" or "LLTC")
                        sumaLlegadasTarde += valorTarde;
                    else if (codigoT is "RA" or "RAE")
                        sumaRetiros += valorTarde;
                }
            }

            // ── 8. Distribución por subtipo ──────────────────────────────────────
            var subtipos = new DistribucionSubtiposDto();
            foreach (var a in asistencias)
            {
                var codLlegM = (a.TipoLlegadaManiana ?? a.TipoManiana)?.Codigo?.ToUpper();
                if (codLlegM == "LLT") subtipos.LLT++;
                else if (codLlegM == "LLTE") subtipos.LLTE++;
                else if (codLlegM == "LLTC") subtipos.LLTC++;

                var codEstM = a.TipoManiana?.Codigo?.ToUpper();
                if (codEstM != codLlegM)
                {
                    if (codEstM == "RE") subtipos.RE++;
                    else if (codEstM == "RA") subtipos.RA++;
                    else if (codEstM == "RAE") subtipos.RAE++;
                }

                var codT = a.TipoTarde?.Codigo?.ToUpper();
                if (codT == "LLT") subtipos.LLT++;
                else if (codT == "LLTE") subtipos.LLTE++;
                else if (codT == "LLTC") subtipos.LLTC++;
                else if (codT == "RE") subtipos.RE++;
                else if (codT == "RA") subtipos.RA++;
                else if (codT == "RAE") subtipos.RAE++;
            }

            // ── 9. Respuesta ─────────────────────────────────────────────────────
            return Ok(new DashboardAsistenciaDto
            {
                PorcentajeAsistenciaGeneral = porcentajeAsistenciaGeneral,
                PorcentajeAsistenciaPorEC = porcentajeAsistenciaPorEC,
                PromedioInasistenciasPorCurso = promedioGeneralInasistencias,
                PorcentajeLlegadasTarde = porcentajeLlegadasTarde,
                PorcentajeRetirosAnticipados = porcentajeRetiros,
                AlumnosTeaGeneral = alumnosTeaGeneral,
                AlumnosTeaPorEspacio = alumnosTeaPorEspacio,
                InasistenciasPorCurso = inasistenciasPorCurso,
                AsistenciaPorEC = asistenciaPorEC,
                TendenciaMensual = tendenciaMensual,
                DistribucionInasistencias = new DistribucionInasistenciasDto
                {
                    Ausentes = Math.Round(sumaAusencias, 2),
                    LlegadasTarde = Math.Round(sumaLlegadasTarde, 2),
                    RetirosAnticipados = Math.Round(sumaRetiros, 2)
                },
                DistribucionSubtipos = subtipos,
                DistribucionInasistenciasEC = new DistribucionInasistenciasECDto
                {
                    Ausencias = totalAusenciasEC,
                    LlegadasTarde = totalLlegadasTardeEC,
                    RetirosAnticipados = totalRetirosEC,
                },
            });
        }

        private static DashboardAsistenciaDto BuildEmptyDashboard() => new()
        {
            DistribucionInasistencias = new DistribucionInasistenciasDto(),
            DistribucionSubtipos = new DistribucionSubtiposDto(),
            DistribucionInasistenciasEC = new DistribucionInasistenciasECDto(),
        };
    }
}
