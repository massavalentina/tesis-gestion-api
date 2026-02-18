using Microsoft.EntityFrameworkCore;
using RepoDB.Entities;
using TesisGestorApi.Data;
using TesisGestorApi.DTOs;
using TesisGestorApi.Entities;

namespace TesisGestorApi.Services
{
    public class AsistenciaService : IAsistenciaService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AsistenciaService> _logger;

        public AsistenciaService(ApplicationDbContext context, ILogger<AsistenciaService> logger)
        {
            _context = context;
            _logger = logger;
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
                .Include(a => a.TipoTarde)
                .Where(a => idsEstudiantes.Contains(a.EstudianteId) && fechas.Contains(a.Fecha))
                .ToListAsync();

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

                string codigoOriginal = tipoEntidadOriginal.Codigo.ToUpper();
                var turno = dto.Turno?.Trim().ToUpper();
                bool esManana = turno == "MANANA";

                // Lógica de cambio de código para los retiros - Vía frontend solo se carga RA

                TipoAsistencia tipoFinal = tipoEntidadOriginal;

                if (codigoOriginal == "RA" && dto.Hora.HasValue)
                {
                    var inscripcion = inscripciones.FirstOrDefault(i => i.IdEstudiante == dto.EstudianteId);
                    if (inscripcion != null)
                    {
                        // Se filtran los Horarios del Curso para este día y turno
                        var horariosTurno = horarios
                            .Where(h => h.IdCurso == inscripcion.IdCurso &&
                                        h.DíaSemana == dto.Fecha.DayOfWeek &&
                                        (esManana ? h.HorarioEntrada.Hours < 13 : h.HorarioEntrada.Hours >= 13))
                            .ToList();

                        // Se calcula el porcentaje de clases perdido en el retiro
                        double porcPerdido = CalcularPorcentajePerdidoHelper(horariosTurno, dto.Hora.Value, clasesDictadasLocales, dto.Fecha);

                        // Lógica de negocio para los retiros - De acuerdo al tiempo de porcentaje perdido, se coloca  RA, RE o RAE.
                        if (esManana)
                        {
                            if (porcPerdido > 50 && tiposPorCodigo.ContainsKey("RAE"))
                                tipoFinal = tiposPorCodigo["RAE"]; // Si el porcentaje > 50%, el retiro es Extendido
                            else if (porcPerdido <= 10 && tiposPorCodigo.ContainsKey("RE"))
                                tipoFinal = tiposPorCodigo["RE"];   // Si el porcentaje es <= 10% es Express
                        }
                        else // Tarde
                        {
                            if (porcPerdido <= 10 && tiposPorCodigo.ContainsKey("RE"))
                                tipoFinal = tiposPorCodigo["RE"];
                            // En la tarde, solo hay RA. Si el tiempo es <= a 10%, es RE, que no computa insasistencia. 
                        }
                    }
                }

                // Se define si la asistencia marcada es para Ingreso o Salida, basandose en el tipo de asistencia final.
                bool esHorarioSalida = tipoFinal.Codigo.StartsWith("RA") || tipoFinal.Codigo == "RE";
                bool esHorarioEntrada = tipoFinal.Codigo.StartsWith("LL") || tipoFinal.Codigo == "P";

                // Dependiendo si es para la mañana o la tarde, se asigna el tipo de asistencia y la hora correspondiente (Entrada o Salida)
                if (esManana)
                {
                    asistencia.TipoManianaId = tipoFinal.IdTipo; 
                    asistencia.TipoManiana = tipoFinal;

                    if (dto.Hora.HasValue)
                    {
                        if (esHorarioEntrada) asistencia.HoraEntradaManana = dto.Hora.Value;
                        else if (esHorarioSalida) asistencia.HoraSalidaManana = dto.Hora.Value;
                    }
                }
                else // Tarde
                {
                    asistencia.TipoTardeId = tipoFinal.IdTipo;
                    asistencia.TipoTarde = tipoFinal;

                    if (dto.Hora.HasValue)
                    {
                        if (esHorarioEntrada) asistencia.HoraEntradaTarde = dto.Hora.Value;
                        else if (esHorarioSalida) asistencia.HoraSalidaTarde = dto.Hora.Value;
                    }
                }

                cont++;
            }

            await _context.SaveChangesAsync();

            // Completa la lógica de horarios y de tipo de asistencia, se dispara el procesamiento de asistencia en Espacios Curriculares.
            if (asistenciasParaProcesar.Any())
            {
                await ProcesarAsistenciaEspacios(asistenciasParaProcesar);
            }

