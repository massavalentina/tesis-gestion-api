using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TesisGestorApi.Data;
using TesisGestorApi.DTOs;
using TesisGestorApi.Entities;

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

        // GET /api/reportes-estrategicos/asistencia?anioLectivo=2026&desde=&hasta=&cursoId=&ecId=&turno=GENERAL
        [HttpGet("asistencia")]
        public async Task<IActionResult> GetDashboard(
            [FromQuery] int anioLectivo = 2026,
            [FromQuery] DateOnly? desde = null,
            [FromQuery] DateOnly? hasta = null,
            [FromQuery(Name = "cursoId")] List<Guid>? cursoIds = null,
            [FromQuery] Guid? ecId = null,
            [FromQuery] string turno = "GENERAL",
            CancellationToken ct = default)
        {
            // ── 1. Cursos del año lectivo ────────────────────────────────────────
            var cursosQuery = _db.Cursos
                .AsNoTracking()
                .Include(c => c.Anio)
                .Include(c => c.Division)
                .Where(c => c.AñoLectivo.Year == anioLectivo);

            if (cursoIds != null && cursoIds.Count > 0)
                cursosQuery = cursosQuery.Where(c => cursoIds.Contains(c.IdCurso));

            var cursos = await cursosQuery.ToListAsync(ct);
            if (!cursos.Any())
                return Ok(BuildEmptyDashboard());

            var cursoIdSet = cursos.Select(c => c.IdCurso).ToHashSet();

            // ── 2. Estudiantes activos por curso ─────────────────────────────────
            var detallesCursado = await _db.DetallesCursado
                .AsNoTracking()
                .Where(dc => cursoIdSet.Contains(dc.IdCurso) && dc.Estado)
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
            bool esTarde = turno.ToUpper() == "TARDE";

            // Helper: valor de inasistencia por turno tarde de un registro
            static decimal ValorTarde(Asistencia a)
            {
                var codT = a.TipoTarde?.Codigo?.ToUpper();
                if (codT == null) return 0m;
                return codT is "P" or "RE" or "ANC" ? 0m : 0.5m;
            }

            decimal porcentajeAsistenciaGeneral;
            decimal porcentajeLlegadasTarde;
            decimal porcentajeRetiros;

            if (!esTarde)
            {
                // GENERAL: combina mañana + tarde
                var nManiana = asistencias.Count(a =>
                {
                    var llegada = a.TipoLlegadaManiana ?? a.TipoManiana;
                    var codigo = llegada?.Codigo?.ToUpper();
                    return codigo != null && codigo != "A" && codigo != "ANC";
                });
                var nTarde = asistencias.Count(a =>
                {
                    var codT = a.TipoTarde?.Codigo?.ToUpper();
                    return codT != null && codT != "A" && codT != "ANC";
                });
                var numeradorAsist = nManiana + nTarde;
                // Denominador: slots con registro real (no multiplicar x2 si no hay turno tarde)
                var denominadorAsist = asistencias.Count(a => a.TipoManiana != null)
                                     + asistencias.Count(a => a.TipoTarde != null);
                porcentajeAsistenciaGeneral = denominadorAsist > 0
                    ? Math.Round((decimal)numeradorAsist / denominadorAsist * 100, 1)
                    : 0m;

                // Presencias combinadas (denominador para LLT y retiros)
                var presenciasManiana = asistencias.Count(a =>
                {
                    var llegada = a.TipoLlegadaManiana ?? a.TipoManiana;
                    var codigo = llegada?.Codigo?.ToUpper();
                    return codigo != null && codigo != "A" && codigo != "ANC";
                });
                var presenciasTardeG = asistencias.Count(a =>
                {
                    var codT = a.TipoTarde?.Codigo?.ToUpper();
                    return codT != null && codT != "A" && codT != "ANC";
                });
                var presenciasGenerales = presenciasManiana + presenciasTardeG;

                var ltManiana = asistencias.Count(a =>
                {
                    var llegada = a.TipoLlegadaManiana ?? a.TipoManiana;
                    return llegada?.Codigo?.ToUpper() is "LLT" or "LLTE" or "LLTC";
                });
                var ltTarde = asistencias.Count(a => a.TipoTarde?.Codigo?.ToUpper() is "LLT" or "LLTE" or "LLTC");
                porcentajeLlegadasTarde = presenciasGenerales > 0
                    ? Math.Round((decimal)(ltManiana + ltTarde) / presenciasGenerales * 100, 1)
                    : 0m;

                var retirosM = asistencias.Count(a => a.TipoManiana?.Codigo?.ToUpper() is "RA" or "RAE" or "RE");
                var retirosT = asistencias.Count(a => a.TipoTarde?.Codigo?.ToUpper() is "RA" or "RAE" or "RE");
                porcentajeRetiros = presenciasGenerales > 0
                    ? Math.Round((decimal)(retirosM + retirosT) / presenciasGenerales * 100, 1)
                    : 0m;
            }
            else
            {
                // TARDE
                var conTarde = asistencias.Where(a => a.TipoTarde != null).ToList();
                var denominadorTarde = conTarde.Count;

                var presenciasTarde = conTarde.Count(a =>
                {
                    var codT = a.TipoTarde?.Codigo?.ToUpper();
                    return codT != null && codT != "A" && codT != "ANC";
                });
                porcentajeAsistenciaGeneral = denominadorTarde > 0
                    ? Math.Round((decimal)presenciasTarde / denominadorTarde * 100, 1)
                    : 0m;

                var ltTardeOnly = conTarde.Count(a => a.TipoTarde?.Codigo?.ToUpper() is "LLT" or "LLTE" or "LLTC");
                porcentajeLlegadasTarde = presenciasTarde > 0
                    ? Math.Round((decimal)ltTardeOnly / presenciasTarde * 100, 1)
                    : 0m;

                var retirosTardeOnly = conTarde.Count(a => a.TipoTarde?.Codigo?.ToUpper() is "RA" or "RAE" or "RE");
                porcentajeRetiros = presenciasTarde > 0
                    ? Math.Round((decimal)retirosTardeOnly / presenciasTarde * 100, 1)
                    : 0m;
            }

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
                        var valorInasist = esTarde ? ValorTarde(a) : a.ValorTotalInasistencia;
                        totalInasistencias += valorInasist;
                        var idx = a.Fecha.Month - 3;
                        if (idx >= 0 && idx < 10)
                        {
                            inasistMesCurso[idx] += valorInasist;
                            mesTieneDatos[idx] = true;
                        }
                    }
                }

                inasistenciasPorCurso.Add(new CursoInasistenciaDto
                {
                    Curso = label,
                    Promedio = Math.Round(totalInasistencias / estIds.Count, 1)
                });

                // Tendencia acumulativa
                decimal acum = 0m;
                var valoresMensuales = new List<decimal?>();
                for (int i = 0; i < 10; i++)
                {
                    if (mesTieneDatos[i])
                    {
                        acum += inasistMesCurso[i] / estIds.Count;
                        valoresMensuales.Add(Math.Round(acum, 2));
                    }
                    else
                    {
                        valoresMensuales.Add(null);
                    }
                }

                tendenciaMensual.Add(new TendenciaMensualDto
                {
                    Curso = label,
                    ValoresMensuales = valoresMensuales
                });
            }

            decimal promedioGeneralInasistencias;
            if (!esTarde)
            {
                var totalInasistenciasGlobal = asistencias.Sum(a => a.ValorTotalInasistencia);
                promedioGeneralInasistencias = estudianteIds.Count > 0
                    ? Math.Round(totalInasistenciasGlobal / estudianteIds.Count, 1)
                    : 0m;
            }
            else
            {
                var totalInasistTarde = asistencias.Sum(ValorTarde);
                promedioGeneralInasistencias = estudianteIds.Count > 0
                    ? Math.Round(totalInasistTarde / estudianteIds.Count, 1)
                    : 0m;
            }

            // ── 6. Asistencia por EC ─────────────────────────────────────────────
            var ecsQuery = _db.EspaciosCurriculares
                .AsNoTracking()
                .Include(ec => ec.Curricula)
                .Where(ec => cursoIdSet.Contains(ec.IdCurso));

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

            if (!esTarde)
            {
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
            }
            else
            {
                foreach (var a in asistencias)
                {
                    var codT = a.TipoTarde?.Codigo?.ToUpper();
                    if (codT == null) continue;

                    if (codT == "A")
                        sumaAusencias += 0.5m;
                    else if (codT is "LLT" or "LLTE" or "LLTC")
                        sumaLlegadasTarde += 0.5m;
                    else if (codT is "RA" or "RAE")
                        sumaRetiros += 0.5m;
                }
            }

            // ── 8. Distribución por subtipo ──────────────────────────────────────
            var subtipos = new DistribucionSubtiposDto();
            if (!esTarde)
            {
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
            }
            else
            {
                foreach (var a in asistencias)
                {
                    var codT = a.TipoTarde?.Codigo?.ToUpper();
                    if (codT == null) continue;
                    if (codT == "LLT") subtipos.LLT++;
                    else if (codT == "LLTE") subtipos.LLTE++;
                    else if (codT == "LLTC") subtipos.LLTC++;
                    else if (codT == "RE") subtipos.RE++;
                    else if (codT == "RA") subtipos.RA++;
                    else if (codT == "RAE") subtipos.RAE++;
                }
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

        // GET /api/reportes-estrategicos/cursos?anioLectivo=2026
        [HttpGet("cursos")]
        public async Task<IActionResult> GetCursos(
            [FromQuery] int anioLectivo = 2026,
            CancellationToken ct = default)
        {
            var cursos = await _db.Cursos
                .AsNoTracking()
                .Include(c => c.Anio)
                .Include(c => c.Division)
                .Where(c => c.AñoLectivo.Year == anioLectivo)
                .OrderBy(c => c.Anio.Numero)
                .ThenBy(c => c.Division.Nombre)
                .Select(c => new CursoLabelDto
                {
                    Id    = c.IdCurso,
                    Label = c.Anio.Numero.ToString() + c.Division.Nombre,
                })
                .ToListAsync(ct);

            return Ok(cursos);
        }

        // GET /api/reportes-estrategicos/calificaciones?anioLectivo=2026&desde=&hasta=
        [HttpGet("calificaciones")]
        public async Task<IActionResult> GetDashboardCalificaciones(
            [FromQuery] int anioLectivo = 2026,
            [FromQuery] DateOnly? desde = null,
            [FromQuery] DateOnly? hasta = null,
            CancellationToken ct = default)
        {
            // ── 1. Avance de programas (solo Origen=Manual, Vigente o Confirmado) ─
            var bloquesPorPrograma = await _db.BloquesProgramas
                .AsNoTracking()
                .Where(b => b.Tipo == TipoBloquePrograma.Tema
                         && b.Programa.AnioLectivo == anioLectivo
                         && b.Programa.Origen == OrigenPrograma.Manual
                         && (b.Programa.Estado == EstadoPrograma.Vigente
                             || b.Programa.Estado == EstadoPrograma.Confirmado))
                .GroupBy(b => b.IdPrograma)
                .Select(g => new
                {
                    Total = g.Count(),
                    Dados = g.Count(b => b.Estado == EstadoBloque.Dado),
                })
                .ToListAsync(ct);

            decimal? avanceProgramas = null;
            if (bloquesPorPrograma.Any())
            {
                avanceProgramas = Math.Round(
                    (decimal)bloquesPorPrograma.Average(g =>
                        g.Total > 0 ? (double)g.Dados / g.Total * 100 : 0),
                    1);
            }

            // ── 2. ECs del año lectivo ───────────────────────────────────────────
            var ecIds = await _db.EspaciosCurriculares
                .AsNoTracking()
                .Where(ec => ec.Curso.AñoLectivo.Year == anioLectivo)
                .Select(ec => new
                {
                    ec.IdEC,
                    NombreEC = ec.Curricula.Nombre + " " + ec.Curso.Anio.Numero.ToString() + ec.Curso.Division.Nombre,
                    NombreCurso = ec.Curso.Anio.Numero.ToString() + ec.Curso.Division.Nombre,
                })
                .ToListAsync(ct);

            if (!ecIds.Any())
                return Ok(BuildEmptyCalificacionesDashboard(avanceProgramas));

            var ecIdSet = ecIds.Select(e => e.IdEC).ToHashSet();
            var ecNombreMap = ecIds.ToDictionary(e => e.IdEC, e => e.NombreEC);
            var ecCursoMap = ecIds.ToDictionary(e => e.IdEC, e => e.NombreCurso);

            // ── 3. Instancias evaluativas del año ────────────────────────────────
            var instancias = await _db.InstanciasEvaluativas
                .AsNoTracking()
                .Where(i => ecIdSet.Contains(i.IdEC))
                .Select(i => new { i.IdIE, i.IdEC })
                .ToListAsync(ct);

            if (!instancias.Any())
                return Ok(BuildEmptyCalificacionesDashboard(avanceProgramas));

            var ieSet = instancias.Select(i => i.IdIE).ToHashSet();
            var ieEcMap = instancias.ToDictionary(i => i.IdIE, i => i.IdEC);

            // ── 4. Archivos IE (para Exámenes Realizados y Recuperatorios) ───────
            // Fuente: ArchivosIE — no requiere calificaciones cargadas.
            var archivosIeQuery = _db.ArchivosIE
                .AsNoTracking()
                .Where(a => a.Habilitada && ieSet.Contains(a.IdIE));

            if (desde.HasValue)
                archivosIeQuery = archivosIeQuery.Where(a => a.FechaEjecucion >= desde.Value.ToDateTime(TimeOnly.MinValue));
            if (hasta.HasValue)
                archivosIeQuery = archivosIeQuery.Where(a => a.FechaEjecucion < hasta.Value.AddDays(1).ToDateTime(TimeOnly.MinValue));

            var archivosIe = await archivosIeQuery
                .Select(a => new { a.IdIE, a.TipoCalificacion })
                .ToListAsync(ct);

            // Exámenes Realizados = cantidad de archivos de tipo NotaOriginal (= exámenes tomados)
            int examenesRealizados = archivosIe.Count(a => a.TipoCalificacion == TipoCalificacion.NotaOriginal);

            // Recuperatorios: por slot (IdIE), detectar qué tipos de archivo existen
            var tiposPorSlot = archivosIe
                .GroupBy(a => a.IdIE)
                .Select(g => new
                {
                    TieneOrig = g.Any(a => a.TipoCalificacion == TipoCalificacion.NotaOriginal),
                    TieneRec1 = g.Any(a => a.TipoCalificacion == TipoCalificacion.Recuperatorio1),
                    TieneRec2 = g.Any(a => a.TipoCalificacion == TipoCalificacion.Recuperatorio2),
                })
                .Where(s => s.TieneOrig)
                .ToList();

            int totalSlots = tiposPorSlot.Count;
            int sinRec = tiposPorSlot.Count(s => !s.TieneRec1 && !s.TieneRec2);
            int con1   = tiposPorSlot.Count(s =>  s.TieneRec1 && !s.TieneRec2);
            int con2   = tiposPorSlot.Count(s =>  s.TieneRec2);

            decimal pctSinRec = totalSlots > 0 ? Math.Round((decimal)sinRec / totalSlots * 100, 1) : 0m;
            decimal pctCon1   = totalSlots > 0 ? Math.Round((decimal)con1   / totalSlots * 100, 1) : 0m;
            decimal pctCon2   = totalSlots > 0 ? Math.Round((decimal)con2   / totalSlots * 100, 1) : 0m;

            // ── 5. Calificaciones vigentes (filtradas por período si se indicó) ──
            var calQuery = _db.Calificaciones
                .AsNoTracking()
                .Where(c => c.Habilitada && c.Puntaje != null && ieSet.Contains(c.IdIE));

            if (desde.HasValue)
                calQuery = calQuery.Where(c => c.ArchivoIE.FechaEjecucion >= desde.Value.ToDateTime(TimeOnly.MinValue));
            if (hasta.HasValue)
                calQuery = calQuery.Where(c => c.ArchivoIE.FechaEjecucion < hasta.Value.AddDays(1).ToDateTime(TimeOnly.MinValue));

            var calificaciones = await calQuery
                .Select(c => new
                {
                    c.IdIE,
                    c.IdEstudiante,
                    c.TipoCalificacion,
                    c.Puntaje,
                })
                .ToListAsync(ct);

            // Sin calificaciones: devolver lo que tenemos (exámenes y recuperatorios ya computados)
            if (!calificaciones.Any())
                return Ok(new DashboardCalificacionesDto
                {
                    AvanceProgramas             = avanceProgramas,
                    ExamenesRealizados          = examenesRealizados,
                    PorcentajeSinRecuperatorio  = pctSinRec,
                    PorcentajeConRecuperatorio1 = pctCon1,
                    PorcentajeConRecuperatorio2 = pctCon2,
                    DistribucionEstados         = new DistribucionEstadosDto(),
                });

            // ── 6. Agrupar por (IE, Estudiante) → puntaje final y tipo ──────────
            // "Final" = la calificación de mayor tipo presente (Rec2 > Rec1 > Original)
            var porExamen = calificaciones
                .GroupBy(c => (c.IdIE, c.IdEstudiante))
                .Select(g =>
                {
                    var rec2 = g.FirstOrDefault(c => c.TipoCalificacion == TipoCalificacion.Recuperatorio2);
                    var rec1 = g.FirstOrDefault(c => c.TipoCalificacion == TipoCalificacion.Recuperatorio1);
                    var orig = g.FirstOrDefault(c => c.TipoCalificacion == TipoCalificacion.NotaOriginal);

                    var final = rec2 ?? rec1 ?? orig;

                    return new
                    {
                        g.Key.IdIE,
                        g.Key.IdEstudiante,
                        PuntajeFinal = final!.Puntaje!.Value,
                        TieneRec1 = rec1 != null,
                        TieneRec2 = rec2 != null,
                        IdEC = ieEcMap.GetValueOrDefault(g.Key.IdIE),
                    };
                })
                .ToList();

            // ── 7. KPIs ──────────────────────────────────────────────────────────
            var puntajes = porExamen.Select(e => (double)e.PuntajeFinal).ToList();

            decimal? promedioGeneral = porExamen.Count > 0
                ? Math.Round((decimal)puntajes.Average(), 2)
                : null;

            // ── 8. Distribución de estados ───────────────────────────────────────
            // Aprobado: puntaje >= 7
            // Desaprobado por Tema: 4 ≤ puntaje < 7
            // Desaprobado: puntaje < 4
            int totalCalif    = porExamen.Count;
            int aprobados     = porExamen.Count(e => e.PuntajeFinal >= 7);
            int desapTema     = porExamen.Count(e => e.PuntajeFinal >= 4 && e.PuntajeFinal < 7);
            int desaprobados  = porExamen.Count(e => e.PuntajeFinal < 4);

            decimal pctAprobado    = totalCalif > 0 ? Math.Round((decimal)aprobados    / totalCalif * 100, 1) : 0m;
            decimal pctDesapTema   = totalCalif > 0 ? Math.Round((decimal)desapTema    / totalCalif * 100, 1) : 0m;
            decimal pctDesaprobado = totalCalif > 0 ? Math.Round((decimal)desaprobados / totalCalif * 100, 1) : 0m;

            // Tasa de Aprobación General = % Aprobados sobre total
            decimal tasaAprobacionGeneral = pctAprobado;

            // Alumnos en riesgo: estudiantes cuyo promedio personal < 7 (en todos sus exámenes)
            int alumnosEnRiesgo = porExamen
                .GroupBy(e => e.IdEstudiante)
                .Count(g => g.Average(e => (double)e.PuntajeFinal) < 7.0);

            // ── 9. Top 5 EC Mayor Tasa de Desaprobación ──────────────────────────
            // Tasa = (desaprobados + desap. por tema) / total × 100 (complemento de aprobación)
            var top5EcDesap = porExamen
                .Where(e => e.IdEC != Guid.Empty)
                .GroupBy(e => e.IdEC)
                .Where(g => g.Count() >= 3)
                .Select(g => new EcDesaprobacionDto
                {
                    Nombre = ecNombreMap.GetValueOrDefault(g.Key, g.Key.ToString()),
                    TasaDesaprobacion = Math.Round((decimal)g.Count(e => e.PuntajeFinal < 7) / g.Count() * 100, 1),
                })
                .OrderByDescending(e => e.TasaDesaprobacion)
                .Take(5)
                .ToList();

            // ── 10. Top 5 EC Mejor Promedio ──────────────────────────────────────
            var top5EcPromedio = porExamen
                .Where(e => e.IdEC != Guid.Empty)
                .GroupBy(e => e.IdEC)
                .Where(g => g.Count() >= 3)
                .Select(g => new EcPromedioDto
                {
                    Nombre = ecNombreMap.GetValueOrDefault(g.Key, g.Key.ToString()),
                    Promedio = Math.Round((decimal)g.Average(e => e.PuntajeFinal), 2),
                })
                .OrderByDescending(e => e.Promedio)
                .Take(5)
                .ToList();

            // ── 11. Top 5 Cursos Mayor Tasa Desaprobación ─────────────────────────
            var top5CursosTasa = porExamen
                .Where(e => e.IdEC != Guid.Empty)
                .GroupBy(e => ecCursoMap.GetValueOrDefault(e.IdEC, ""))
                .Where(g => !string.IsNullOrEmpty(g.Key) && g.Count() >= 3)
                .Select(g =>
                {
                    int desap = g.Count(e => e.PuntajeFinal < 7);
                    return new CursoTasaDesaprobacionDto
                    {
                        Curso = g.Key,
                        TasaDesaprobacion = Math.Round((decimal)desap / g.Count() * 100, 1),
                    };
                })
                .OrderByDescending(c => c.TasaDesaprobacion)
                .Take(5)
                .ToList();

            return Ok(new DashboardCalificacionesDto
            {
                AvanceProgramas             = avanceProgramas,
                PromedioGeneral             = promedioGeneral,
                TasaAprobacionGeneral       = tasaAprobacionGeneral,
                AlumnosEnRiesgo             = alumnosEnRiesgo,
                ExamenesRealizados          = examenesRealizados,
                PorcentajeSinRecuperatorio  = pctSinRec,
                PorcentajeConRecuperatorio1 = pctCon1,
                PorcentajeConRecuperatorio2 = pctCon2,
                Top5EcMayorDesaprobacion    = top5EcDesap,
                Top5EcMejorPromedio         = top5EcPromedio,
                Top5CursosMayorTasa         = top5CursosTasa,
                DistribucionEstados = new DistribucionEstadosDto
                {
                    Aprobado           = pctAprobado,
                    Desaprobado        = pctDesaprobado,
                    DesaprobadoPorTema = pctDesapTema,
                },
            });
        }

        private static DashboardCalificacionesDto BuildEmptyCalificacionesDashboard(decimal? avanceProgramas) => new()
        {
            AvanceProgramas     = avanceProgramas,
            DistribucionEstados = new DistribucionEstadosDto(),
        };
    }
}
