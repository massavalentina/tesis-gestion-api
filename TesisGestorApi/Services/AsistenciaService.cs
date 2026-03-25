using Microsoft.EntityFrameworkCore;
using RepoDB.Entities;
using TesisGestorApi.Data;
using TesisGestorApi.DTOs;
using TesisGestorApi.Dtos;
using TesisGestorApi.Exceptions;
using TesisGestorApi.Interfaces;

namespace TesisGestorApi.Services
{
    public class AsistenciaService : IAsistenciaService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AsistenciaService> _logger;
        private readonly IParteDiarioService _parteDiarioService;

        public AsistenciaService(ApplicationDbContext context, ILogger<AsistenciaService> logger, IParteDiarioService parteDiarioService)
        {
            _context = context;
            _logger = logger;
            _parteDiarioService = parteDiarioService;
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

            // Caché de clases dictadas
            var clasesIdsEC = horarios.Select(h => h.IdEC).Distinct().ToList();
            var clasesDictadasDb = await _context.ClasesDictadas
                .AsNoTracking()
                .Where(c => clasesIdsEC.Contains(c.IdEC) && fechas.Contains(c.Fecha))
                .ToListAsync();

            var clasesDictadasLocales = new Dictionary<(Guid, DateOnly), ClaseDictada>();
            foreach (var c in clasesDictadasDb) clasesDictadasLocales[(c.IdEC, c.Fecha)] = c;

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
                var turno = dto.Turno?.Trim().ToUpper();
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

            // Agrupar por EC para manejar overrides de horario y multi-módulos correctamente
            foreach (var grupoEC in horariosTurno.GroupBy(h => h.IdEC))
            {
                clasesDictadasLocales.TryGetValue((grupoEC.Key, fecha), out var cd);
                if (cd != null && !cd.Dictada) continue;

                TimeSpan horaEntradaEf = cd?.HorarioEntradaEfectiva
                    ?? grupoEC.OrderBy(h => h.HorarioEntrada).First().HorarioEntrada;
                TimeSpan horaSalidaEf = cd?.HorarioSalidaEfectiva
                    ?? grupoEC.OrderBy(h => h.HorarioSalida).Last().HorarioSalida;

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


        // [ Helper Público ] Procesa las asistencias por espacio y calcula el valor final
        // turnosPorEstudiante: para cada estudiante, qué turno(s) fueron explícitamente registrados en este request.
        // Si es null, no se aplica filtro por turno (solo se salta si tipoTurnoEc == null).
        public async Task ProcesarAsistenciaEspacios(List<Asistencia> asistenciasGenerales, Dictionary<Guid, HashSet<string>>? turnosPorEstudiante = null)
        {
            // Se preparan los datos que se van a consultar con frecuencia
            var estudiantesIds = asistenciasGenerales.Select(a => a.EstudianteId).Distinct().ToList();
            var fechas = asistenciasGenerales.Select(a => a.Fecha).Distinct().ToList();
            var diasSemana = fechas.Select(f => f.DayOfWeek).Distinct().ToList();

            var inscripciones = await _context.DetallesCursado.AsNoTracking()
                .Where(dc => estudiantesIds.Contains(dc.IdEstudiante) && dc.Estado == true).ToListAsync();
            var cursosIds = inscripciones.Select(i => i.IdCurso).Distinct().ToList();
            var horarios = await _context.Horarios.AsNoTracking()
                .Where(h => cursosIds.Contains(h.IdCurso) && diasSemana.Contains(h.DíaSemana)).ToListAsync();

            // Cache de clases
            var clasesDictadasLocales = new Dictionary<(Guid IdEC, DateOnly Fecha), ClaseDictada>();
            var clasesDbIds = horarios.Select(h => h.IdEC).Distinct().ToList();
            var clasesExistentes = await _context.ClasesDictadas
                .Where(c => clasesDbIds.Contains(c.IdEC) && fechas.Contains(c.Fecha)).ToListAsync();
            foreach (var c in clasesExistentes) clasesDictadasLocales[(c.IdEC, c.Fecha)] = c;

            var nuevasAsistenciasEspacio = new List<AsistenciaPorEspacio>();

            // Procesamiento de cada asistencia
            foreach (var asistencia in asistenciasGenerales)
            {
                var inscripcion = inscripciones.FirstOrDefault(i => i.IdEstudiante == asistencia.EstudianteId);
                if (inscripcion == null) continue;

                var horariosDelDia = horarios
                    .Where(h => h.IdCurso == inscripcion.IdCurso && h.DíaSemana == asistencia.Fecha.DayOfWeek)
                    .ToList();

                // Variables acumuladoras para el cálculo del turno
                double minTotalesM = 0, minPerdidaIngresoM = 0, minPerdidaSalidaM = 0;
                double minTotalesT = 0, minPerdidaIngresoT = 0, minPerdidaSalidaT = 0;

                var gruposMaterias = horariosDelDia.GroupBy(h => h.IdEC).ToList();

                foreach (var grupoMateria in gruposMaterias)
                {
                    Guid idEC = grupoMateria.Key;
                    var key = (idEC, asistencia.Fecha);

                    // Determinar el turno del EC ANTES de cualquier operación sobre ClaseDictada o AsistenciaPorEspacio.
                    // Cada turno es independiente: solo se procesan los ECs del turno que tiene asistencia registrada.
                    var primerModulo = grupoMateria.First();
                    bool esEcManana = primerModulo.HorarioEntrada < new TimeSpan(13, 20, 0);

                    // Filtro por turno POR ESTUDIANTE: solo procesar ECs cuyo turno fue explícitamente
                    // registrado para este estudiante en este request. Esto evita que un turno registrado
                    // para otro estudiante (o cargado desde la BD) habilite ECs del turno incorrecto.
                    if (turnosPorEstudiante != null)
                    {
                        string ecTurno = esEcManana ? "MANANA" : "TARDE";
                        if (!turnosPorEstudiante.TryGetValue(asistencia.EstudianteId, out var turnosEst)
                            || !turnosEst.Contains(ecTurno))
                            continue;
                    }

                    var tipoTurnoEc = esEcManana ? asistencia.TipoManiana : asistencia.TipoTarde;

                    // Si el turno correspondiente a este EC no tiene asistencia registrada:
                    // se elimina el AsistenciaPorEspacio existente (stale data de ejecuciones anteriores o de SA)
                    // y se omite la creación de ClaseDictada y nuevos registros.
                    if (tipoTurnoEc == null)
                    {
                        if (clasesDictadasLocales.TryGetValue(key, out var claseStale))
                        {
                            var stale = await _context.AsistenciasPorEspacio
                                .FirstOrDefaultAsync(ae => ae.IdClaseDictada == claseStale.IdClaseDictada
                                                        && ae.IdEstudiante   == asistencia.EstudianteId);
                            if (stale != null) _context.AsistenciasPorEspacio.Remove(stale);
                        }
                        continue;
                    }

                    string codigoTurnoEc = tipoTurnoEc.Codigo.ToUpper();

                    if (!clasesDictadasLocales.ContainsKey(key))
                    {
                        // Si no existe la clase dictada, se crea con "Dictada = true" ya que si se está registrando asistencia para este EC
                        // en esta fecha, la clase se asume dictada. Después se puede marcar como "no dictada" mediante otro método,
                        // lo que borrará las asistencias por espacio.
                        var nuevaClase = new ClaseDictada { IdClaseDictada = Guid.NewGuid(), IdEC = idEC, Fecha = asistencia.Fecha, Dictada = true, Tema = "Generado Automáticamente" };
                        _context.ClasesDictadas.Add(nuevaClase);
                        clasesDictadasLocales[key] = nuevaClase;
                    }

                    var claseDictada = clasesDictadasLocales[key];
                    // Si la clase fue marcada como no dictada, sus minutos no acumulan en minTotalesM/minTotalesT ni se crean AsistenciasPorEspacio.
                    if (!claseDictada.Dictada) continue;

                    // ANC: asistencia no computable → el EC queda en null (se elimina el registro si existía)
                    if (codigoTurnoEc == "ANC")
                    {
                        var existenteAnc = await _context.AsistenciasPorEspacio
                            .FirstOrDefaultAsync(ae => ae.IdClaseDictada == claseDictada.IdClaseDictada && ae.IdEstudiante == asistencia.EstudianteId);
                        if (existenteAnc != null) _context.AsistenciasPorEspacio.Remove(existenteAnc);
                        continue;
                    }

                    double minTotalesMateria = 0;
                    double minAsistidosMateria = 0;

                    // Offset de tiempo efectivo: desplaza todos los módulos del EC si el preceptor
                    // lo movió a un horario distinto del programado (HorarioEntradaEfectiva != null).
                    var primerModuloEC = grupoMateria.OrderBy(h => h.HorarioEntrada).First();
                    TimeSpan offsetTiempo = claseDictada.HorarioEntradaEfectiva.HasValue
                        ? claseDictada.HorarioEntradaEfectiva.Value - primerModuloEC.HorarioEntrada
                        : TimeSpan.Zero;

                    foreach (var horario in grupoMateria)
                    {
                        double duracionModulo = (horario.HorarioSalida - horario.HorarioEntrada).TotalMinutes;
                        if (duracionModulo <= 0) continue;

                        // Acumula los minutos totales del Espacio Curricular en el día
                        minTotalesMateria += duracionModulo;

                        // El turno se determina siempre con el horario original (un EC no cambia de turno al moverlo)
                        bool esManana = horario.HorarioEntrada < new TimeSpan(13, 20, 0);
                        if (esManana) minTotalesM += duracionModulo; else minTotalesT += duracionModulo;

                        // Se obtiene el código para determinar si se aplica lógica de tiempo de asistencia o asignación directa
                        var tipoAsistencia = esManana ? asistencia.TipoManiana : asistencia.TipoTarde;
                        string codigo = tipoAsistencia?.Codigo?.ToUpper() ?? "-";

                        // Presencia Total: P y RE siempre cuentan como asistencia completa al módulo.
                        // RE = retiro con ≤10% perdido del turno → siempre < 20% por EC → siempre presente.
                        if (codigo == "P" || codigo == "RE")
                        {
                            minAsistidosMateria += duracionModulo;
                            continue;
                        }

                        // Tiempos efectivos del módulo (desplazados si el EC fue movido)
                        TimeSpan entradaEfMod = horario.HorarioEntrada + offsetTiempo;
                        TimeSpan salidaEfMod  = horario.HorarioSalida  + offsetTiempo;

                        // Presencia Parcial
                        // Solo usar la hora de entrada real cuando el código de llegada es una llegada tarde
                        // (LLT/LLTE/LLTC). Para "P" el docente pudo haberlo registrado después del inicio,
                        // pero el alumno fue marcado como presente → se toma el horario efectivo.
                        string? codigoLlegada = esManana
                            ? asistencia.TipoLlegadaManiana?.Codigo?.ToUpper()
                            : asistencia.TipoTarde?.Codigo?.ToUpper();
                        bool usarHoraEntradaReal = codigoLlegada is "LLT" or "LLTE" or "LLTC";
                        TimeSpan entradaAlumno = usarHoraEntradaReal
                            ? (esManana ? (asistencia.HoraEntradaManana ?? entradaEfMod) : (asistencia.HoraEntradaTarde ?? entradaEfMod))
                            : entradaEfMod;
                        TimeSpan salidaAlumno = esManana ? (asistencia.HoraSalidaManana ?? salidaEfMod) : (asistencia.HoraSalidaTarde ?? salidaEfMod);

                        // Llegadas Tarde
                        if (entradaAlumno > entradaEfMod)
                        {
                            TimeSpan finPerdida = entradaAlumno < salidaEfMod ? entradaAlumno : salidaEfMod;
                            double perdido = (finPerdida - entradaEfMod).TotalMinutes;
                            if (perdido > 0)
                            {
                                if (esManana) minPerdidaIngresoM += perdido; else minPerdidaIngresoT += perdido;
                            }
                        }

                        // Retiros Anticipados
                        if (salidaAlumno < salidaEfMod)
                        {
                            TimeSpan inicioPerdida = salidaAlumno > entradaEfMod ? salidaAlumno : entradaEfMod;
                            double perdido = (salidaEfMod - inicioPerdida).TotalMinutes;
                            if (perdido > 0)
                            {
                                if (esManana) minPerdidaSalidaM += perdido; else minPerdidaSalidaT += perdido;
                            }
                        }

                        // Intersección entre tiempo de dictado y tiempo de asistencia
                        TimeSpan inicioEfectivo = entradaEfMod > entradaAlumno ? entradaEfMod : entradaAlumno;
                        TimeSpan finEfectivo    = salidaEfMod  < salidaAlumno  ? salidaEfMod  : salidaAlumno;
                        double asistidoEnModulo = (finEfectivo - inicioEfectivo).TotalMinutes;
                        if (asistidoEnModulo < 0) asistidoEnModulo = 0;

                        // Ausencia Total: A fuerza 0 minutos asistidos.
                        // LLTC y RAE (>50% turno perdido) también pasan por cálculo parcial por EC:
                        // el estudiante puede haber asistido a algunos ECs → la regla del 20% determina por EC.
                        if (codigo == "A") asistidoEnModulo = 0;
                        else minAsistidosMateria += asistidoEnModulo;
                    }

                    // Regla de negocio del 20% - Si el tiempo de ausencia en el día a un espacio curricular es mayor al 20%, se marca como ausencia total.
                    bool presenteMateria = true;
                    string motivoMateria = "P";

                    if (minTotalesMateria > 0)
                    {
                        double porcentajeAusencia = 100.0 - ((minAsistidosMateria / minTotalesMateria) * 100.0);
                        if (porcentajeAusencia > 20.0)
                        {
                            presenteMateria = false;
                            motivoMateria = minAsistidosMateria == 0 ? "Ausente Total" : $"Ausencia Parcial ({Math.Round(porcentajeAusencia)}%)";
                        }
                    }

                    // Upsert
                    var asistenciaEspacioExistente = await _context.AsistenciasPorEspacio.FirstOrDefaultAsync(ae => ae.IdClaseDictada == claseDictada.IdClaseDictada && ae.IdEstudiante == asistencia.EstudianteId);
                    if (asistenciaEspacioExistente != null)
                    {
                        asistenciaEspacioExistente.Presente = presenteMateria;
                        asistenciaEspacioExistente.Motivo = motivoMateria;
                    }
                    else
                    {
                        nuevasAsistenciasEspacio.Add(new AsistenciaPorEspacio { IdAsistenciaEspacio = Guid.NewGuid(), IdClaseDictada = claseDictada.IdClaseDictada, IdEstudiante = asistencia.EstudianteId, Presente = presenteMateria, Motivo = motivoMateria });
                    }
                }

                // Cálculo Final de Asistencia General Diaria
                asistencia.CalcularAsistencia(minTotalesM, minPerdidaIngresoM, minPerdidaSalidaM, minTotalesT, minPerdidaIngresoT, minPerdidaSalidaT);
            }

            // Save en base de datos
            if (nuevasAsistenciasEspacio.Any()) _context.AsistenciasPorEspacio.AddRange(nuevasAsistenciasEspacio);
            await _context.SaveChangesAsync();
        }

        // [ PUT ] Actualizar estado de clase de Dictada a No Dictada y viceversa.
        public async Task ActualizarEstadoClaseAsync(ClaseDictadaDTO dto)
        {
            // Obtener datos del EC para el mensaje de evento y el curso
            var ec = await _context.EspaciosCurriculares
                .AsNoTracking()
                .Include(e => e.Curricula)
                .FirstOrDefaultAsync(e => e.IdEC == dto.IdEC);

            var clase = await _context.ClasesDictadas
                .Include(c => c.Asistencias)
                .FirstOrDefaultAsync(c => c.IdEC == dto.IdEC && c.Fecha == dto.Fecha);

            if (clase == null)
            {
                clase = new ClaseDictada
                {
                    IdClaseDictada = Guid.NewGuid(),
                    IdEC    = dto.IdEC,
                    Fecha   = dto.Fecha,
                    Dictada = dto.Dictada,
                    Tema    = dto.Tema,
                    Motivo  = dto.Motivo,
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
            if (ec != null)
            {
                string materiaNombre = ec.Curricula?.Nombre ?? "Clase";
                string estadoTexto   = dto.Dictada ? "Dictada" : "No Dictada";
                string descripcion   = string.IsNullOrWhiteSpace(dto.Motivo)
                    ? $"{materiaNombre} marcada como {estadoTexto}"
                    : $"{materiaNombre} marcada como {estadoTexto}. Motivo: {dto.Motivo}";

                await _parteDiarioService.RegistrarEventoAsync(ec.IdCurso, dto.Fecha, descripcion);
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

            // Determinar el turno del EC regenerado para evitar crear ClaseDictadas
            // en ECs del turno contrario como efecto secundario.
            var primerHorario = await _context.Horarios.AsNoTracking()
                .Where(h => h.IdEC == clase.IdEC)
                .OrderBy(h => h.HorarioEntrada)
                .FirstOrDefaultAsync();
            bool esManana = primerHorario != null && primerHorario.HorarioEntrada < new TimeSpan(13, 20, 0);
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

            return ecs.SelectMany(ec =>
                ec.Horarios.Select(h => {
                    var clase = ec.ClasesDictadas.FirstOrDefault();
                    var asist = clase?.Asistencias.FirstOrDefault();
                    return new AsistenciaEspacioItemDto
                    {
                        IdAsistenciaEspacio = asist?.IdAsistenciaEspacio,
                        IdEC                = ec.IdEC,
                        IdClaseDictada      = clase?.IdClaseDictada,
                        NombreMateria       = ec.Curricula.Nombre,
                        HorarioEntrada      = h.HorarioEntrada.ToString(@"hh\:mm"),
                        HorarioSalida       = h.HorarioSalida.ToString(@"hh\:mm"),
                        Dictada             = clase?.Dictada,
                        Presente            = clase?.Dictada == true ? asist?.Presente : null,
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

            var tipoP = await _context.TiposAsistencia
                .FirstOrDefaultAsync(t => t.Codigo.ToUpper() == "P");

            if (tipoP == null)
                throw new Exception("No existe el tipo 'P'.");

            if (esManana)
            {
                asistencia.TipoManianaId        = tipoP.IdTipo;
                asistencia.TipoManiana           = tipoP;
                asistencia.TipoLlegadaManianaId = tipoP.IdTipo;
                asistencia.TipoLlegadaManiana   = tipoP;
                asistencia.HoraEntradaManana    = null;
                asistencia.HoraSalidaManana     = null;
            }
            else
            {
                asistencia.TipoTardeId      = tipoP.IdTipo;
                asistencia.TipoTarde        = tipoP;
                asistencia.HoraEntradaTarde = null;
                asistencia.HoraSalidaTarde  = null;
            }

            await _context.SaveChangesAsync();
            await ProcesarAsistenciaEspacios(new List<Asistencia> { asistencia },
                new Dictionary<Guid, HashSet<string>> { { asistencia.EstudianteId, new HashSet<string> { esManana ? "MANANA" : "TARDE" } } });

            return new AsistenciaResponseDto
            {
                Id = asistencia.Id,
                ValorTotal = asistencia.ValorTotalInasistencia,
                Mensaje = $"Se deshizo el registro del turno {turno} correctamente."
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
