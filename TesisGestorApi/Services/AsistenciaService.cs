using Microsoft.EntityFrameworkCore;
using TesisGestorApi.Data;
using TesisGestorApi.DTOs;
using TesisGestorApi.Dtos;
using TesisGestorApi.Interfaces;
using TesisGestorApi.Entities;

namespace TesisGestorApi.Services
{
    public class AsistenciaService : IAsistenciaService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AsistenciaService> _logger;
        private readonly IAsistenciaUmbralService _umbrales;

        public AsistenciaService(
            ApplicationDbContext context,
            ILogger<AsistenciaService> logger,
            IAsistenciaUmbralService umbrales)
        {
            _context = context;
            _logger = logger;
            _umbrales = umbrales;
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

                var turno = (dto.Turno ?? "MANANA").Trim().ToUpperInvariant();
                if (turno == "MANIANA")
                    turno = "MANANA";

                bool esManana = turno == "MANANA";

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

                if (!asistenciasParaProcesar.Contains(asistencia))
                    asistenciasParaProcesar.Add(asistencia);

                cont++;
            }

            await _context.SaveChangesAsync();

            if (asistenciasParaProcesar.Any())
                await ProcesarAsistenciaEspacios(asistenciasParaProcesar);

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

        // =========================================
        // REGISTRO INDIVIDUAL
        // =========================================