            return cont;
        }


        // [ Helper Privado ] Cálculo de Porcentaje de Ausencia en Retiro Anticipado
        private double CalcularPorcentajePerdidoHelper(
            List<Horario> horariosTurno,
            TimeSpan horaRetiro,
            Dictionary<(Guid, DateOnly), ClaseDictada> clasesDictadasLocales,
            DateOnly fecha)
        {
            double minutosTotales = 0;
            double minutosPerdidos = 0;

            foreach (var h in horariosTurno)
            {
                // Se asume que la clase está dictada si no hay registros previos. 
                bool seDicto = !clasesDictadasLocales.TryGetValue((h.IdEC, fecha), out var cd) || cd.Dictada;
                if (!seDicto) continue;

                double duracion = (h.HorarioSalida - h.HorarioEntrada).TotalMinutes;
                if (duracion <= 0) continue;

                minutosTotales += duracion;

                if (horaRetiro < h.HorarioSalida)
                {
                    TimeSpan inicioPerdida = horaRetiro > h.HorarioEntrada ? horaRetiro : h.HorarioEntrada;
                    double perdidoEnModulo = (h.HorarioSalida - inicioPerdida).TotalMinutes;
                    if (perdidoEnModulo > 0) minutosPerdidos += perdidoEnModulo;
                }
            }

            return minutosTotales == 0 ? 0 : (minutosPerdidos / minutosTotales) * 100.0;
        }


        // [ Helper Público ] Procesa las asistencias por espacio y calcula el valor final
        public async Task ProcesarAsistenciaEspacios(List<Asistencia> asistenciasGenerales)
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

                // Variables acumuladoras para el cálculo delturno
                double minTotalesM = 0, minPerdidaIngresoM = 0, minPerdidaSalidaM = 0;
                double minTotalesT = 0, minPerdidaIngresoT = 0, minPerdidaSalidaT = 0;

                var gruposMaterias = horariosDelDia.GroupBy(h => h.IdEC).ToList();

                foreach (var grupoMateria in gruposMaterias)
                {
                    Guid idEC = grupoMateria.Key;
                    var key = (idEC, asistencia.Fecha);

                    if (!clasesDictadasLocales.ContainsKey(key))
                    {
                        var nuevaClase = new ClaseDictada { IdClaseDictada = Guid.NewGuid(), IdEC = idEC, Fecha = asistencia.Fecha, Dictada = true, Tema = "Generado Automáticamente" };
                        _context.ClasesDictadas.Add(nuevaClase);
                        clasesDictadasLocales[key] = nuevaClase;
                    }

                    var claseDictada = clasesDictadasLocales[key];
                    if (!claseDictada.Dictada) continue;

                    double minTotalesMateria = 0;
                    double minAsistidosMateria = 0;

                    foreach (var horario in grupoMateria)
                    {
                        double duracionModulo = (horario.HorarioSalida - horario.HorarioEntrada).TotalMinutes;
                        if (duracionModulo <= 0) continue;

                        // Acumula los minutos totales del Espacio Curricular en el día
                        minTotalesMateria += duracionModulo;

                        bool esManana = horario.HorarioEntrada.Hours < 13;
                        if (esManana) minTotalesM += duracionModulo; else minTotalesT += duracionModulo;

                        // Se obtiene el código para determinar si se aplica lógica de tiempo de asistencia o asignación directa
                        var tipoAsistencia = esManana ? asistencia.TipoManiana : asistencia.TipoTarde;
                        string codigo = tipoAsistencia?.Codigo?.ToUpper() ?? "-";

                        // Presencia Total 
                        if (codigo == "P" || codigo == "ANC" || codigo == "RE")
                        {
                            // Se asume que asistió todo el módulo sin pérdidas de tiempo.
                            minAsistidosMateria += duracionModulo;
                            continue; // Salta al siguiente horario
                        }

                        // Presencia Parcial
                        TimeSpan entradaAlumno = esManana ? (asistencia.HoraEntradaManana ?? horario.HorarioEntrada) : (asistencia.HoraEntradaTarde ?? horario.HorarioEntrada);
                        TimeSpan salidaAlumno = esManana ? (asistencia.HoraSalidaManana ?? horario.HorarioSalida) : (asistencia.HoraSalidaTarde ?? horario.HorarioSalida);

                        // Llegadas Tarde
                        if (entradaAlumno > horario.HorarioEntrada)
                        {
                            TimeSpan finPerdida = entradaAlumno < horario.HorarioSalida ? entradaAlumno : horario.HorarioSalida;
                            double perdido = (finPerdida - horario.HorarioEntrada).TotalMinutes;
                            if (perdido > 0)
                            {
                                if (esManana) minPerdidaIngresoM += perdido; else minPerdidaIngresoT += perdido;
                            }
                        }

                        // Retiros Anticipados
                        if (salidaAlumno < horario.HorarioSalida)
                        {
                            TimeSpan inicioPerdida = salidaAlumno > horario.HorarioEntrada ? salidaAlumno : horario.HorarioEntrada;
                            double perdido = (horario.HorarioSalida - inicioPerdida).TotalMinutes;
                            if (perdido > 0)
                            {
                                if (esManana) minPerdidaSalidaM += perdido; else minPerdidaSalidaT += perdido;
                            }
                        }

                        // Intersección entre tiempo de dictado y tiempo de asistencia
                        TimeSpan inicioEfectivo = horario.HorarioEntrada > entradaAlumno ? horario.HorarioEntrada : entradaAlumno;
                        TimeSpan finEfectivo = horario.HorarioSalida < salidaAlumno ? horario.HorarioSalida : salidaAlumno;
                        double asistidoEnModulo = (finEfectivo - inicioEfectivo).TotalMinutes;
                        if (asistidoEnModulo < 0) asistidoEnModulo = 0;

                        // Ausencia Total
                        if (codigo == "A" || codigo == "LLTC" || codigo == "RAE") asistidoEnModulo = 0; 
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
            var clase = await _context.ClasesDictadas
                .Include(c => c.Asistencias)
                .FirstOrDefaultAsync(c => c.IdEC == dto.IdEC && c.Fecha == dto.Fecha);

            if (clase == null)
            {
                clase = new ClaseDictada
                {
                    IdClaseDictada = Guid.NewGuid(),
                    IdEC = dto.IdEC,
                    Fecha = dto.Fecha,
                    Dictada = dto.Dictada,
                    Tema = dto.Tema
                };
                _context.ClasesDictadas.Add(clase);
                await _context.SaveChangesAsync();

                if (clase.Dictada) await RegenerarAsistenciasParaClase(clase);
                return;
            }

            bool estadoAnterior = clase.Dictada;
            clase.Dictada = dto.Dictada;
            if (!string.IsNullOrEmpty(dto.Tema)) clase.Tema = dto.Tema;

            if (estadoAnterior == true && dto.Dictada == false)
            {
                if (clase.Asistencias.Any()) _context.AsistenciasPorEspacio.RemoveRange(clase.Asistencias);
            }
            else if (estadoAnterior == false && dto.Dictada == true)
            {
                await _context.SaveChangesAsync();
                await RegenerarAsistenciasParaClase(clase);
                return;
            }

            await _context.SaveChangesAsync();
        }


        // [ Helper Público ] Regenera las asistencias de los EC de una clase específica en base al estado de Dictado (true o false) y recalcula en base a la información de asistencias generales.
        public async Task RegenerarAsistenciasParaClase(ClaseDictada clase)
        {
            var idCurso = await _context.EspaciosCurriculares.Where(ec => ec.IdEC == clase.IdEC).Select(ec => ec.IdCurso).FirstOrDefaultAsync();
            var idsAlumnos = await _context.DetallesCursado.Where(dc => dc.IdCurso == idCurso && dc.Estado == true).Select(dc => dc.IdEstudiante).ToListAsync();
            var asistenciasGenerales = await _context.Asistencias.Include(a => a.TipoManiana).Include(a => a.TipoTarde).Where(a => a.Fecha == clase.Fecha && idsAlumnos.Contains(a.EstudianteId)).ToListAsync();

            if (asistenciasGenerales.Any()) await ProcesarAsistenciaEspacios(asistenciasGenerales);
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
            var query = _context.Asistencias.AsNoTracking().Include(a => a.Estudiante).Include(a => a.TipoManiana).Include(a => a.TipoTarde).AsQueryable();
            if (fecha.HasValue) query = query.Where(a => a.Fecha == fecha.Value);
            if (estudianteId.HasValue) query = query.Where(a => a.EstudianteId == estudianteId.Value);

            return await query.Select(a => new AsistenciaGetDTO
            {
                Id = a.Id,
                Fecha = a.Fecha,
                ValorTotal = a.ValorTotalInasistencia,
                NombreCompleto = $"{a.Estudiante.Nombre} {a.Estudiante.Apellido}",
                Documento = a.Estudiante.Documento,
                CodigoManana = a.TipoManiana != null ? a.TipoManiana.Codigo : "-",
                CodigoTarde = a.TipoTarde != null ? a.TipoTarde.Codigo : "-"
            }).OrderByDescending(a => a.Fecha).ToListAsync();
        }
    }
}