using Microsoft.EntityFrameworkCore;
using TesisGestorApi.Data;
using TesisGestorApi.DTOs.Retiro;
using TesisGestorApi.Entities;
using TesisGestorApi.Interfaces;

namespace TesisGestorApi.Services
{
    public class RetiroService : IRetiroService
    {
        private readonly ApplicationDbContext _context;
        private readonly IParteDiarioService _parteDiario;
        private readonly ICurrentUserService _currentUser;
        private readonly IAuditoriaAsistenciaECService _auditoriaEC;
        private static readonly TimeSpan LimiteTarde = new(13, 20, 0);

        public RetiroService(ApplicationDbContext context, IParteDiarioService parteDiario, ICurrentUserService currentUser, IAuditoriaAsistenciaECService auditoriaEC)
        {
            _context     = context;
            _parteDiario = parteDiario;
            _currentUser = currentUser;
            _auditoriaEC = auditoriaEC;
        }

        // ── Tutores del estudiante ─────────────────────────────────────────────

        public async Task<List<TutorEstudianteDto>> ObtenerTutoresEstudianteAsync(Guid estudianteId)
        {
            return await _context.Set<TutorEstudiante>()
                .AsNoTracking()
                .Include(te => te.Tutor)
                .Where(te => te.IdEstudiante == estudianteId)
                .Select(te => new TutorEstudianteDto
                {
                    IdTutor            = te.IdTutor,
                    Nombre             = te.Tutor.Nombre,
                    Apellido           = te.Tutor.Apellido,
                    Documento          = te.Tutor.Documento,
                    RelacionEstudiante = te.Tutor.RelacionEstudiante,
                    EsPrincipal        = te.EsPrincipal,
                    Telefono           = te.Tutor.Telefono == 0 ? null : te.Tutor.Telefono.ToString(),
                    Correo             = te.Tutor.Correo,
                })
                .OrderByDescending(t => t.EsPrincipal)
                .ThenBy(t => t.Apellido)
                .ToListAsync();
        }

        // ── Obtener retiros activos del día ───────────────────────────────────

        public async Task<List<RetiroActivoDto>> ObtenerRetirosActivosAsync(Guid estudianteId, DateOnly fecha)
        {
            var asistencia = await _context.Asistencias
                .AsNoTracking()
                .Include(a => a.TipoManiana)
                .Include(a => a.TipoTarde)
                .FirstOrDefaultAsync(a => a.EstudianteId == estudianteId && a.Fecha == fecha);

            if (asistencia == null) return [];

            var retiros = await _context.RetirosAnticipados
                .AsNoTracking()
                .Include(r => r.Tutor)
                .Where(r => r.IdAsistencia == asistencia.Id)
                .ToListAsync();

            return retiros.Select(r =>
            {
                string? codigoTipo = r.Turno == "MANANA"
                    ? asistencia.TipoManiana?.Codigo
                    : asistencia.TipoTarde?.Codigo;
                return MapToDto(r, codigoTipo);
            }).ToList();
        }

        // ── Registrar retiro ───────────────────────────────────────────────────

