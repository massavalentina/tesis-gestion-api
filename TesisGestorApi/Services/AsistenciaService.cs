using Microsoft.EntityFrameworkCore;
using TesisGestorApi.Data;
using TesisGestorApi.DTOs;
using TesisGestorApi.Dtos;
using TesisGestorApi.Exceptions;
using TesisGestorApi.Interfaces;
using TesisGestorApi.Entities;

namespace TesisGestorApi.Services
{
    public class AsistenciaService : IAsistenciaService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AsistenciaService> _logger;
        private readonly IParteDiarioService _parteDiarioService;

        private readonly IAsistenciaUmbralService _umbrales;

        public AsistenciaService(
            ApplicationDbContext context,
            ILogger<AsistenciaService> logger,
            IParteDiarioService parteDiarioService,
            IAsistenciaUmbralService umbrales)
        {
            _context = context;
            _logger = logger;
            _parteDiarioService = parteDiarioService;
            _umbrales = umbrales;
        }

        // [ POST ] Registro de Asistencia General por Lote
        public async Task<int> RegistrarLoteAsync(List<RegistrarAsistenciaDto> lista)
        {
            // Preparación de Datos - Se traen los tipos de asistencia
            var codigosEspeciales = new[] { "RA", "RAE", "RE", "LLT", "LLTE", "LLTC", "P", "A" };
            var idsTiposRequest = lista.Select(x => x.TipoAsistenciaId).Distinct().ToList();

            // Se traen los tipos de asistencia que vienen en el request
            var tiposDict = await _context.TiposAsistencia
                .Where(t => idsTiposRequest.Contains(t.IdTipo) || codigosEspeciales.Contains(t.Codigo))
                .ToDictionaryAsync(t => t.IdTipo);

            // Diccionario inverso para buscar por código ("RAE" -> Entidad)
            var tiposPorCodigo = tiposDict.Values
                .GroupBy(t => t.Codigo.ToUpper())
                .ToDictionary(g => g.Key, g => g.First());

            // Datos Generales de Estudiante y fechas para optimizar consultas posteriores
            var idsEstudiantes = lista.Select(x => x.EstudianteId).Distinct().ToList();
            var fechas = lista.Select(x => x.Fecha).Distinct().ToList();
            var diasSemana = fechas.Select(f => f.DayOfWeek).Distinct().ToList();

            // Inscripciones (Detalles de Cursado)
            var inscripciones = await _context.DetallesCursado
                .AsNoTracking()
                .Where(dc => idsEstudiantes.Contains(dc.IdEstudiante) && dc.Estado)
                .ToListAsync();

            // Búsqueda de Cursos
            var cursosIds = inscripciones.Select(i => i.IdCurso).Distinct().ToList();

            // Búsqueda de Horarios relacionados a los cursos y el día de la semana
            var horarios = await _context.Horarios
                .AsNoTracking()
                .Where(h => cursosIds.Contains(h.IdCurso) && diasSemana.Contains(h.DíaSemana))
                .ToListAsync();

            // Caché de clases dictadas — una por slot (IdHorario, Fecha)
            var clasesIdsHorario = horarios.Select(h => h.IdHorario).Distinct().ToList();
            var clasesDictadasDb = await _context.ClasesDictadas
                .AsNoTracking()
                .Where(c => clasesIdsHorario.Contains(c.IdHorario) && fechas.Contains(c.Fecha))
                .ToListAsync();

            var clasesDictadasLocales = new Dictionary<(Guid, DateOnly), ClaseDictada>();
            foreach (var c in clasesDictadasDb) clasesDictadasLocales[(c.IdHorario, c.Fecha)] = c;

            // Asistencias Existentes (para ver cuáles hay que insertar o actualizar)
            var asistenciasExistentes = await _context.Asistencias
                .Include(a => a.TipoManiana)
                .Include(a => a.TipoLlegadaManiana)  // Necesario para preservar llegada al registrar retiro
                .Include(a => a.TipoTarde)
                .Where(a => idsEstudiantes.Contains(a.EstudianteId) && fechas.Contains(a.Fecha))
                .ToListAsync();

            // Captura de estado anterior (antes de modificar) para el log del parte diario
            var oldManana = asistenciasExistentes.ToDictionary(a => (a.EstudianteId, a.Fecha), a => a.TipoManiana?.Codigo);
            var oldTarde  = asistenciasExistentes.ToDictionary(a => (a.EstudianteId, a.Fecha), a => a.TipoTarde?.Codigo);

            // Nombres de estudiantes para el log
            var estudiantesDict = await _context.Estudiantes
                .AsNoTracking()
                .Where(e => idsEstudiantes.Contains(e.IdEstudiante))
                .Select(e => new { e.IdEstudiante, e.Nombre, e.Apellido })
                .ToDictionaryAsync(e => e.IdEstudiante, e => (e.Nombre, e.Apellido));

            int cont = 0;
            var asistenciasParaProcesar = new List<Asistencia>();

            // Procesamiento de cada DTO
            foreach (var dto in lista)
            {
                if (!tiposDict.TryGetValue(dto.TipoAsistenciaId, out var tipoEntidadOriginal))
                {
                    _logger.LogWarning($"Tipo {dto.TipoAsistenciaId} no encontrado.");
                    continue;
                }

                // Buscar o Crear Asistencia
                var asistencia = asistenciasExistentes
                    .FirstOrDefault(a => a.EstudianteId == dto.EstudianteId && a.Fecha == dto.Fecha);

                if (asistencia == null)
                {
                    asistencia = new Asistencia
                    {
                        Id = Guid.NewGuid(),
                        EstudianteId = dto.EstudianteId,
                        Fecha = dto.Fecha,
                        ValorTotalInasistencia = 0
                    };
                    _context.Asistencias.Add(asistencia);
                    asistenciasExistentes.Add(asistencia);
                }
                if (!asistenciasParaProcesar.Contains(asistencia)) asistenciasParaProcesar.Add(asistencia);

                TimeSpan horaEfectiva = dto.Hora ?? TimeOnly.FromDateTime(DateTime.Now).ToTimeSpan();
                string codigoOriginal = tipoEntidadOriginal.Codigo.ToUpper();
                var turno = (dto.Turno ?? "MANANA").Trim().ToUpperInvariant();
                if (turno == "MANIANA")
                    turno = "MANANA";

                bool esManana = turno == "MANANA";

                TipoAsistencia tipoFinal = tipoEntidadOriginal;

                if (codigoOriginal == "RA")
                {
                    var inscripcion = inscripciones.FirstOrDefault(i => i.IdEstudiante == dto.EstudianteId);
                    if (inscripcion != null)
                    {
                        var horariosTurno = horarios
                            .Where(h => h.IdCurso == inscripcion.IdCurso &&
                                        h.DíaSemana == dto.Fecha.DayOfWeek &&
                                        (esManana ? h.HorarioEntrada < new TimeSpan(13, 20, 0) : h.HorarioEntrada >= new TimeSpan(13, 20, 0)))
                            .ToList();

                        double porcPerdido = CalcularPorcentajePerdidoHelper(
                            horariosTurno, horaEfectiva, clasesDictadasLocales, dto.Fecha);

                        if (esManana)
                        {
                            if (porcPerdido > 50 && tiposPorCodigo.ContainsKey("RAE"))
                                tipoFinal = tiposPorCodigo["RAE"];
                            else if (porcPerdido <= 10 && tiposPorCodigo.ContainsKey("RE"))
                                tipoFinal = tiposPorCodigo["RE"];
                        }
                        else
                        {
                            if (porcPerdido <= 10 && tiposPorCodigo.ContainsKey("RE"))
                                tipoFinal = tiposPorCodigo["RE"];
                        }
                    }
                }

                // Clasifica el tipo de asistencia para saber como actualizar Asistencia.
                // Códigos de llegada: asignados manualmente por el preceptor al ingreso.
                // Códigos de retiro: calculados automáticamente al registrar RA.
                // Código SA (Sin Asistencia): limpia el turno, dejándolo como si nunca se hubiera registrado.
                string codigoFinal = tipoFinal.Codigo.ToUpper();
                bool esTipoLlegada = codigoFinal is "P" or "A" or "ANC" or "LLT" or "LLTE" or "LLTC";
                bool esTipoRetiro  = codigoFinal is "RE" or "RA" or "RAE";
                bool esSinAsistencia = codigoFinal == "SA";

                if (esSinAsistencia)
                {
                    // SA actúa como comando de limpieza: deja el turno como "Sin definir".
                    if (esManana)
                    {
                        asistencia.TipoManianaId        = null;
                        asistencia.TipoManiana           = null;
                        asistencia.TipoLlegadaManianaId = null;
                        asistencia.TipoLlegadaManiana   = null;
                        asistencia.HoraEntradaManana    = null;
                        asistencia.HoraSalidaManana     = null;
                    }
                    else
                    {
                        asistencia.TipoTardeId      = null;
                        asistencia.TipoTarde        = null;
                        asistencia.HoraEntradaTarde = null;
                        asistencia.HoraSalidaTarde  = null;
                    }
                    cont++;
                    continue;
                }

                if (esManana)
                {
                    // TipoManiana siempre se actualiza al estado más reciente del turno.
                    // Si hay un retiro, refleja el código de retiro.
                    asistencia.TipoManianaId = tipoFinal.IdTipo;
                    asistencia.TipoManiana   = tipoFinal;

                    if (esTipoLlegada)
                    {
                        // Llegada: se guarda también en TipoLlegadaManiana para que Asistencia.CalcularAsistencia()
                        // pueda sumar llegada + retiro si después se registra un retiro.
                        asistencia.TipoLlegadaManianaId = tipoFinal.IdTipo;
                        asistencia.TipoLlegadaManiana   = tipoFinal;
                        asistencia.HoraEntradaManana    = horaEfectiva;
                    }
                    else if (esTipoRetiro)
                    {
                        // Retiro: solo se actualiza la hora de salida. TipoLlegadaManianaId NO se modifica.
                        asistencia.HoraSalidaManana = horaEfectiva;
                    }
                }
                else
                {
                    asistencia.TipoTardeId = tipoFinal.IdTipo;
                    asistencia.TipoTarde   = tipoFinal;

                    if (esTipoLlegada) asistencia.HoraEntradaTarde = horaEfectiva;
                    else if (esTipoRetiro)  asistencia.HoraSalidaTarde  = horaEfectiva;
                }

                if (!asistenciasParaProcesar.Contains(asistencia))
                    asistenciasParaProcesar.Add(asistencia);

                cont++;
            }

            await _context.SaveChangesAsync();

            // Se determina qué turno fue registrado para CADA ESTUDIANTE en este lote.
            // Usar un mapeo por estudiante (en lugar de un HashSet global) evita que un turno presente
            // en el lote de un estudiante "contamine" el filtro de otro estudiante que no lo tiene.
            var turnosPorEstudiante = lista
                .GroupBy(d => d.EstudianteId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(d => d.Turno?.Trim().ToUpper() == "MANANA" ? "MANANA" : "TARDE").ToHashSet()
                );

            if (asistenciasParaProcesar.Any())
            {
                await ProcesarAsistenciaEspacios(asistenciasParaProcesar, turnosPorEstudiante);
            }

            // Registrar cambios de asistencia en el log del parte diario
            await LogearCambiosAsistenciaAsync(lista, inscripciones, asistenciasExistentes, oldManana, oldTarde, estudiantesDict);
            try
            {
                foreach (var anio in fechas.Select(f => f.Year).Distinct())
                {
                    await _umbrales.ProcesarUmbralesAsync(idsEstudiantes, anio);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error procesando umbrales de inasistencias");
            }

            return cont;
        }


        // [ Helper Privado ] Cálculo de Porcentaje de Ausencia en Retiro Anticipado
        // Devuelve el porcentaje de tiempo perdido por el estudiante respecto al total
        // de minutos de clases DADAS en el turno. Solo se cuentan clases explícitamente
        // marcadas como "no dictada" = false; si no hay registro, se asume dictada
        // (porque este helper se llama en tiempo real, antes de que ProcesarAsistenciaEspacios
        // cree los registros de ClaseDictada automáticamente).
        private double CalcularPorcentajePerdidoHelper(
            List<Horario> horariosTurno,
            TimeSpan horaRetiro,
            Dictionary<(Guid, DateOnly), ClaseDictada> clasesDictadasLocales,
            DateOnly fecha)
        {
            double minutosTotales = 0;
            double minutosPerdidos = 0;

            // Cada slot (Horario) es independiente — tiene su propia ClaseDictada con sus tiempos efectivos
            foreach (var h in horariosTurno)
            {
                clasesDictadasLocales.TryGetValue((h.IdHorario, fecha), out var cd);
                if (cd != null && !cd.Dictada) continue;

                TimeSpan horaEntradaEf = cd?.HorarioEntradaEfectiva ?? h.HorarioEntrada;
                TimeSpan horaSalidaEf  = cd?.HorarioSalidaEfectiva  ?? h.HorarioSalida;

                double duracion = (horaSalidaEf - horaEntradaEf).TotalMinutes;
                if (duracion <= 0) continue;

                minutosTotales += duracion;

                if (horaRetiro < horaSalidaEf)
                {
                    TimeSpan inicioPerdida = horaRetiro > horaEntradaEf ? horaRetiro : horaEntradaEf;
                    double perdidoEnModulo = (horaSalidaEf - inicioPerdida).TotalMinutes;
                    if (perdidoEnModulo > 0) minutosPerdidos += perdidoEnModulo;
                }
            }

            return minutosTotales == 0 ? 0 : (minutosPerdidos / minutosTotales) * 100.0;
        }


        // [ Helper Público ] Procesa las asistencias por espacio y calcula el valor final.
        // Cada slot de Horario es independiente: tiene su propio ClaseDictada con sus propios tiempos efectivos.
        // La regla del 20% se sigue calculando por EC (agrega todos los slots dictados del EC en el día).
        // Cada slot dictado recibe su propio AsistenciaPorEspacio con el mismo resultado de presente/ausente.
        // turnosPorEstudiante: para cada estudiante, qué turno(s) fueron explícitamente registrados en este request.
        // Si es null, no se aplica filtro por turno (solo se salta si tipoTurnoEc == null).
        public async Task ProcesarAsistenciaEspacios(List<Asistencia> asistenciasGenerales, Dictionary<Guid, HashSet<string>>? turnosPorEstudiante = null)
        {
            var estudiantesIds = asistenciasGenerales.Select(a => a.EstudianteId).Distinct().ToList();
            var fechas = asistenciasGenerales.Select(a => a.Fecha).Distinct().ToList();
            var diasSemana = fechas.Select(f => f.DayOfWeek).Distinct().ToList();

            var inscripciones = await _context.DetallesCursado.AsNoTracking()
                .Where(dc => estudiantesIds.Contains(dc.IdEstudiante) && dc.Estado == true).ToListAsync();
            var cursosIds = inscripciones.Select(i => i.IdCurso).Distinct().ToList();
            var horarios = await _context.Horarios.AsNoTracking()
                .Where(h => cursosIds.Contains(h.IdCurso) && diasSemana.Contains(h.DíaSemana)).ToListAsync();

            // Cache de clases — una por slot (IdHorario, Fecha)
            var clasesIdsHorario = horarios.Select(h => h.IdHorario).Distinct().ToList();
            var clasesExistentes = await _context.ClasesDictadas
                .Where(c => clasesIdsHorario.Contains(c.IdHorario) && fechas.Contains(c.Fecha)).ToListAsync();
            var clasesDictadasLocales = new Dictionary<(Guid IdHorario, DateOnly Fecha), ClaseDictada>();
            foreach (var c in clasesExistentes) clasesDictadasLocales[(c.IdHorario, c.Fecha)] = c;

            var nuevasAsistenciasEspacio = new List<AsistenciaPorEspacio>();

            foreach (var asistencia in asistenciasGenerales)
            {
                var inscripcion = inscripciones.FirstOrDefault(i => i.IdEstudiante == asistencia.EstudianteId);
                if (inscripcion == null) continue;

                var horariosDelDia = horarios
                    .Where(h => h.IdCurso == inscripcion.IdCurso && h.DíaSemana == asistencia.Fecha.DayOfWeek)
                    .ToList();

                double minTotalesM = 0, minPerdidaIngresoM = 0, minPerdidaSalidaM = 0;
                double minTotalesT = 0, minPerdidaIngresoT = 0, minPerdidaSalidaT = 0;

                // Agrupar por EC para la regla del 20% (que sigue siendo por espacio curricular)
                var gruposMaterias = horariosDelDia.GroupBy(h => h.IdEC).ToList();

                foreach (var grupoMateria in gruposMaterias)
                {
                    Guid idEC  = grupoMateria.Key;
                    var  slots = grupoMateria.OrderBy(h => h.HorarioEntrada).ToList();

                    // Determinar el turno del EC por el primer slot (turno no cambia al mover)
                    bool esEcManana = slots.First().HorarioEntrada < new TimeSpan(13, 20, 0);

                    if (turnosPorEstudiante != null)
                    {
                        string ecTurno = esEcManana ? "MANANA" : "TARDE";
                        if (!turnosPorEstudiante.TryGetValue(asistencia.EstudianteId, out var turnosEst)
                            || !turnosEst.Contains(ecTurno))
                            continue;
                    }

                    var tipoTurnoEc = esEcManana ? asistencia.TipoManiana : asistencia.TipoTarde;

                    // Sin asistencia en este turno: eliminar stale APEs de todos los slots del EC
                    if (tipoTurnoEc == null)
                    {
                        foreach (var slot in slots)
                        {
                            var slotKey = (slot.IdHorario, asistencia.Fecha);
                            if (clasesDictadasLocales.TryGetValue(slotKey, out var cdStale))
                            {
                                var stale = await _context.AsistenciasPorEspacio
                                    .FirstOrDefaultAsync(ae => ae.IdClaseDictada == cdStale.IdClaseDictada
                                                            && ae.IdEstudiante   == asistencia.EstudianteId);
                                if (stale != null) _context.AsistenciasPorEspacio.Remove(stale);
                            }
                        }
                        continue;
                    }

                    string codigoTurnoEc = tipoTurnoEc.Codigo.ToUpper();

                    // Crear ClaseDictadas para los slots que aún no las tienen
                    foreach (var slot in slots)
                    {
                        var slotKey = (slot.IdHorario, asistencia.Fecha);
                        if (!clasesDictadasLocales.ContainsKey(slotKey))
                        {
                            var nuevaClase = new ClaseDictada
                            {
                                IdClaseDictada = Guid.NewGuid(),
                                IdHorario      = slot.IdHorario,
                                IdEC           = idEC,
                                Fecha          = asistencia.Fecha,
                                Dictada        = true,
                            };
                            _context.ClasesDictadas.Add(nuevaClase);
                            clasesDictadasLocales[slotKey] = nuevaClase;
                        }
                    }

                    // ANC: eliminar APEs de todos los slots dictados del EC
                    if (codigoTurnoEc == "ANC")
                    {
                        foreach (var slot in slots)
                        {
                            var slotKey = (slot.IdHorario, asistencia.Fecha);
                            var cdAnc = clasesDictadasLocales[slotKey];
                            if (!cdAnc.Dictada) continue;
                            var existenteAnc = await _context.AsistenciasPorEspacio
                                .FirstOrDefaultAsync(ae => ae.IdClaseDictada == cdAnc.IdClaseDictada && ae.IdEstudiante == asistencia.EstudianteId);
                            if (existenteAnc != null) _context.AsistenciasPorEspacio.Remove(existenteAnc);
                        }
                        continue;
                    }

                    // Regla del 20%: agregar minutos de TODOS los slots dictados del EC
                    double minTotalesMateria  = 0;
                    double minAsistidosMateria = 0;

                    foreach (var horario in slots)
                    {
                        var slotKey = (horario.IdHorario, asistencia.Fecha);
                        var cdSlot  = clasesDictadasLocales[slotKey];

                        // Solo los slots dictados contribuyen al cálculo
                        if (!cdSlot.Dictada) continue;

                        double duracionModulo = (horario.HorarioSalida - horario.HorarioEntrada).TotalMinutes;
                        if (duracionModulo <= 0) continue;

                        minTotalesMateria += duracionModulo;

                        bool esManana = horario.HorarioEntrada < new TimeSpan(13, 20, 0);
                        if (esManana) minTotalesM += duracionModulo; else minTotalesT += duracionModulo;

                        var tipoAsistencia = esManana ? asistencia.TipoManiana : asistencia.TipoTarde;
                        string codigo = tipoAsistencia?.Codigo?.ToUpper() ?? "-";

                        if (codigo == "P" || codigo == "RE")
                        {
                            minAsistidosMateria += duracionModulo;
                            continue;
                        }

                        // Tiempos efectivos del slot (override individual por slot)
                        TimeSpan entradaEfMod = cdSlot.HorarioEntradaEfectiva ?? horario.HorarioEntrada;
                        TimeSpan salidaEfMod  = cdSlot.HorarioSalidaEfectiva  ?? horario.HorarioSalida;

                        string? codigoLlegada = esManana
                            ? asistencia.TipoLlegadaManiana?.Codigo?.ToUpper()
                            : asistencia.TipoTarde?.Codigo?.ToUpper();
                        bool usarHoraEntradaReal = codigoLlegada is "LLT" or "LLTE" or "LLTC";
                        TimeSpan entradaAlumno = usarHoraEntradaReal
                            ? (esManana ? (asistencia.HoraEntradaManana ?? entradaEfMod) : (asistencia.HoraEntradaTarde ?? entradaEfMod))
                            : entradaEfMod;
                        TimeSpan salidaAlumno = esManana ? (asistencia.HoraSalidaManana ?? salidaEfMod) : (asistencia.HoraSalidaTarde ?? salidaEfMod);

                        if (entradaAlumno > entradaEfMod)
                        {
                            TimeSpan finPerdida = entradaAlumno < salidaEfMod ? entradaAlumno : salidaEfMod;
                            double perdido = (finPerdida - entradaEfMod).TotalMinutes;
                            if (perdido > 0) { if (esManana) minPerdidaIngresoM += perdido; else minPerdidaIngresoT += perdido; }
                        }

                        if (salidaAlumno < salidaEfMod)
                        {
                            TimeSpan inicioPerdida = salidaAlumno > entradaEfMod ? salidaAlumno : entradaEfMod;
                            double perdido = (salidaEfMod - inicioPerdida).TotalMinutes;
                            if (perdido > 0) { if (esManana) minPerdidaSalidaM += perdido; else minPerdidaSalidaT += perdido; }
                        }

                        TimeSpan inicioEfectivo = entradaEfMod > entradaAlumno ? entradaEfMod : entradaAlumno;
                        TimeSpan finEfectivo    = salidaEfMod  < salidaAlumno  ? salidaEfMod  : salidaAlumno;
                        double asistidoEnModulo = (finEfectivo - inicioEfectivo).TotalMinutes;
                        if (asistidoEnModulo < 0) asistidoEnModulo = 0;

                        if (codigo == "A") asistidoEnModulo = 0;
                        else minAsistidosMateria += asistidoEnModulo;
                    }

                    // Regla del 20%
                    bool   presenteMateria = true;
                    string motivoMateria   = "P";
                    if (minTotalesMateria > 0)
                    {
                        double porcentajeAusencia = 100.0 - ((minAsistidosMateria / minTotalesMateria) * 100.0);
                        if (porcentajeAusencia > 20.0)
                        {
                            presenteMateria = false;
                            motivoMateria = minAsistidosMateria == 0 ? "Ausente Total" : $"Ausencia Parcial ({Math.Round(porcentajeAusencia)}%)";
                        }
                    }

                    // Upsert un AsistenciaPorEspacio por cada slot DICTADO del EC
                    foreach (var slot in slots)
                    {
                        var slotKey = (slot.IdHorario, asistencia.Fecha);
                        var cdSlot  = clasesDictadasLocales[slotKey];
                        if (!cdSlot.Dictada) continue;

                        var ape = await _context.AsistenciasPorEspacio
                            .FirstOrDefaultAsync(ae => ae.IdClaseDictada == cdSlot.IdClaseDictada
                                                    && ae.IdEstudiante   == asistencia.EstudianteId);
                        if (ape != null)
                        {
                            ape.Presente = presenteMateria;
                            ape.Motivo   = motivoMateria;
                        }
                        else
                        {
                            nuevasAsistenciasEspacio.Add(new AsistenciaPorEspacio
                            {
                                IdAsistenciaEspacio = Guid.NewGuid(),
                                IdClaseDictada      = cdSlot.IdClaseDictada,
                                IdEstudiante        = asistencia.EstudianteId,
                                Presente            = presenteMateria,
                                Motivo              = motivoMateria,
                            });
                        }
                    }
                }

                asistencia.CalcularAsistencia(minTotalesM, minPerdidaIngresoM, minPerdidaSalidaM, minTotalesT, minPerdidaIngresoT, minPerdidaSalidaT);
            }

            if (nuevasAsistenciasEspacio.Any()) _context.AsistenciasPorEspacio.AddRange(nuevasAsistenciasEspacio);
            await _context.SaveChangesAsync();
        }

        // [ PUT ] Actualizar estado de clase de Dictada a No Dictada y viceversa.
        public async Task ActualizarEstadoClaseAsync(ClaseDictadaDTO dto)
        {
            // Cargar el slot de Horario para obtener IdEC e IdCurso
            var horario = await _context.Horarios
                .AsNoTracking()
                .Include(h => h.EspacioCurricular).ThenInclude(ec => ec.Curricula)
                .FirstOrDefaultAsync(h => h.IdHorario == dto.IdHorario);

            var clase = await _context.ClasesDictadas
                .Include(c => c.Asistencias)
                .FirstOrDefaultAsync(c => c.IdHorario == dto.IdHorario && c.Fecha == dto.Fecha);

            if (clase == null)
            {
                clase = new ClaseDictada
                {
                    IdClaseDictada = Guid.NewGuid(),
                    IdHorario      = dto.IdHorario,
                    IdEC           = horario?.IdEC ?? Guid.Empty,
                    Fecha          = dto.Fecha,
                    Dictada        = dto.Dictada,
                    Tema           = dto.Tema,
                    Motivo         = dto.Motivo,
                };
                _context.ClasesDictadas.Add(clase);
                await _context.SaveChangesAsync();

                if (clase.Dictada) await RegenerarAsistenciasParaClase(clase);
            }
            else
            {
                bool estadoAnterior = clase.Dictada;
                clase.Dictada = dto.Dictada;
                clase.Motivo  = dto.Motivo;
                if (!string.IsNullOrEmpty(dto.Tema)) clase.Tema = dto.Tema;

                if (estadoAnterior == true && dto.Dictada == false)
                {
                    if (clase.Asistencias.Any()) _context.AsistenciasPorEspacio.RemoveRange(clase.Asistencias);
                    await _context.SaveChangesAsync();
                }
                else if (estadoAnterior == false && dto.Dictada == true)
                {
                    await _context.SaveChangesAsync();
                    await RegenerarAsistenciasParaClase(clase);
                }
                else
                {
                    await _context.SaveChangesAsync();
                }
            }

            // Registrar evento en el parte diario
            if (horario != null)
            {
                string materiaNombre = horario.EspacioCurricular.Curricula?.Nombre ?? "Clase";
                string estadoTexto   = dto.Dictada ? "Dictada" : "No Dictada";
                string descripcion   = string.IsNullOrWhiteSpace(dto.Motivo)
                    ? $"{materiaNombre} marcada como {estadoTexto}"
                    : $"{materiaNombre} marcada como {estadoTexto}. Motivo: {dto.Motivo}";

                await _parteDiarioService.RegistrarEventoAsync(horario.EspacioCurricular.IdCurso, dto.Fecha, descripcion);
            }
        }


        // [ POST ] Recalcula las asistencias por espacio de todos los alumnos del curso en una fecha,
        // usando los tiempos efectivos vigentes (incluye overrides de horario del parte diario).
        public async Task RecalcularAsistenciasCursoFechaAsync(Guid cursoId, DateOnly fecha)
        {
            var idsEstudiantes = await _context.DetallesCursado
                .AsNoTracking()
                .Where(dc => dc.IdCurso == cursoId && dc.Estado)
                .Select(dc => dc.IdEstudiante)
                .ToListAsync();

            if (!idsEstudiantes.Any()) return;

            var asistencias = await _context.Asistencias
                .Include(a => a.TipoManiana)
                .Include(a => a.TipoTarde)
                .Include(a => a.TipoLlegadaManiana)
                .Where(a => a.Fecha == fecha && idsEstudiantes.Contains(a.EstudianteId))
                .ToListAsync();

            if (!asistencias.Any()) return;

            // Filtrar por turno según el estado actual de cada asistencia para evitar crear
            // ClaseDictadas en ECs del turno contrario como efecto secundario.
            var turnosPorEstudiante = asistencias
                .ToDictionary(a => a.EstudianteId, a =>
                {
                    var t = new HashSet<string>();
                    if (a.TipoManiana != null) t.Add("MANANA");
                    if (a.TipoTarde   != null) t.Add("TARDE");
                    return t;
                });

            await ProcesarAsistenciaEspacios(asistencias, turnosPorEstudiante);
        }

        // [ Helper Público ] Regenera las asistencias de los EC de una clase específica en base al estado de Dictado (true o false) y recalcula en base a la información de asistencias generales.
        public async Task RegenerarAsistenciasParaClase(ClaseDictada clase)
        {
            var idCurso = await _context.EspaciosCurriculares.Where(ec => ec.IdEC == clase.IdEC).Select(ec => ec.IdCurso).FirstOrDefaultAsync();
            var idsAlumnos = await _context.DetallesCursado.Where(dc => dc.IdCurso == idCurso && dc.Estado == true).Select(dc => dc.IdEstudiante).ToListAsync();
            var asistenciasGenerales = await _context.Asistencias
                .Include(a => a.TipoManiana)
                .Include(a => a.TipoLlegadaManiana)
                .Include(a => a.TipoTarde)
                .Where(a => a.Fecha == clase.Fecha && idsAlumnos.Contains(a.EstudianteId))
                .ToListAsync();

            if (!asistenciasGenerales.Any()) return;

            // Determinar el turno del slot específico para evitar crear ClaseDictadas
            // en ECs del turno contrario como efecto secundario.
            var horario = await _context.Horarios.AsNoTracking()
                .Where(h => h.IdHorario == clase.IdHorario)
                .FirstOrDefaultAsync();
            bool esManana = horario != null && horario.HorarioEntrada < new TimeSpan(13, 20, 0);
            string turnoEC = esManana ? "MANANA" : "TARDE";

            var turnosPorEstudiante = asistenciasGenerales
                .ToDictionary(a => a.EstudianteId, _ => new HashSet<string> { turnoEC });

            await ProcesarAsistenciaEspacios(asistenciasGenerales, turnosPorEstudiante);
        }

        // [ POST ] Registro de Asistencia General Individual - Recibe una asistencia y la procesa con el método de procesamiento por lote.
        public async Task<AsistenciaResponseDto> RegistrarAsistenciaIndividualAsync(RegistrarAsistenciaDto dto)
        {
            await RegistrarLoteAsync(new List<RegistrarAsistenciaDto> { dto });
            var entidad = await _context.Asistencias.AsNoTracking().FirstAsync(a => a.EstudianteId == dto.EstudianteId && a.Fecha == dto.Fecha);
            return new AsistenciaResponseDto { Id = entidad.Id, ValorTotal = entidad.ValorTotalInasistencia, Mensaje = "Registrado correctamente." };
        }

        // [ GET ALL ] Con filtros opcionales de Fecha y Estudiante.
        public async Task<IEnumerable<AsistenciaGetDTO>> ObtenerAsistenciasAsync(DateOnly? fecha, Guid? estudianteId)
        {
            var query = _context.Asistencias
                .AsNoTracking()
                .Include(a => a.Estudiante)
                .Include(a => a.TipoManiana)
                .Include(a => a.TipoLlegadaManiana)
                .Include(a => a.TipoTarde)
                .AsQueryable();

            if (fecha.HasValue)       query = query.Where(a => a.Fecha == fecha.Value);
            if (estudianteId.HasValue) query = query.Where(a => a.EstudianteId == estudianteId.Value);

            return await query.Select(a => new AsistenciaGetDTO
            {
                Id             = a.Id,
                Fecha          = a.Fecha,
                ValorTotal     = a.ValorTotalInasistencia,
                NombreCompleto = $"{a.Estudiante.Nombre} {a.Estudiante.Apellido}",
                Documento      = a.Estudiante.Documento,

                // TipoManiana mostrará Retiro en caso de haber habido uno.
                CodigoManana = a.TipoManiana != null ? a.TipoManiana.Codigo: "-",

                // TipoLlegadaManiana preserva el código de llegada.
                CodigoLlegadaManana = a.TipoLlegadaManiana != null ? a.TipoLlegadaManiana.Codigo : null,
                CodigoTarde = a.TipoTarde != null ? a.TipoTarde.Codigo : "-"
            }).OrderByDescending(a => a.Fecha).ToListAsync();
        }

        public async Task<PrevisualizarAsistenciaResponse> PrevisualizarAsync(PrevisualizarAsistenciaRequest request)
        {
            if (!Guid.TryParse(request.CodigoQr, out var qrGuid))
                throw new AsistenciaException("QR_INVALID", "Código QR inválido");

            if (request.IdCurso == Guid.Empty)
                throw new AsistenciaException("COURSE_INVALID", "Curso inválido");

            var credencial = await _context.CredencialesQR
                .Include(c => c.Estudiante)
                    .ThenInclude(e => e.DetallesCursado)
                        .ThenInclude(dc => dc.Curso)
                .FirstOrDefaultAsync(c => c.Codigo == qrGuid);

            if (credencial == null)
                throw new AsistenciaException("QR_INVALID", "Código no reconocido");

            if (!credencial.Activo)
                throw new AsistenciaException("QR_INACTIVE", "QR inactivo");

            if (credencial.FechaExpiracion < DateTime.UtcNow)
                throw new AsistenciaException("QR_EXPIRED", "QR expirado");

            var estudiante = credencial.Estudiante;
            var cursadoActivo = estudiante.DetallesCursado.FirstOrDefault(dc => dc.Estado);

            if (cursadoActivo == null)
                throw new AsistenciaException("STUDENT_INACTIVE", "Estudiante inactivo");

            if (cursadoActivo.IdCurso != request.IdCurso)
                throw new AsistenciaException("STUDENT_NOT_IN_COURSE", "El estudiante no pertenece al curso seleccionado");

            var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
            var asistencia = await _context.Asistencias.FirstOrDefaultAsync(a =>
                a.EstudianteId == estudiante.IdEstudiante &&
                a.Fecha == hoy);

            if (asistencia != null)
            {
                if (request.Turno == "Mañana" && asistencia.TipoManianaId != null)
                    throw new AsistenciaException("ALREADY_SCANNED", "Este alumno ya tiene asistencia cargada en el turno mañana");

                if (request.Turno == "Tarde" && asistencia.TipoTardeId != null)
                    throw new AsistenciaException("ALREADY_SCANNED", "Este alumno ya tiene asistencia cargada en el turno tarde");
            }

            return new PrevisualizarAsistenciaResponse
            {
                Estudiante = new EstudianteAsistenciaDto
                {
                    Id = estudiante.IdEstudiante,
                    Nombre = estudiante.Nombre,
                    Apellido = estudiante.Apellido,
                    Curso = cursadoActivo.Curso.Codigo
                }
            };
        }

        public async Task ConfirmarAsync(ConfirmarAsistenciaRequest request)
        {
            if (request.EstudianteIds == null || request.EstudianteIds.Count == 0)
                throw new AsistenciaException("EMPTY_STUDENTS", "No se recibieron estudiantes.");

            var tipoExiste = await _context.TiposAsistencia
                .AsNoTracking()
                .AnyAsync(t => t.IdTipo == request.TipoAsistenciaId);

            if (!tipoExiste)
                throw new AsistenciaException("INVALID_ATTENDANCE_TYPE", "Tipo inválido");

            var nowLocal = DateTime.Now;
            var hoy = DateOnly.FromDateTime(nowLocal);
            var hora = request.Hora ?? nowLocal.TimeOfDay;
            var turnoNormalizado = NormalizarTurno(request.Turno);

            var lista = request.EstudianteIds
                .Distinct()
                .Select(id => new RegistrarAsistenciaDto
                {
                    EstudianteId = id,
                    Fecha = hoy,
                    Turno = turnoNormalizado,
                    TipoAsistenciaId = request.TipoAsistenciaId,
                    Hora = hora
                })
                .ToList();

            await RegistrarLoteAsync(lista);
        }

        public async Task<List<OpcionSeleccionDto>> ObtenerCursosAsync()
        {
            return await _context.Cursos
                .Include(c => c.Anio)
                .Include(c => c.Division)
                .Where(c => c.Estado)
                .Select(c => new OpcionSeleccionDto
                {
                    Id = c.IdCurso.ToString(),
                    Label = $"{c.Anio.Numero}{c.Division.Nombre}"
                })
                .ToListAsync();
        }

        public List<OpcionSeleccionDto> ObtenerTurnos()
        {
            return Enum.GetValues<Turno>()
                .Select(t => new OpcionSeleccionDto
                {
                    Id = ((int)t).ToString(),
                    Label = t.ToString()
                })
                .ToList();
        }

        public async Task<List<OpcionSeleccionDto>> ObtenerTiposAsistenciaAsync()
        {
            return await _context.TiposAsistencia
                .Select(t => new OpcionSeleccionDto
                {
                    Id = t.IdTipo.ToString(),
                    Label = t.Codigo
                })
                .ToListAsync();
        }

        // [ GET ] Asistencia por Espacio Curricular de un día para un estudiante
        public async Task<List<AsistenciaEspacioItemDto>> ObtenerAsistenciaEspaciosDiaAsync(Guid estudianteId, DateOnly fecha)
        {
            var dc = await _context.DetallesCursado.AsNoTracking()
                .Where(d => d.IdEstudiante == estudianteId && d.Estado)
                .FirstOrDefaultAsync();
            if (dc == null) return new List<AsistenciaEspacioItemDto>();

            var diaSemana = fecha.DayOfWeek;
            var ecs = await _context.EspaciosCurriculares
                .AsNoTracking()
                .Include(ec => ec.Curricula)
                .Include(ec => ec.Horarios.Where(h => h.DíaSemana == diaSemana))
                .Include(ec => ec.ClasesDictadas.Where(cd => cd.Fecha == fecha))
                    .ThenInclude(cd => cd.Asistencias.Where(a => a.IdEstudiante == estudianteId))
                .Where(ec => ec.IdCurso == dc.IdCurso && ec.Horarios.Any(h => h.DíaSemana == diaSemana))
                .ToListAsync();

            // Cada slot (Horario) tiene su propia ClaseDictada — búsqueda por IdHorario
            return ecs.SelectMany(ec =>
                ec.Horarios.Select(h => {
                    var clase = ec.ClasesDictadas.FirstOrDefault(cd => cd.IdHorario == h.IdHorario);
                    var asist = clase?.Asistencias.FirstOrDefault();
                    bool modificado     = clase?.HorarioEntradaEfectiva.HasValue == true;
                    var entradaEfectiva = clase?.HorarioEntradaEfectiva ?? h.HorarioEntrada;
                    var salidaEfectiva  = clase?.HorarioSalidaEfectiva  ?? h.HorarioSalida;
                    return new AsistenciaEspacioItemDto
                    {
                        IdAsistenciaEspacio      = asist?.IdAsistenciaEspacio,
                        IdEC                     = ec.IdEC,
                        IdClaseDictada           = clase?.IdClaseDictada,
                        NombreMateria            = ec.Curricula.Nombre,
                        HorarioEntrada           = entradaEfectiva.ToString(@"hh\:mm"),
                        HorarioSalida            = salidaEfectiva.ToString(@"hh\:mm"),
                        HorarioEntradaOriginal   = modificado ? h.HorarioEntrada.ToString(@"hh\:mm") : null,
                        HorarioSalidaOriginal    = modificado ? h.HorarioSalida.ToString(@"hh\:mm")  : null,
                        Dictada                  = clase?.Dictada,
                        Presente                 = clase?.Dictada == true ? asist?.Presente : null,
                    };
                })
            ).OrderBy(d => d.HorarioEntrada).ToList();
        }

        // [ PUT ] Actualización manual de presencia en un Espacio Curricular
        public async Task ActualizarAsistenciaEspacioAsync(ActualizarAsistenciaEspacioDto dto)
        {
            var existente = await _context.AsistenciasPorEspacio
                .FirstOrDefaultAsync(a => a.IdEstudiante == dto.EstudianteId && a.IdClaseDictada == dto.IdClaseDictada);

            if (existente != null)
            {
                existente.Presente = dto.Presente;
                existente.Motivo   = dto.Presente ? "P" : "Ausente Manual";
            }
            else
            {
                var clase = await _context.ClasesDictadas.FindAsync(dto.IdClaseDictada)
                    ?? throw new Exception("ClaseDictada no encontrada.");
                _context.AsistenciasPorEspacio.Add(new AsistenciaPorEspacio
                {
                    IdAsistenciaEspacio = Guid.NewGuid(),
                    IdEstudiante        = dto.EstudianteId,
                    IdClaseDictada      = dto.IdClaseDictada,
                    Fecha               = clase.Fecha,
                    Presente            = dto.Presente,
                    Motivo              = dto.Presente ? "P" : "Ausente Manual",
                });
            }
            await _context.SaveChangesAsync();
        }

        // [ POST ] Deshacer registro de asistencia rápida — restablece el turno a Presente (P)
        public async Task<AsistenciaResponseDto> DeshacerAsistenciaRapidaAsync(DeshacerAsistenciaRapidaDto dto)
        {
            var turno = NormalizarTurno(dto.Turno ?? "MANANA");
            bool esManana = turno == "MANANA";

            var asistencia = await _context.Asistencias
                .Include(a => a.TipoManiana)
                .Include(a => a.TipoLlegadaManiana)
                .Include(a => a.TipoTarde)
                .FirstOrDefaultAsync(a => a.EstudianteId == dto.EstudianteId && a.Fecha == dto.Fecha);

            if (asistencia == null)
            {
                return new AsistenciaResponseDto
                {
                    Id = Guid.Empty,
                    ValorTotal = 0,
                    Mensaje = "No había registro para deshacer."
                };
            }

            if (esManana)
            {
                asistencia.TipoManianaId        = null;
                asistencia.TipoManiana           = null;
                asistencia.TipoLlegadaManianaId = null;
                asistencia.TipoLlegadaManiana   = null;
                asistencia.HoraEntradaManana    = null;
                asistencia.HoraSalidaManana     = null;
            }
            else
            {
                asistencia.TipoTardeId      = null;
                asistencia.TipoTarde        = null;
                asistencia.HoraEntradaTarde = null;
                asistencia.HoraSalidaTarde  = null;
            }

            await _context.SaveChangesAsync();
            await ProcesarAsistenciaEspacios(new List<Asistencia> { asistencia },
                new Dictionary<Guid, HashSet<string>> { { asistencia.EstudianteId, new HashSet<string> { esManana ? "MANANA" : "TARDE" } } });

            try
            {
                await _umbrales.ProcesarUmbralesAsync(new List<Guid> { dto.EstudianteId }, dto.Fecha.Year);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error procesando umbrales tras deshacer asistencia.");
            }

            return new AsistenciaResponseDto
            {
                Id = asistencia.Id,
                ValorTotal = asistencia.ValorTotalInasistencia,
                Mensaje = $"Se restableció el turno {turno} a sin definir."
            };
        }

        // [ Helper Privado ] Registra en el parte diario los cambios de estado de asistencia del lote.
        private async Task LogearCambiosAsistenciaAsync(
            List<RegistrarAsistenciaDto> lista,
            List<DetalleCursado> inscripciones,
            List<Asistencia> asistenciasPost,
            Dictionary<(Guid, DateOnly), string?> oldManana,
            Dictionary<(Guid, DateOnly), string?> oldTarde,
            Dictionary<Guid, (string Nombre, string Apellido)> estudiantesDict)
        {
            var cambiosPorGrupo = new Dictionary<(Guid CursoId, DateOnly Fecha), List<string>>();
            var procesados      = new HashSet<(Guid, DateOnly, bool)>();

            foreach (var dto in lista)
            {
                bool esManana = dto.Turno?.Trim().ToUpperInvariant() is "MANANA" or "MAÑANA" or "M" or "AM";

                if (!procesados.Add((dto.EstudianteId, dto.Fecha, esManana))) continue;

                var inscripcion = inscripciones.FirstOrDefault(i => i.IdEstudiante == dto.EstudianteId);
                if (inscripcion == null) continue;

                var key = (dto.EstudianteId, dto.Fecha);
                string? oldCode = esManana
                    ? (oldManana.TryGetValue(key, out var v1) ? v1 : null)
                    : (oldTarde .TryGetValue(key, out var v2) ? v2 : null);

                var asist   = asistenciasPost.FirstOrDefault(a => a.EstudianteId == dto.EstudianteId && a.Fecha == dto.Fecha);
                string? newCode = esManana ? asist?.TipoManiana?.Codigo : asist?.TipoTarde?.Codigo;

                if (oldCode == newCode) continue;

                estudiantesDict.TryGetValue(dto.EstudianteId, out var est);
                string nombre = est != default ? $"{est.Apellido}, {est.Nombre}" : dto.EstudianteId.ToString("N")[..8];
                string turnoLabel = esManana ? "M" : "T";

                var grupoKey = (inscripcion.IdCurso, dto.Fecha);
                if (!cambiosPorGrupo.ContainsKey(grupoKey)) cambiosPorGrupo[grupoKey] = new List<string>();
                cambiosPorGrupo[grupoKey].Add($"• {nombre} ({turnoLabel}): {oldCode ?? "—"} → {newCode ?? "—"}");
            }

            foreach (var ((cursoId, fecha), cambios) in cambiosPorGrupo)
            {
                if (!cambios.Any()) continue;
                string plural = cambios.Count == 1 ? "estudiante" : "estudiantes";
                string msg    = $"Asistencia actualizada ({cambios.Count} {plural}):\n{string.Join("\n", cambios)}";
                try   { await _parteDiarioService.RegistrarEventoAsync(cursoId, fecha, msg); }
                catch (Exception ex) { _logger.LogWarning(ex, "No se pudo registrar el evento de asistencia en el parte diario."); }
            }
        }

        private static string NormalizarTurno(string turno)
        {
            if (string.IsNullOrWhiteSpace(turno))
                throw new AsistenciaException("INVALID_TURNO", "Turno inválido");

            var t = turno.Trim().ToUpperInvariant();

            if (t is "MAÑANA" or "MANANA" or "M" or "AM")
                return "MANANA";

            if (t is "TARDE" or "T" or "PM")
                return "TARDE";

            throw new AsistenciaException("INVALID_TURNO", $"Turno inválido: {turno}");
        }

    }
}