        public async Task<AsistenciaResponseDto> RegistrarAsistenciaIndividualAsync(RegistrarAsistenciaDto dto)
        {
            await RegistrarLoteAsync(new List<RegistrarAsistenciaDto> { dto });

            var entidad = await _context.Asistencias
                .AsNoTracking()
                .FirstAsync(a => a.EstudianteId == dto.EstudianteId && a.Fecha == dto.Fecha);

            return new AsistenciaResponseDto
            {
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
                asistencia.TipoManianaId = null;
                asistencia.TipoManiana = null;
                asistencia.HoraEntradaManana = null;
                asistencia.HoraSalidaManana = null;
            }
            else
            {
                asistencia.TipoTardeId = null;
                asistencia.TipoTarde = null;
                asistencia.HoraEntradaTarde = null;
                asistencia.HoraSalidaTarde = null;
            }

            await _context.SaveChangesAsync();

            await ProcesarAsistenciaEspacios(new List<Asistencia> { asistencia });

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

        // =========================================
        // PROCESAMIENTO DE ESPACIOS
        // =========================================

        public async Task ProcesarAsistenciaEspacios(List<Asistencia> asistenciasGenerales)
        {
            if (asistenciasGenerales == null || !asistenciasGenerales.Any())
                return;

            var estudiantesIds = asistenciasGenerales.Select(a => a.EstudianteId).Distinct().ToList();
            var fechas = asistenciasGenerales.Select(a => a.Fecha).Distinct().ToList();
            var diasSemana = fechas.Select(f => f.DayOfWeek).Distinct().ToList();

            var inscripciones = await _context.DetallesCursado
                .AsNoTracking()
                .Where(dc => estudiantesIds.Contains(dc.IdEstudiante) && dc.Estado == true)
                .ToListAsync();

            var cursosIds = inscripciones.Select(i => i.IdCurso).Distinct().ToList();

            var horarios = await _context.Horarios
                .AsNoTracking()
                .Where(h => cursosIds.Contains(h.IdCurso) && diasSemana.Contains(h.DíaSemana))
                .ToListAsync();

            var clasesDbIds = horarios.Select(h => h.IdEC).Distinct().ToList();

            var clasesExistentes = await _context.ClasesDictadas
                .Where(c => clasesDbIds.Contains(c.IdEC) && fechas.Contains(c.Fecha))
                .ToListAsync();

            var clasesDictadasLocales = new Dictionary<(Guid IdEC, DateOnly Fecha), ClaseDictada>();
            foreach (var c in clasesExistentes)
                clasesDictadasLocales[(c.IdEC, c.Fecha)] = c;

            var asistenciasEspacioExistentes = await (
                from ae in _context.AsistenciasPorEspacio
                join cd in _context.ClasesDictadas on ae.IdClaseDictada equals cd.IdClaseDictada
                where estudiantesIds.Contains(ae.IdEstudiante) && fechas.Contains(cd.Fecha)
                select ae
            ).ToListAsync();

            if (asistenciasEspacioExistentes.Any())
                _context.AsistenciasPorEspacio.RemoveRange(asistenciasEspacioExistentes);

            var nuevasAsistenciasEspacio = new List<AsistenciaPorEspacio>();

            foreach (var asistencia in asistenciasGenerales)
            {
                var inscripcion = inscripciones.FirstOrDefault(i => i.IdEstudiante == asistencia.EstudianteId);

                if (inscripcion == null)
                {
                    asistencia.ValorTotalInasistencia = 0;
                    continue;
                }

                var horariosDelDia = horarios
                    .Where(h => h.IdCurso == inscripcion.IdCurso && h.DíaSemana == asistencia.Fecha.DayOfWeek)
                    .ToList();

                double minTotalesM = 0, minPerdidaIngresoM = 0, minPerdidaSalidaM = 0;
                double minTotalesT = 0, minPerdidaIngresoT = 0, minPerdidaSalidaT = 0;

                var gruposMaterias = horariosDelDia.GroupBy(h => h.IdEC).ToList();

                foreach (var grupoMateria in gruposMaterias)
                {
                    Guid idEC = grupoMateria.Key;
                    var key = (idEC, asistencia.Fecha);

                    if (!clasesDictadasLocales.ContainsKey(key))
                    {
                        var nuevaClase = new ClaseDictada
                        {
                            IdClaseDictada = Guid.NewGuid(),
                            IdEC = idEC,
                            Fecha = asistencia.Fecha,
                            Dictada = true,
                            Tema = "Generado Automáticamente"
                        };

                        _context.ClasesDictadas.Add(nuevaClase);
                        clasesDictadasLocales[key] = nuevaClase;
                    }

                    var claseDictada = clasesDictadasLocales[key];
                    if (!claseDictada.Dictada) continue;

                    double minTotalesMateria = 0;
                    double minAsistidosMateria = 0;
                    bool huboDefinicionMateria = false;

                    foreach (var horario in grupoMateria)
                    {
                        double duracionModulo = (horario.HorarioSalida - horario.HorarioEntrada).TotalMinutes;
                        if (duracionModulo <= 0) continue;

                        bool esManana = horario.HorarioEntrada.Hours < 13;
                        var tipoAsistencia = esManana ? asistencia.TipoManiana : asistencia.TipoTarde;
                        string codigo = tipoAsistencia?.Codigo?.ToUpper() ?? "-";

                        if (codigo == "-" || string.IsNullOrWhiteSpace(codigo))
                            continue;

                        huboDefinicionMateria = true;

                        minTotalesMateria += duracionModulo;

                        if (esManana) minTotalesM += duracionModulo;
                        else minTotalesT += duracionModulo;

                        if (codigo == "P" || codigo == "ANC" || codigo == "RE")
                        {
                            minAsistidosMateria += duracionModulo;
                            continue;
                        }

                        TimeSpan entradaAlumno = esManana
                            ? (asistencia.HoraEntradaManana ?? horario.HorarioEntrada)
                            : (asistencia.HoraEntradaTarde ?? horario.HorarioEntrada);

                        TimeSpan salidaAlumno = esManana
                            ? (asistencia.HoraSalidaManana ?? horario.HorarioSalida)
                            : (asistencia.HoraSalidaTarde ?? horario.HorarioSalida);

                        if (entradaAlumno > horario.HorarioEntrada)
                        {
                            TimeSpan finPerdida = entradaAlumno < horario.HorarioSalida ? entradaAlumno : horario.HorarioSalida;
                            double perdido = (finPerdida - horario.HorarioEntrada).TotalMinutes;
                            if (perdido > 0)
                            {
                                if (esManana) minPerdidaIngresoM += perdido;
                                else minPerdidaIngresoT += perdido;
                            }
                        }

                        if (salidaAlumno < horario.HorarioSalida)
                        {
                            TimeSpan inicioPerdida = salidaAlumno > horario.HorarioEntrada ? salidaAlumno : horario.HorarioEntrada;
                            double perdido = (horario.HorarioSalida - inicioPerdida).TotalMinutes;
                            if (perdido > 0)
                            {
                                if (esManana) minPerdidaSalidaM += perdido;
                                else minPerdidaSalidaT += perdido;
                            }
                        }

                        TimeSpan inicioEfectivo = horario.HorarioEntrada > entradaAlumno ? horario.HorarioEntrada : entradaAlumno;
                        TimeSpan finEfectivo = horario.HorarioSalida < salidaAlumno ? horario.HorarioSalida : salidaAlumno;
                        double asistidoEnModulo = (finEfectivo - inicioEfectivo).TotalMinutes;
                        if (asistidoEnModulo < 0) asistidoEnModulo = 0;

                        if (codigo == "A" || codigo == "LLTC" || codigo == "RAE")
                            asistidoEnModulo = 0;
                        else
                            minAsistidosMateria += asistidoEnModulo;
                    }

                    if (!huboDefinicionMateria)
                        continue;

                    bool presenteMateria = true;
                    string motivoMateria = "P";

                    if (minTotalesMateria > 0)
                    {
                        double porcentajeAusencia = 100.0 - ((minAsistidosMateria / minTotalesMateria) * 100.0);
                        if (porcentajeAusencia > 20.0)
                        {
                            presenteMateria = false;
                            motivoMateria = minAsistidosMateria == 0
                                ? "Ausente Total"
                                : $"Ausencia Parcial ({Math.Round(porcentajeAusencia)}%)";
                        }
                    }

                    nuevasAsistenciasEspacio.Add(new AsistenciaPorEspacio
                    {
                        IdAsistenciaEspacio = Guid.NewGuid(),
                        IdClaseDictada = claseDictada.IdClaseDictada,
                        IdEstudiante = asistencia.EstudianteId,
                        Presente = presenteMateria,
                        Motivo = motivoMateria
                    });
                }

                asistencia.CalcularAsistencia(
                    minTotalesM, minPerdidaIngresoM, minPerdidaSalidaM,
                    minTotalesT, minPerdidaIngresoT, minPerdidaSalidaT);
            }

            if (nuevasAsistenciasEspacio.Any())
                _context.AsistenciasPorEspacio.AddRange(nuevasAsistenciasEspacio);

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
            var asistencias = await _context.Asistencias
                .Include(a => a.TipoManiana)
                .Include(a => a.TipoTarde)
                .Where(a => a.Fecha == clase.Fecha)
                .ToListAsync();

            await ProcesarAsistenciaEspacios(asistencias);

            try
            {
                var ids = asistencias.Select(a => a.EstudianteId).Distinct().ToList();
                if (ids.Any())
                    await _umbrales.ProcesarUmbralesAsync(ids, clase.Fecha.Year);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error procesando umbrales tras regenerar asistencias.");
            }
        }

        // =========================================
        // METODOS ADICIONALES
        // =========================================

        public Task<PrevisualizarAsistenciaResponse> PrevisualizarAsync(PrevisualizarAsistenciaRequest request)
        {
            throw new NotImplementedException();
        }

        public Task ConfirmarAsync(ConfirmarAsistenciaRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<List<OpcionSeleccionDto>> ObtenerCursosAsync()
        {
            var anioActual = DateTime.UtcNow.Year;

            return _context.Cursos
                .AsNoTracking()
                .Where(c => c.Estado && c.AñoLectivo.Year == anioActual)
                .OrderBy(c => c.Codigo)
                .Select(c => new OpcionSeleccionDto
                {
                    Id = c.IdCurso.ToString(),
                    Label = c.Codigo
                })
                .ToListAsync();
        }

        public List<OpcionSeleccionDto> ObtenerTurnos()
        {
            return new List<OpcionSeleccionDto>
            {
                new()
                {
                    Id = "MANANA",
                    Label = "MANANA"
                },
                new()
                {
                    Id = "TARDE",
                    Label = "TARDE"
                }
            };
        }

        public Task<List<OpcionSeleccionDto>> ObtenerTiposAsistenciaAsync()
        {
            return _context.TiposAsistencia
                .AsNoTracking()
                .Where(t => t.Codigo != "RE" && t.Codigo != "RAE")
                .OrderBy(t => t.Codigo)
                .Select(t => new OpcionSeleccionDto
                {
                    Id = t.IdTipo.ToString(),
                    Label = t.Descripcion
                })
                .ToListAsync();
        }

      
    }
}