        public async Task<RetiroActivoDto> RegistrarRetiroAsync(RegistrarRetiroDto dto)
        {
            // Validaciones básicas
            if (dto.ConReingreso && !dto.HorarioLimiteReingreso.HasValue)
                throw new ArgumentException("Se debe indicar hora límite de reingreso cuando ConReingreso es true.");

            if (dto.IdTutor == null &&
                (string.IsNullOrWhiteSpace(dto.NombreResponsable) || string.IsNullOrWhiteSpace(dto.ApellidoResponsable)))
                throw new ArgumentException("Se debe indicar un tutor registrado o una persona responsable contingente.");

            var fecha    = dto.Fecha;
            bool esManana = dto.Turno.ToUpperInvariant() == "MANANA";

            // Inscripción del estudiante (para obtener el curso)
            var inscripcion = await _context.DetallesCursado
                .AsNoTracking()
                .FirstOrDefaultAsync(dc => dc.IdEstudiante == dto.EstudianteId && dc.Estado == true)
                ?? throw new InvalidOperationException("El estudiante no está inscripto en ningún curso activo.");

            // Cargar todos los horarios del día para el curso
            var todosHorarios = await _context.Horarios
                .AsNoTracking()
                .Where(h => h.IdCurso == inscripcion.IdCurso && h.DíaSemana == fecha.DayOfWeek)
                .ToListAsync();

            var idsHorario = todosHorarios.Select(h => h.IdHorario).ToList();

            // Clases dictadas del día
            var clasesDictadas = await _context.ClasesDictadas
                .AsNoTracking()
                .Where(c => idsHorario.Contains(c.IdHorario) && c.Fecha == fecha)
                .ToListAsync();
            var clasesDict = clasesDictadas.ToDictionary(c => c.IdHorario);

            // Horarios del turno del retiro
            var horariosTurno = todosHorarios
                .Where(h => esManana ? h.HorarioEntrada < LimiteTarde : h.HorarioEntrada >= LimiteTarde)
                .ToList();

            // Calcular porcentaje perdido y determinar tipo
            double porcPerdido = CalcularPorcentajePerdido(horariosTurno, clasesDict, dto.HorarioRetiro);
            string codigoTipo  = porcPerdido > 50 ? "RAE" : (porcPerdido > 10 ? "RA" : "RE");

            var tipoFinal = await _context.TiposAsistencia
                .FirstOrDefaultAsync(t => t.Codigo == codigoTipo)
                ?? throw new InvalidOperationException($"Tipo de asistencia '{codigoTipo}' no encontrado.");

            // Buscar o crear Asistencia
            var asistencia = await _context.Asistencias
                .Include(a => a.TipoManiana)
                .Include(a => a.TipoLlegadaManiana)
                .Include(a => a.TipoTarde)
                .FirstOrDefaultAsync(a => a.EstudianteId == dto.EstudianteId && a.Fecha == fecha);

            if (asistencia == null)
            {
                asistencia = new Asistencia
                {
                    Id           = Guid.NewGuid(),
                    EstudianteId = dto.EstudianteId,
                    Fecha        = fecha,
                };
                _context.Asistencias.Add(asistencia);
                await _context.SaveChangesAsync();
            }

            // Validar que no exista ya un retiro para este turno en esta asistencia
            if (await _context.RetirosAnticipados.AnyAsync(r => r.IdAsistencia == asistencia.Id && r.Turno == dto.Turno.ToUpperInvariant()))
                throw new InvalidOperationException("Ya existe un retiro registrado para este estudiante en este turno.");

            // Actualizar Asistencia
            if (esManana)
            {
                asistencia.TipoManianaId = tipoFinal.IdTipo;
                asistencia.TipoManiana   = tipoFinal;
                asistencia.HoraSalidaManana = dto.HorarioRetiro;
            }
            else
            {
                asistencia.TipoTardeId = tipoFinal.IdTipo;
                asistencia.TipoTarde   = tipoFinal;
                asistencia.HoraSalidaTarde = dto.HorarioRetiro;
            }

            // Recalcular ValorTotalInasistencia
            var horariosManana = todosHorarios.Where(h => h.HorarioEntrada < LimiteTarde).ToList();
            double minTotalesM, minPerdidosSalidaM;

            if (esManana)
            {
                (minTotalesM, minPerdidosSalidaM) = ComputarMinutosTurno(horariosManana, clasesDict, dto.HorarioRetiro);
            }
            else if (asistencia.HoraSalidaManana.HasValue)
            {
                // Había retiro matutino previo — preservar su contribución
                (minTotalesM, minPerdidosSalidaM) = ComputarMinutosTurno(horariosManana, clasesDict, asistencia.HoraSalidaManana.Value);
            }
            else
            {
                (minTotalesM, _) = ComputarMinutosTurno(horariosManana, clasesDict, null);
                minPerdidosSalidaM = 0;
            }

            // CalcularAsistencia para tarde no usa parámetros de minutos (solo switch en Codigo)
            asistencia.CalcularAsistencia(minTotalesM, 0, minPerdidosSalidaM, 0, 0, 0);

            // Crear RetiroAnticipado
            var retiro = new RetiroAnticipado
            {
                IdRetiro              = Guid.NewGuid(),
                Turno                 = dto.Turno.ToUpperInvariant(),
                HorarioRetiro         = DateTime.SpecifyKind(fecha.ToDateTime(TimeOnly.FromTimeSpan(dto.HorarioRetiro)), DateTimeKind.Utc),
                ConReingreso          = dto.ConReingreso,
                HorarioLimiteReingreso = dto.HorarioLimiteReingreso.HasValue
                    ? DateTime.SpecifyKind(fecha.ToDateTime(TimeOnly.FromTimeSpan(dto.HorarioLimiteReingreso.Value)), DateTimeKind.Utc)
                    : null,
                Motivo                = dto.Motivo,
                NombrePreceptor       = _currentUser.NombreCompleto,
                IdEstudiante          = dto.EstudianteId,
                IdTutor               = dto.IdTutor,
                NombreResponsable     = dto.NombreResponsable,
                ApellidoResponsable   = dto.ApellidoResponsable,
                DNIResponsable        = dto.DNIResponsable,
                RelacionResponsable   = dto.RelacionResponsable,
                TelefonoResponsable   = dto.TelefonoResponsable,
                CorreoResponsable     = dto.CorreoResponsable,
                IdAsistencia          = asistencia.Id,
            };

            _context.RetirosAnticipados.Add(retiro);

            // horaLlegada: si había LLT previo en mañana, lo consideramos en el 20%
            var codigoLlegadaM = esManana ? asistencia.TipoLlegadaManiana?.Codigo?.ToUpper() : null;
            TimeSpan? horaLlegadaEC = (codigoLlegadaM is "LLT" or "LLTE" or "LLTC")
                ? asistencia.HoraEntradaManana : null;

            // Marcar ausentes en AsistenciaPorEspacio usando regla del 20% por EC
            var auditBufferRetiro = new List<(Guid, Guid, bool?, bool, TimeSpan)>();
            await MarcarAusentesPorRetiroAsync(dto.EstudianteId, horariosTurno, clasesDict, dto.HorarioRetiro, horaLlegadaEC, auditBufferRetiro);

            await _context.SaveChangesAsync();

            if (auditBufferRetiro.Any())
            {
                try { await _auditoriaEC.RegistrarLoteAsync(auditBufferRetiro, TipoEventoAuditoriaEC.Retiro); }
                catch { /* no bloquear el retiro si falla la auditoría */ }
            }

            // Actividad del día
            var est = await _context.Estudiantes
                .AsNoTracking()
                .Where(e => e.IdEstudiante == dto.EstudianteId)
                .Select(e => new { e.Nombre, e.Apellido })
                .FirstOrDefaultAsync();
            if (est != null)
            {
                string horaLimiteStr = dto.ConReingreso && dto.HorarioLimiteReingreso.HasValue
                    ? $"\nHora de reingreso estimada: {TimeOnly.FromTimeSpan(dto.HorarioLimiteReingreso.Value):HH:mm}"
                    : "";

                string respNombre, respRelacion, respDni;
                if (dto.IdTutor.HasValue)
                {
                    var tutor = await _context.Tutores
                        .AsNoTracking()
                        .Where(t => t.IdTutor == dto.IdTutor.Value)
                        .Select(t => new { t.Nombre, t.Apellido, t.Documento, t.RelacionEstudiante })
                        .FirstOrDefaultAsync();
                    respNombre   = tutor != null ? $"{tutor.Apellido}, {tutor.Nombre}" : "—";
                    respRelacion = tutor?.RelacionEstudiante ?? "—";
                    respDni      = tutor?.Documento ?? "—";
                }
                else
                {
                    respNombre   = $"{dto.ApellidoResponsable ?? "—"}, {dto.NombreResponsable ?? "—"}";
                    respRelacion = dto.RelacionResponsable ?? "—";
                    respDni      = dto.DNIResponsable ?? "—";
                }

                string reingresoLabel = dto.ConReingreso && dto.HorarioLimiteReingreso.HasValue
                    ? $"con reingreso estimado {TimeOnly.FromTimeSpan(dto.HorarioLimiteReingreso.Value):HH:mm}"
                    : "sin reingreso";

                await _parteDiario.RegistrarEventoAsync(
                    inscripcion.IdCurso, fecha, "RETIRO",
                    $"Retiro registrado — {est.Apellido}, {est.Nombre} — {TimeOnly.FromTimeSpan(dto.HorarioRetiro):HH:mm} ({reingresoLabel})",
                    $"Responsable: {respNombre}\nRelación: {respRelacion}\nDNI: {respDni}");
            }

            return MapToDto(retiro, codigoTipo);
        }

