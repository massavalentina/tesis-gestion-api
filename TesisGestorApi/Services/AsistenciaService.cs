using Microsoft.EntityFrameworkCore;
using RepoDB.Entities;
using TesisGestorApi.Data;
using TesisGestorApi.DTOs;
using TesisGestorApi.Interfaces;

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

        // =========================================
        // REGISTRO LOTE
        // =========================================

        public async Task<int> RegistrarLoteAsync(List<RegistrarAsistenciaDto> lista)
        {
            var idsTiposRequest = lista.Select(x => x.TipoAsistenciaId).Distinct().ToList();

            var tiposDict = await _context.TiposAsistencia
                .Where(t => idsTiposRequest.Contains(t.IdTipo))
                .ToDictionaryAsync(t => t.IdTipo);

            var idsEstudiantes = lista.Select(x => x.EstudianteId).Distinct().ToList();
            var fechas = lista.Select(x => x.Fecha).Distinct().ToList();

            var asistenciasExistentes = await _context.Asistencias
                .Include(a => a.TipoManiana)
                .Include(a => a.TipoLlegadaManiana)  // Necesario para preservar llegada al registrar retiro
                .Include(a => a.TipoTarde)
                .Where(a => idsEstudiantes.Contains(a.EstudianteId) && fechas.Contains(a.Fecha))
                .ToListAsync();

            int cont = 0;
            var asistenciasParaProcesar = new List<Asistencia>();

            foreach (var dto in lista)
            {
                if (!tiposDict.TryGetValue(dto.TipoAsistenciaId, out var tipoEntidad))
                {
                    _logger.LogWarning($"Tipo {dto.TipoAsistenciaId} no encontrado.");
                    continue;
                }

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

                var turno = (dto.Turno ?? "MANANA").ToUpper();
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
                                        (esManana ? h.HorarioEntrada.Hours < 13 : h.HorarioEntrada.Hours >= 13))
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
                TimeSpan hora = dto.Hora ?? TimeOnly.FromDateTime(DateTime.Now).ToTimeSpan();

                if (esManana)
                {
                    asistencia.TipoManianaId = tipoEntidad.IdTipo;
                    asistencia.TipoManiana = tipoEntidad;
                    asistencia.HoraEntradaManana = hora;
                }
                else
                {
                    asistencia.TipoTardeId = tipoEntidad.IdTipo;
                    asistencia.TipoTarde = tipoEntidad;
                    asistencia.HoraEntradaTarde = hora;
                }

                asistenciasParaProcesar.Add(asistencia);
                cont++;
            }

            await _context.SaveChangesAsync();

            // Con la lógica de horarios y tipo de asistencia completa se dispara el procesamiento de asistencia en Espacios Curriculares.
            if (asistenciasParaProcesar.Any())
                await ProcesarAsistenciaEspacios(asistenciasParaProcesar);

            return cont;
        }

        // =========================================
        // REGISTRO INDIVIDUAL
        // =========================================

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
        public async Task<AsistenciaResponseDto> RegistrarAsistenciaIndividualAsync(RegistrarAsistenciaDto dto)
        {
            await RegistrarLoteAsync(new List<RegistrarAsistenciaDto> { dto });

            var entidad = await _context.Asistencias
                .AsNoTracking()
                .FirstAsync(a => a.EstudianteId == dto.EstudianteId && a.Fecha == dto.Fecha);

            return new AsistenciaResponseDto
            {
                // Si la clase está marcada como no dictada, no cuenta en el total de minutos de clase del turno.
                // Si no hay registro (clase aún no procesada), se asume que sí se dictó.
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
                Id = entidad.Id,
                ValorTotal = entidad.ValorTotalInasistencia,
                Mensaje = "Registrado correctamente."
            };
        }

        // =========================================
        // OBTENER ASISTENCIAS
        // =========================================

        public async Task<IEnumerable<AsistenciaGetDTO>> ObtenerAsistenciasAsync(DateOnly? fecha, Guid? estudianteId)
        {
            var query = _context.Asistencias
                .AsNoTracking()
                .Include(a => a.Estudiante)
                .Include(a => a.TipoManiana)
                .Include(a => a.TipoTarde)
                .AsQueryable();

            if (fecha.HasValue)
                query = query.Where(a => a.Fecha == fecha.Value);

            if (estudianteId.HasValue)
                query = query.Where(a => a.EstudianteId == estudianteId.Value);

            return await query
                .Select(a => new AsistenciaGetDTO
                {
                    Id = a.Id,
                    Fecha = a.Fecha,
                    ValorTotal = a.ValorTotalInasistencia,
                    NombreCompleto = $"{a.Estudiante.Nombre} {a.Estudiante.Apellido}",
                    Documento = a.Estudiante.Documento,
                    CodigoManana = a.TipoManiana != null ? a.TipoManiana.Codigo : "-",
                    CodigoTarde = a.TipoTarde != null ? a.TipoTarde.Codigo : "-"
                })
                .OrderByDescending(a => a.Fecha)
                .ToListAsync();
        }

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
        // =========================================
        // DESHACER ASISTENCIA RAPIDA
        // =========================================

        public async Task<AsistenciaResponseDto> DeshacerAsistenciaRapidaAsync(DeshacerAsistenciaRapidaDto dto)
        {
            var turno = (dto.Turno ?? "MANANA").Trim().ToUpperInvariant();

            if (turno == "MANIANA")
                turno = "MANANA";

            bool esManana = turno == "MANANA";

            var asistencia = await _context.Asistencias
                .Include(a => a.TipoManiana)
                .Include(a => a.TipoTarde)
                .FirstOrDefaultAsync(a =>
                    a.EstudianteId == dto.EstudianteId &&
                    a.Fecha == dto.Fecha);

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
                asistencia.TipoManianaId = tipoP.IdTipo;
                asistencia.TipoManiana = tipoP;
                asistencia.HoraEntradaManana = null;
                asistencia.HoraSalidaManana = null;
            }
            else
            {
                asistencia.TipoTardeId = tipoP.IdTipo;
                asistencia.TipoTarde = tipoP;
                asistencia.HoraEntradaTarde = null;
                asistencia.HoraSalidaTarde = null;
            }

            await _context.SaveChangesAsync();

            await ProcesarAsistenciaEspacios(new List<Asistencia> { asistencia });

            return new AsistenciaResponseDto
            {
                Id = asistencia.Id,
                ValorTotal = asistencia.ValorTotalInasistencia,
                Mensaje = $"Se deshizo el registro del turno {turno} correctamente."
            };
        }

        // =========================================
        // PROCESAMIENTO DE ESPACIOS
        // =========================================

        public async Task ProcesarAsistenciaEspacios(List<Asistencia> asistenciasGenerales)
        {
            foreach (var asistencia in asistenciasGenerales)
            {
                asistencia.ValorTotalInasistencia = 0;
            }

            await _context.SaveChangesAsync();
        }

        // =========================================
        // CLASE DICTADA
        // =========================================

        public async Task ActualizarEstadoClaseAsync(ClaseDictadaDTO dto)
        {
            var clase = await _context.ClasesDictadas
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
            }
            else
            {
                clase.Dictada = dto.Dictada;
                clase.Tema = dto.Tema;
            }

            await _context.SaveChangesAsync();
        }

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
            var asistencias = await _context.Asistencias
                .Where(a => a.Fecha == clase.Fecha)
                .ToListAsync();

            await ProcesarAsistenciaEspacios(asistencias);
        }

    }
}