        // ── Registrar reingreso ────────────────────────────────────────────────

        public async Task<RetiroActivoDto> RegistrarReingresoAsync(RegistrarReingresoDto dto)
        {
            var retiro = await _context.RetirosAnticipados
                .Include(r => r.Asistencia)
                    .ThenInclude(a => a.TipoManiana)
                .Include(r => r.Asistencia)
                    .ThenInclude(a => a.TipoLlegadaManiana)
                .Include(r => r.Asistencia)
                    .ThenInclude(a => a.TipoTarde)
                .FirstOrDefaultAsync(r => r.IdRetiro == dto.IdRetiro)
                ?? throw new InvalidOperationException("Retiro no encontrado.");

            if (retiro.HorarioReingreso.HasValue)
                throw new InvalidOperationException("Este retiro ya tiene un reingreso registrado.");

            var fecha     = retiro.Asistencia.Fecha;
            bool esManana = retiro.Turno == "MANANA";

            retiro.HorarioReingreso = DateTime.SpecifyKind(fecha.ToDateTime(TimeOnly.FromTimeSpan(dto.HorarioReingreso)), DateTimeKind.Utc);

            // Recalcular tipo con la ventana de ausencia real [HorarioRetiro, HorarioReingreso]
            var inscripcion = await _context.DetallesCursado
                .AsNoTracking()
                .FirstOrDefaultAsync(dc => dc.IdEstudiante == retiro.IdEstudiante && dc.Estado == true)
                ?? throw new InvalidOperationException("El estudiante no está inscripto en ningún curso activo.");

            var todosHorarios = await _context.Horarios
                .AsNoTracking()
                .Where(h => h.IdCurso == inscripcion.IdCurso && h.DíaSemana == fecha.DayOfWeek)
                .ToListAsync();

            var idsHorario    = todosHorarios.Select(h => h.IdHorario).ToList();
            var clasesDictadas = await _context.ClasesDictadas
                .AsNoTracking()
                .Where(c => idsHorario.Contains(c.IdHorario) && c.Fecha == fecha)
                .ToListAsync();
            var clasesDict = clasesDictadas.ToDictionary(c => c.IdHorario);

            var horariosTurno = todosHorarios
                .Where(h => esManana ? h.HorarioEntrada < LimiteTarde : h.HorarioEntrada >= LimiteTarde)
                .ToList();

            TimeSpan horaRetiro    = retiro.HorarioRetiro.TimeOfDay;
            TimeSpan horaReingreso = dto.HorarioReingreso;

            // Minutos perdidos = intersección de clases con la ventana [retiro, reingreso]
            var (minTotales, minPerdidosVentana) = ComputarMinutosVentana(horariosTurno, clasesDict, horaRetiro, horaReingreso);

            double porcPerdido = minTotales == 0 ? 0 : (minPerdidosVentana / minTotales) * 100;
            string codigoTipo  = porcPerdido > 50 ? "RAE" : (porcPerdido > 10 ? "RA" : "RE");

            var tipoFinal = await _context.TiposAsistencia
                .FirstOrDefaultAsync(t => t.Codigo == codigoTipo)
                ?? throw new InvalidOperationException($"Tipo de asistencia '{codigoTipo}' no encontrado.");

            // Actualizar tipo en Asistencia
            var asistencia = retiro.Asistencia;
            if (esManana)
            {
                asistencia.TipoManianaId = tipoFinal.IdTipo;
                asistencia.TipoManiana   = tipoFinal;
            }
            else
            {
                asistencia.TipoTardeId = tipoFinal.IdTipo;
                asistencia.TipoTarde   = tipoFinal;
            }

            // Recalcular ValorTotalInasistencia
            var horariosManana = todosHorarios.Where(h => h.HorarioEntrada < LimiteTarde).ToList();
            double minTotalesM, minPerdidosSalidaM;

            if (esManana)
            {
                (minTotalesM, minPerdidosSalidaM) = ComputarMinutosTurno(horariosManana, clasesDict, dto.HorarioReingreso > horaRetiro ? horaRetiro : dto.HorarioReingreso);
                // Para reingreso, los minutos perdidos son los de la ventana real
                minPerdidosSalidaM = minPerdidosVentana;
            }
            else if (asistencia.HoraSalidaManana.HasValue)
            {
                (minTotalesM, minPerdidosSalidaM) = ComputarMinutosTurno(horariosManana, clasesDict, asistencia.HoraSalidaManana.Value);
            }
            else
            {
                (minTotalesM, _) = ComputarMinutosTurno(horariosManana, clasesDict, null);
                minPerdidosSalidaM = 0;
            }

            asistencia.CalcularAsistencia(minTotalesM, 0, minPerdidosSalidaM, 0, 0, 0);

            // horaLlegada para el cálculo del 20%
            var codLlegadaReing = esManana ? asistencia.TipoLlegadaManiana?.Codigo?.ToUpper() : null;
            TimeSpan? horaLlegadaReing = (codLlegadaReing is "LLT" or "LLTE" or "LLTC")
                ? asistencia.HoraEntradaManana : null;

            // Actualizar AsistenciaPorEspacio con la ventana real [retiro, reingreso] usando 20% por EC
            var auditBufferReingreso = new List<(Guid, Guid, bool?, bool, TimeSpan)>();
            await ActualizarAusentesPorReingresoAsync(retiro.IdEstudiante, horariosTurno, clasesDict, horaRetiro, horaReingreso, horaLlegadaReing, auditBufferReingreso);

            await _context.SaveChangesAsync();

            if (auditBufferReingreso.Any())
            {
                try { await _auditoriaEC.RegistrarLoteAsync(auditBufferReingreso, TipoEventoAuditoriaEC.Retiro); }
                catch { /* no bloquear el reingreso si falla la auditoría */ }
            }

            // Actividad del día
            var estReingreso = await _context.Estudiantes
                .AsNoTracking()
                .Where(e => e.IdEstudiante == retiro.IdEstudiante)
                .Select(e => new { e.Nombre, e.Apellido })
                .FirstOrDefaultAsync();
            if (estReingreso != null)
            {
                string horaRetiroStr    = retiro.HorarioRetiro.ToString("HH:mm");
                string horaReingresoStr = TimeOnly.FromTimeSpan(dto.HorarioReingreso).ToString("HH:mm");
                int    minAusencia      = Math.Max(0, (int)(dto.HorarioReingreso - retiro.HorarioRetiro.TimeOfDay).TotalMinutes);

                await _parteDiario.RegistrarEventoAsync(
                    inscripcion.IdCurso, fecha, "RETIRO",
                    $"Reingreso registrado - {estReingreso.Apellido}, {estReingreso.Nombre}",
                    $"Hora de retiro: {horaRetiroStr}\nHora de reingreso: {horaReingresoStr}\nTiempo de ausencia: {minAusencia} min\nTipo de retiro: {codigoTipo}");
            }

            return MapToDto(retiro, codigoTipo);
        }

        // ── Actualizar retiro ──────────────────────────────────────────────────

        public async Task<RetiroActivoDto> ActualizarRetiroAsync(Guid idRetiro, ActualizarRetiroDto dto)
        {
            var retiro = await _context.RetirosAnticipados
                .Include(r => r.Asistencia)
                    .ThenInclude(a => a.TipoManiana)
                .Include(r => r.Asistencia)
                    .ThenInclude(a => a.TipoLlegadaManiana)
                .Include(r => r.Asistencia)
                    .ThenInclude(a => a.TipoTarde)
                .Include(r => r.Estudiante)
                .FirstOrDefaultAsync(r => r.IdRetiro == idRetiro)
                ?? throw new InvalidOperationException("Retiro no encontrado.");

            var fecha     = retiro.Asistencia.Fecha;
            bool esManana = retiro.Turno == "MANANA";

            // ── Validaciones de horario ────────────────────────────────────────
            bool conReingresoFinal = dto.ConReingreso ?? retiro.ConReingreso;

            if (conReingresoFinal && dto.HorarioLimiteReingreso.HasValue)
            {
                if (dto.HorarioRetiro >= dto.HorarioLimiteReingreso.Value)
                    throw new ArgumentException("El horario de retiro debe ser anterior al límite de reingreso.");
                if (esManana && dto.HorarioLimiteReingreso.Value > LimiteTarde)
                    throw new ArgumentException("Para el turno mañana, el límite de reingreso no puede superar las 13:20.");
            }

            TimeSpan? nuevaHoraReingreso = dto.HorarioReingreso
                ?? (retiro.HorarioReingreso.HasValue ? retiro.HorarioReingreso.Value.TimeOfDay : (TimeSpan?)null);

            if (dto.HorarioReingreso.HasValue && dto.HorarioRetiro >= dto.HorarioReingreso.Value)
                throw new ArgumentException("El horario de retiro debe ser anterior al horario de reingreso.");

            // ── Datos del curso ────────────────────────────────────────────────
            var inscripcion = await _context.DetallesCursado
                .AsNoTracking()
                .FirstOrDefaultAsync(dc => dc.IdEstudiante == retiro.IdEstudiante && dc.Estado == true)
                ?? throw new InvalidOperationException("El estudiante no está inscripto en ningún curso activo.");

            var todosHorarios = await _context.Horarios
                .AsNoTracking()
                .Where(h => h.IdCurso == inscripcion.IdCurso && h.DíaSemana == fecha.DayOfWeek)
                .ToListAsync();

            var idsHorario = todosHorarios.Select(h => h.IdHorario).ToList();
            var clasesDictadas = await _context.ClasesDictadas
                .AsNoTracking()
                .Where(c => idsHorario.Contains(c.IdHorario) && c.Fecha == fecha)
                .ToListAsync();
            var clasesDict = clasesDictadas.ToDictionary(c => c.IdHorario);

            var horariosTurno = todosHorarios
                .Where(h => esManana ? h.HorarioEntrada < LimiteTarde : h.HorarioEntrada >= LimiteTarde)
                .ToList();

            // ── Calcular tipo según ventana real ───────────────────────────────
            double porcPerdido;
            if (nuevaHoraReingreso.HasValue)
            {
                var (mt, mp) = ComputarMinutosVentana(horariosTurno, clasesDict, dto.HorarioRetiro, nuevaHoraReingreso.Value);
                porcPerdido = mt == 0 ? 0 : (mp / mt) * 100;
            }
            else
            {
                porcPerdido = CalcularPorcentajePerdido(horariosTurno, clasesDict, dto.HorarioRetiro);
            }
            string codigoTipo = porcPerdido > 50 ? "RAE" : (porcPerdido > 10 ? "RA" : "RE");

            var tipoFinal = await _context.TiposAsistencia
                .FirstOrDefaultAsync(t => t.Codigo == codigoTipo)
                ?? throw new InvalidOperationException($"Tipo '{codigoTipo}' no encontrado.");

            // ── Capturar valores previos para el log ──────────────────────────────
            string prevHora      = retiro.HorarioRetiro.ToString("HH:mm");
            string prevPreceptor = retiro.NombrePreceptor ?? "";
            string prevMotivo    = retiro.Motivo ?? "";
            bool   prevConReing  = retiro.ConReingreso;
            string prevLimite    = retiro.HorarioLimiteReingreso?.ToString("HH:mm") ?? "";
            string prevReingreso = retiro.HorarioReingreso?.ToString("HH:mm") ?? "";

            // ── Actualizar campos del retiro ───────────────────────────────────
            retiro.HorarioRetiro   = DateTime.SpecifyKind(fecha.ToDateTime(TimeOnly.FromTimeSpan(dto.HorarioRetiro)), DateTimeKind.Utc);
            retiro.NombrePreceptor = _currentUser.NombreCompleto;
            if (dto.Motivo != null) retiro.Motivo = dto.Motivo;

            retiro.ConReingreso = conReingresoFinal;
            if (!conReingresoFinal)
                retiro.HorarioLimiteReingreso = null;
            else if (dto.HorarioLimiteReingreso.HasValue)
                retiro.HorarioLimiteReingreso = DateTime.SpecifyKind(fecha.ToDateTime(TimeOnly.FromTimeSpan(dto.HorarioLimiteReingreso.Value)), DateTimeKind.Utc);

            if (dto.HorarioReingreso.HasValue)
                retiro.HorarioReingreso = DateTime.SpecifyKind(fecha.ToDateTime(TimeOnly.FromTimeSpan(dto.HorarioReingreso.Value)), DateTimeKind.Utc);

            // Campos de responsable contingente
            if (!retiro.IdTutor.HasValue)
            {
                if (dto.NombreResponsable    != null) retiro.NombreResponsable    = dto.NombreResponsable;
                if (dto.ApellidoResponsable  != null) retiro.ApellidoResponsable  = dto.ApellidoResponsable;
                if (dto.DNIResponsable       != null) retiro.DNIResponsable       = dto.DNIResponsable;
                if (dto.RelacionResponsable  != null) retiro.RelacionResponsable  = dto.RelacionResponsable;
                if (dto.TelefonoResponsable  != null) retiro.TelefonoResponsable  = dto.TelefonoResponsable;
                if (dto.CorreoResponsable    != null) retiro.CorreoResponsable    = dto.CorreoResponsable;
            }

            // ── Actualizar Asistencia ──────────────────────────────────────────
            var asistencia = retiro.Asistencia;
            if (esManana)
            {
                asistencia.TipoManianaId    = tipoFinal.IdTipo;
                asistencia.TipoManiana      = tipoFinal;
                asistencia.HoraSalidaManana = dto.HorarioRetiro;
            }
            else
            {
                asistencia.TipoTardeId     = tipoFinal.IdTipo;
                asistencia.TipoTarde       = tipoFinal;
                asistencia.HoraSalidaTarde = dto.HorarioRetiro;
            }

            var horariosManana = todosHorarios.Where(h => h.HorarioEntrada < LimiteTarde).ToList();
            double minTotalesM, minPerdidosSalidaM;
            if (esManana)
            {
                if (nuevaHoraReingreso.HasValue)
                {
                    var (_, mp) = ComputarMinutosVentana(horariosTurno, clasesDict, dto.HorarioRetiro, nuevaHoraReingreso.Value);
                    (minTotalesM, _) = ComputarMinutosTurno(horariosManana, clasesDict, null);
                    minPerdidosSalidaM = mp;
                }
                else
                    (minTotalesM, minPerdidosSalidaM) = ComputarMinutosTurno(horariosManana, clasesDict, dto.HorarioRetiro);
            }
            else if (asistencia.HoraSalidaManana.HasValue)
                (minTotalesM, minPerdidosSalidaM) = ComputarMinutosTurno(horariosManana, clasesDict, asistencia.HoraSalidaManana.Value);
            else
            {
                (minTotalesM, _) = ComputarMinutosTurno(horariosManana, clasesDict, null);
                minPerdidosSalidaM = 0;
            }

            asistencia.CalcularAsistencia(minTotalesM, 0, minPerdidosSalidaM, 0, 0, 0);

            // horaLlegada para el 20%
            var codLlegadaAct = esManana ? asistencia.TipoLlegadaManiana?.Codigo?.ToUpper() : null;
            TimeSpan? horaLlegadaAct = (codLlegadaAct is "LLT" or "LLTE" or "LLTC")
                ? asistencia.HoraEntradaManana : null;

            // ── Re-marcar EC con regla del 20% ────────────────────────────────
            // No se hace Reset previo: MarcarAusentesPorRetiroAsync y
            // ActualizarAusentesPorReingresoAsync actualizan en-lugar los APEs existentes,
            // evitando el estado Deleted del change tracker que causaba que los EC
            // quedaran null al reducir la ventana de reingreso o cambiar Sin→Con Reingreso.
            var auditBufferActualizar = new List<(Guid, Guid, bool?, bool, TimeSpan)>();
            if (nuevaHoraReingreso.HasValue)
                await ActualizarAusentesPorReingresoAsync(retiro.IdEstudiante, horariosTurno, clasesDict, dto.HorarioRetiro, nuevaHoraReingreso.Value, horaLlegadaAct, auditBufferActualizar);
            else
                await MarcarAusentesPorRetiroAsync(retiro.IdEstudiante, horariosTurno, clasesDict, dto.HorarioRetiro, horaLlegadaAct, auditBufferActualizar);

            await _context.SaveChangesAsync();

            if (auditBufferActualizar.Any())
            {
                try { await _auditoriaEC.RegistrarLoteAsync(auditBufferActualizar, TipoEventoAuditoriaEC.Retiro); }
                catch { /* no bloquear si falla la auditoría */ }
            }

            // Actividad del día
            string newHora      = TimeOnly.FromTimeSpan(dto.HorarioRetiro).ToString("HH:mm");
            string newPreceptor = _currentUser.NombreCompleto;
            string newMotivo    = dto.Motivo ?? prevMotivo;
            string newLimite    = dto.HorarioLimiteReingreso.HasValue
                ? TimeOnly.FromTimeSpan(dto.HorarioLimiteReingreso.Value).ToString("HH:mm")
                : (conReingresoFinal ? prevLimite : "");
            string newReingreso = dto.HorarioReingreso.HasValue
                ? TimeOnly.FromTimeSpan(dto.HorarioReingreso.Value).ToString("HH:mm")
                : prevReingreso;

            var cambios = new System.Text.StringBuilder();
            if (prevHora      != newHora)      cambios.AppendLine($"Hora de retiro: {prevHora} → {newHora}");
            if (prevPreceptor != newPreceptor)  cambios.AppendLine($"Preceptor: {prevPreceptor} → {newPreceptor}");
            if (prevMotivo    != newMotivo)     cambios.AppendLine($"Motivo: {prevMotivo} → {newMotivo}");
            if (prevConReing  != conReingresoFinal) cambios.AppendLine($"Con reingreso: {(prevConReing ? "Sí" : "No")} → {(conReingresoFinal ? "Sí" : "No")}");
            if (prevLimite    != newLimite)     cambios.AppendLine($"Hora límite reingreso: {(prevLimite == "" ? "—" : prevLimite)} → {(newLimite == "" ? "—" : newLimite)}");
            if (prevReingreso != newReingreso)  cambios.AppendLine($"Hora de reingreso: {(prevReingreso == "" ? "—" : prevReingreso)} → {(newReingreso == "" ? "—" : newReingreso)}");

            string detalleLog = cambios.Length > 0
                ? cambios.ToString().TrimEnd()
                : "Sin cambios detectados";

            await _parteDiario.RegistrarEventoAsync(
                inscripcion.IdCurso, fecha, "RETIRO",
                $"Retiro actualizado - {retiro.Estudiante.Apellido}, {retiro.Estudiante.Nombre}",
                detalleLog);

            return MapToDto(retiro, codigoTipo);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static double CalcularPorcentajePerdido(
            List<Horario> horarios,
            Dictionary<Guid, ClaseDictada> clasesDict,
            TimeSpan horaRetiro)
        {
            var (total, perdidos) = ComputarMinutosTurno(horarios, clasesDict, horaRetiro);
            return total == 0 ? 0 : (perdidos / total) * 100;
        }

        /// <summary>
        /// Computa minutos totales del turno y minutos perdidos a partir de horaRetiro.
        /// Si horaRetiro es null, devuelve (minTotales, 0).
        /// </summary>
        private static (double minTotales, double minPerdidos) ComputarMinutosTurno(
            List<Horario> horarios,
            Dictionary<Guid, ClaseDictada> clasesDict,
            TimeSpan? horaRetiro)
        {
            double minTotales = 0;
            double minPerdidos = 0;

            foreach (var h in horarios)
            {
                clasesDict.TryGetValue(h.IdHorario, out var cd);
                if (cd != null && !cd.Dictada) continue;

                TimeSpan entrada  = cd?.HorarioEntradaEfectiva ?? h.HorarioEntrada;
                TimeSpan salida   = cd?.HorarioSalidaEfectiva  ?? h.HorarioSalida;
                double   duracion = (salida - entrada).TotalMinutes;
                if (duracion <= 0) continue;

                minTotales += duracion;

                if (horaRetiro.HasValue && horaRetiro.Value < salida)
                {
                    TimeSpan inicio   = horaRetiro.Value > entrada ? horaRetiro.Value : entrada;
                    double   perdido  = (salida - inicio).TotalMinutes;
                    if (perdido > 0) minPerdidos += perdido;
                }
            }

            return (minTotales, minPerdidos);
        }

        /// <summary>
        /// Computa minutos totales del turno y minutos perdidos dentro de la ventana [inicio, fin].
        /// Usado para recalcular al registrar el reingreso.
        /// </summary>
        private static (double minTotales, double minPerdidos) ComputarMinutosVentana(
            List<Horario> horarios,
            Dictionary<Guid, ClaseDictada> clasesDict,
            TimeSpan inicio,
            TimeSpan fin)
        {
            double minTotales = 0;
            double minPerdidos = 0;

            foreach (var h in horarios)
            {
                clasesDict.TryGetValue(h.IdHorario, out var cd);
                if (cd != null && !cd.Dictada) continue;

                TimeSpan claseEntrada = cd?.HorarioEntradaEfectiva ?? h.HorarioEntrada;
                TimeSpan claseSalida  = cd?.HorarioSalidaEfectiva  ?? h.HorarioSalida;
                double   duracion     = (claseSalida - claseEntrada).TotalMinutes;
                if (duracion <= 0) continue;

                minTotales += duracion;

                // Intersección de [inicio, fin] con [claseEntrada, claseSalida]
                TimeSpan ventanaStart = inicio > claseEntrada ? inicio : claseEntrada;
                TimeSpan ventanaEnd   = fin    < claseSalida  ? fin    : claseSalida;

                if (ventanaEnd > ventanaStart)
                    minPerdidos += (ventanaEnd - ventanaStart).TotalMinutes;
            }

            return (minTotales, minPerdidos);
        }

        // ── EC Attendance helpers ──────────────────────────────────────────────

        /// <summary>
        /// Regla del 20% por EC: marca ausente en AsistenciaPorEspacio para cada EC donde
        /// el alumno asistió menos del 80% de los minutos dictados del turno.
        /// La ventana de asistencia es [max(horaLlegada, entradaSlot), min(horaRetiro, salidaSlot)].
        /// horaLlegada: hora de llegada tarde (LLT) si aplica, null si llegó a tiempo.
        /// </summary>
        private async Task MarcarAusentesPorRetiroAsync(
            Guid estudianteId,
            List<Horario> horariosTurno,
            Dictionary<Guid, ClaseDictada> clasesDict,
            TimeSpan horaRetiro,
            TimeSpan? horaLlegada = null,
            List<(Guid EstudianteId, Guid IdClaseDictada, bool? EstadoAnterior, bool EstadoNuevo, TimeSpan HorarioEvento)>? auditBuffer = null)
        {
            foreach (var grupo in horariosTurno.GroupBy(h => h.IdEC))
            {
                var slots = grupo.OrderBy(h => h.HorarioEntrada).ToList();
                double minTotales   = 0;
                double minAsistidos = 0;

                foreach (var h in slots)
                {
                    if (!clasesDict.TryGetValue(h.IdHorario, out var cd) || !cd.Dictada) continue;
                    TimeSpan entrada  = cd.HorarioEntradaEfectiva ?? h.HorarioEntrada;
                    TimeSpan salida   = cd.HorarioSalidaEfectiva  ?? h.HorarioSalida;
                    double   duracion = (salida - entrada).TotalMinutes;
                    if (duracion <= 0) continue;

                    minTotales += duracion;

                    TimeSpan inicioEf = horaLlegada.HasValue && horaLlegada.Value > entrada
                        ? horaLlegada.Value : entrada;
                    TimeSpan finEf    = horaRetiro < salida ? horaRetiro : salida;
                    double asistido   = (finEf - inicioEf).TotalMinutes;
                    if (asistido > 0) minAsistidos += asistido;
                }

                if (minTotales == 0) continue;
                bool debeAusente = (100.0 - (minAsistidos / minTotales) * 100.0) > 20.0;

                foreach (var h in slots)
                {
                    if (!clasesDict.TryGetValue(h.IdHorario, out var cd) || !cd.Dictada) continue;
                    var ape = await _context.AsistenciasPorEspacio
                        .FirstOrDefaultAsync(a => a.IdEstudiante == estudianteId && a.IdClaseDictada == cd.IdClaseDictada);

                    bool? estadoAnterior = ape?.Presente;
                    bool estadoNuevo;

                    if (debeAusente)
                    {
                        estadoNuevo = false;
                        if (ape == null)
                            _context.AsistenciasPorEspacio.Add(new AsistenciaPorEspacio
                            {
                                IdAsistenciaEspacio = Guid.NewGuid(),
                                Fecha          = cd.Fecha,
                                IdEstudiante   = estudianteId,
                                IdClaseDictada = cd.IdClaseDictada,
                                Presente       = false,
                                Motivo         = "Retiro anticipado",
                            });
                        else
                        {
                            ape.Presente = false;
                            if (ape.Motivo != "Llegada tarde")
                                ape.Motivo = "Retiro anticipado";
                        }
                    }
                    else if (ape != null && ape.Motivo == "Retiro anticipado")
                    {
                        // El retiro ya no supera el 20%: restaurar presencia
                        estadoNuevo = true;
                        ape.Presente = true;
                        ape.Motivo   = string.Empty;
                    }
                    else continue;

                    if (auditBuffer != null && estadoAnterior != estadoNuevo)
                        auditBuffer.Add((estudianteId, cd.IdClaseDictada, estadoAnterior, estadoNuevo, DateTime.Now.TimeOfDay));
                }
            }
        }

        /// <summary>
        /// Regla del 20% por EC tras registrar reingreso.
        /// Ventana de ausencia: [horaRetiro, horaReingreso]; el alumno estuvo presente antes y después.
        /// horaLlegada: si había llegada tarde (LLT) previa.
        /// </summary>
        private async Task ActualizarAusentesPorReingresoAsync(
            Guid estudianteId,
            List<Horario> horariosTurno,
            Dictionary<Guid, ClaseDictada> clasesDict,
            TimeSpan horaRetiro,
            TimeSpan horaReingreso,
            TimeSpan? horaLlegada = null,
            List<(Guid EstudianteId, Guid IdClaseDictada, bool? EstadoAnterior, bool EstadoNuevo, TimeSpan HorarioEvento)>? auditBuffer = null)
        {
            foreach (var grupo in horariosTurno.GroupBy(h => h.IdEC))
            {
                var slots = grupo.OrderBy(h => h.HorarioEntrada).ToList();
                double minTotales   = 0;
                double minAsistidos = 0;

                foreach (var h in slots)
                {
                    if (!clasesDict.TryGetValue(h.IdHorario, out var cd) || !cd.Dictada) continue;
                    TimeSpan entrada  = cd.HorarioEntradaEfectiva ?? h.HorarioEntrada;
                    TimeSpan salida   = cd.HorarioSalidaEfectiva  ?? h.HorarioSalida;
                    double   duracion = (salida - entrada).TotalMinutes;
                    if (duracion <= 0) continue;

                    minTotales += duracion;

                    TimeSpan llegadaEf = horaLlegada.HasValue && horaLlegada.Value > entrada
                        ? horaLlegada.Value : entrada;

                    // Antes del retiro
                    TimeSpan finAntes = horaRetiro < salida ? horaRetiro : salida;
                    double antes = (finAntes - llegadaEf).TotalMinutes;
                    if (antes > 0) minAsistidos += antes;

                    // Después del reingreso
                    TimeSpan inicioPost = horaReingreso > entrada ? horaReingreso : entrada;
                    double post = (salida - inicioPost).TotalMinutes;
                    if (post > 0) minAsistidos += post;
                }

                if (minTotales == 0) continue;
                bool debeAusente = (100.0 - (minAsistidos / minTotales) * 100.0) > 20.0;

                foreach (var h in slots)
                {
                    if (!clasesDict.TryGetValue(h.IdHorario, out var cd) || !cd.Dictada) continue;
                    var ape = await _context.AsistenciasPorEspacio
                        .FirstOrDefaultAsync(a => a.IdEstudiante == estudianteId && a.IdClaseDictada == cd.IdClaseDictada);

                    bool? estadoAnterior = ape?.Presente;
                    bool estadoNuevo;

                    if (debeAusente)
                    {
                        estadoNuevo = false;
                        if (ape == null)
                            _context.AsistenciasPorEspacio.Add(new AsistenciaPorEspacio
                            {
                                IdAsistenciaEspacio = Guid.NewGuid(),
                                Fecha          = cd.Fecha,
                                IdEstudiante   = estudianteId,
                                IdClaseDictada = cd.IdClaseDictada,
                                Presente       = false,
                                Motivo         = "Retiro anticipado",
                            });
                        else
                        {
                            ape.Presente = false;
                            if (ape.Motivo != "Llegada tarde")
                                ape.Motivo = "Retiro anticipado";
                        }
                    }
                    else if (ape != null && !ape.Presente)
                    {
                        // El reingreso acortó la ausencia suficientemente: restaurar presencia
                        estadoNuevo = true;
                        ape.Presente = true;
                        ape.Motivo   = string.Empty;
                    }
                    else continue;

                    if (auditBuffer != null && estadoAnterior != estadoNuevo)
                        auditBuffer.Add((estudianteId, cd.IdClaseDictada, estadoAnterior, estadoNuevo, DateTime.Now.TimeOfDay));
                }
            }
        }

        /// <summary>
        /// Elimina los registros de AsistenciaPorEspacio del turno causados por el retiro,
        /// dejando la asistencia por espacio en estado null (sin registro).
        /// </summary>
        private async Task ResetearAusentesECTurnoAsync(
            Guid estudianteId,
            List<Horario> horariosTurno,
            Dictionary<Guid, ClaseDictada> clasesDict)
        {
            var idsCd = horariosTurno
                .Where(h => clasesDict.ContainsKey(h.IdHorario))
                .Select(h => clasesDict[h.IdHorario].IdClaseDictada)
                .ToList();

            var apes = await _context.AsistenciasPorEspacio
                .Where(a => a.IdEstudiante == estudianteId
                         && idsCd.Contains(a.IdClaseDictada)
                         && a.Motivo == "Retiro anticipado")
                .ToListAsync();

            _context.AsistenciasPorEspacio.RemoveRange(apes);
        }

        // ── Cancelar retiro ───────────────────────────────────────────────────

        public async Task CancelarRetiroAsync(Guid idRetiro)
        {
            var retiro = await _context.RetirosAnticipados
                .Include(r => r.Asistencia)
                    .ThenInclude(a => a.TipoManiana)
                .Include(r => r.Asistencia)
                    .ThenInclude(a => a.TipoLlegadaManiana)
                .Include(r => r.Asistencia)
                    .ThenInclude(a => a.TipoTarde)
                .Include(r => r.Estudiante)
                .FirstOrDefaultAsync(r => r.IdRetiro == idRetiro)
                ?? throw new InvalidOperationException("Retiro no encontrado.");

            var fecha    = retiro.Asistencia.Fecha;
            bool esManana = retiro.Turno == "MANANA";

            // Obtener horarios para restaurar EC attendance
            var inscripcion = await _context.DetallesCursado
                .AsNoTracking()
                .FirstOrDefaultAsync(dc => dc.IdEstudiante == retiro.IdEstudiante && dc.Estado == true);

            if (inscripcion != null)
            {
                var todosHorarios = await _context.Horarios
                    .AsNoTracking()
                    .Where(h => h.IdCurso == inscripcion.IdCurso && h.DíaSemana == fecha.DayOfWeek)
                    .ToListAsync();

                var idsHorario = todosHorarios.Select(h => h.IdHorario).ToList();
                var clasesDictadas = await _context.ClasesDictadas
                    .AsNoTracking()
                    .Where(c => idsHorario.Contains(c.IdHorario) && c.Fecha == fecha)
                    .ToListAsync();
                var clasesDict = clasesDictadas.ToDictionary(c => c.IdHorario);

                var horariosTurno = todosHorarios
                    .Where(h => esManana ? h.HorarioEntrada < LimiteTarde : h.HorarioEntrada >= LimiteTarde)
                    .ToList();

                // Restaurar asistencias por espacio
                await ResetearAusentesECTurnoAsync(retiro.IdEstudiante, horariosTurno, clasesDict);

                // Recalcular ValorTotalInasistencia sin el retiro
                var horariosManana = todosHorarios.Where(h => h.HorarioEntrada < LimiteTarde).ToList();
                var asistencia     = retiro.Asistencia;

                if (esManana)
                {
                    // Restaurar el tipo original de llegada mañana (P, LLT, LLTE, LLTC)
                    asistencia.TipoManianaId    = asistencia.TipoLlegadaManianaId;
                    asistencia.TipoManiana      = asistencia.TipoLlegadaManiana;
                    asistencia.HoraSalidaManana = null;
                }
                else
                {
                    // Sin TipoLlegadaTardeId por ahora — queda null hasta próxima migración
                    asistencia.TipoTardeId     = null;
                    asistencia.TipoTarde       = null;
                    asistencia.HoraSalidaTarde = null;
                }

                // Recalcular con valores limpios
                double minTotalesM, minPerdidosSalidaM;
                (minTotalesM, _) = ComputarMinutosTurno(horariosManana, clasesDict, null);
                minPerdidosSalidaM = 0;
                asistencia.CalcularAsistencia(minTotalesM, 0, minPerdidosSalidaM, 0, 0, 0);
            }
            else
            {
                // Sin inscripción activa — solo limpiar el tipo
                var asistencia = retiro.Asistencia;
                if (esManana) { asistencia.TipoManianaId = asistencia.TipoLlegadaManianaId; asistencia.TipoManiana = asistencia.TipoLlegadaManiana; asistencia.HoraSalidaManana = null; }
                else          { asistencia.TipoTardeId   = null; asistencia.TipoTarde   = null; asistencia.HoraSalidaTarde  = null; }
            }

            _context.RetirosAnticipados.Remove(retiro);
            await _context.SaveChangesAsync();

            if (inscripcion != null)
            {
                string respCancelDetalle = retiro.IdTutor.HasValue
                    ? "Responsable: tutor registrado"
                    : $"Responsable: {retiro.ApellidoResponsable ?? "—"}, {retiro.NombreResponsable ?? "—"}";
                await _parteDiario.RegistrarEventoAsync(
                    inscripcion.IdCurso, fecha, "RETIRO",
                    $"Retiro cancelado — {retiro.Estudiante.Apellido}, {retiro.Estudiante.Nombre} — {retiro.HorarioRetiro:HH\\:mm}",
                    $"Preceptor: {retiro.NombrePreceptor ?? "—"}\n{respCancelDetalle}");
            }
        }

        private static RetiroActivoDto MapToDto(RetiroAnticipado retiro, string? codigoTipo)
        {
            bool esTutor = retiro.IdTutor.HasValue;
            var  tutor   = retiro.Tutor;  // non-null si se incluyó con .Include(r => r.Tutor)

            return new RetiroActivoDto
            {
                IdRetiro               = retiro.IdRetiro,
                Turno                  = retiro.Turno,
                HorarioRetiro          = retiro.HorarioRetiro.ToString(@"HH\:mm"),
                ConReingreso           = retiro.ConReingreso,
                HorarioLimiteReingreso = retiro.HorarioLimiteReingreso?.ToString(@"HH\:mm"),
                HorarioReingreso       = retiro.HorarioReingreso?.ToString(@"HH\:mm"),
                EtiquetaEstado         = ComputarEtiqueta(retiro),
                TipoRetiro             = codigoTipo,
                NombrePreceptor        = retiro.NombrePreceptor,
                Motivo                 = retiro.Motivo,
                IdTutor                = retiro.IdTutor,
                NombreResponsable      = esTutor ? tutor?.Nombre            : retiro.NombreResponsable,
                ApellidoResponsable    = esTutor ? tutor?.Apellido          : retiro.ApellidoResponsable,
                DniResponsable         = esTutor ? tutor?.Documento         : retiro.DNIResponsable,
                RelacionResponsable    = esTutor ? tutor?.RelacionEstudiante : retiro.RelacionResponsable,
                TelefonoResponsable    = esTutor ? (tutor?.Telefono == 0 ? null : tutor?.Telefono.ToString()) : retiro.TelefonoResponsable,
                CorreoResponsable      = esTutor ? tutor?.Correo            : retiro.CorreoResponsable,
            };
        }

        private static string? ComputarEtiqueta(RetiroAnticipado retiro)
        {
            if (retiro.HorarioReingreso != null) return "Reingresado";
            // HorarioLimiteReingreso se almacena con la hora local etiquetada como UTC;
            // comparar solo la hora del día para evitar desfase de zona horaria.
            if (retiro.ConReingreso && retiro.HorarioLimiteReingreso.HasValue
                && DateTime.Now.TimeOfDay > retiro.HorarioLimiteReingreso.Value.TimeOfDay)
                return "ReingresoVencido";
            if (retiro.ConReingreso) return "ConReingreso";
            return null;
        }
    }
}
