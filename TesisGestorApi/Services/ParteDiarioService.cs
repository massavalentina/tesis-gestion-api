using Microsoft.EntityFrameworkCore;
using RepoDB.Entities;
using TesisGestorApi.Data;
using TesisGestorApi.DTOs.Retiro;
using TesisGestorApi.Entities;
using TesisGestorApi.DTOs.ParteDiario;
using TesisGestorApi.Interfaces;

namespace TesisGestorApi.Services
{
    public class ParteDiarioService : IParteDiarioService
    {
        private readonly ApplicationDbContext _context;
        private static readonly TimeSpan LimiteTarde = new(13, 20, 0);

        // RE (retiro express, ≤10% perdido) sigue siendo un retiro y se muestra como tal.
        // RA, RAE y RE se muestran como "Retirado" por estudiante, pero también suman al contador Ausentes.
        private static readonly HashSet<string> CodigosPresente  = new(StringComparer.OrdinalIgnoreCase) { "P", "LLT", "LLTE", "LLTC" };
        private static readonly HashSet<string> CodigosRetirado  = new(StringComparer.OrdinalIgnoreCase) { "RA", "RAE", "RE" };
        private static readonly HashSet<string> CodigosAusente   = new(StringComparer.OrdinalIgnoreCase) { "A", "ANC" };

        public ParteDiarioService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<ParteDiarioResumenDto> ObtenerResumenAsync(Guid cursoId, DateOnly fecha)
        {
            var diaSemana = fecha.DayOfWeek;

            // Estudiantes del curso
            var estudiantes = await _context.DetallesCursado
                .AsNoTracking()
                .Where(dc => dc.IdCurso == cursoId && dc.Estado)
                .Select(dc => new { dc.IdEstudiante, dc.Estudiante.Nombre, dc.Estudiante.Apellido, dc.Estudiante.Documento })
                .ToListAsync();

            var idsEstudiantes = estudiantes.Select(e => e.IdEstudiante).ToList();

            // Asistencias del día
            var asistencias = await _context.Asistencias
                .AsNoTracking()
                .Include(a => a.TipoManiana)
                .Include(a => a.TipoTarde)
                .Include(a => a.TipoLlegadaManiana)
                .Where(a => a.Fecha == fecha && idsEstudiantes.Contains(a.EstudianteId))
                .ToListAsync();

            var asistenciaDict = asistencias.ToDictionary(a => a.EstudianteId);

            // Retiros del día — keyed by IdAsistencia
            var idsAsistencia = asistencias.Select(a => a.Id).ToList();
            var retiros = await _context.RetirosAnticipados
                .AsNoTracking()
                .Include(r => r.Tutor)
                .Where(r => idsAsistencia.Contains(r.IdAsistencia))
                .ToListAsync();
            // Clave compuesta (IdAsistencia, Turno) para soportar un retiro por turno
            var retirosDict = retiros.ToDictionary(r => (r.IdAsistencia, r.Turno));

            // Horarios del día para el curso
            var horarios = await _context.Horarios
                .AsNoTracking()
                .Include(h => h.EspacioCurricular)
                    .ThenInclude(ec => ec.Curricula)
                .Include(h => h.EspacioCurricular)
                    .ThenInclude(ec => ec.Docente)
                .Where(h => h.IdCurso == cursoId && h.DíaSemana == diaSemana)
                .OrderBy(h => h.HorarioEntrada)
                .ToListAsync();

            // IDs de slots de Horario con clase ese día
            var idsHorario = horarios.Select(h => h.IdHorario).Distinct().ToList();

            // Clases dictadas para esos slots ese día
            var clasesDictadas = await _context.ClasesDictadas
                .AsNoTracking()
                .Where(c => idsHorario.Contains(c.IdHorario) && c.Fecha == fecha)
                .ToListAsync();

            var clasesDict = clasesDictadas.ToDictionary(c => c.IdHorario);

            // Separar horarios por turno
            var horariosMañana = horarios.Where(h => h.HorarioEntrada < LimiteTarde).ToList();
            var horariosTarde  = horarios.Where(h => h.HorarioEntrada >= LimiteTarde).ToList();

            var turnoManana = BuildTurno(estudiantes.Select(e => (e.IdEstudiante, e.Nombre, e.Apellido, e.Documento)).ToList(),
                                         asistenciaDict, retirosDict, horariosMañana, clasesDict, esMañana: true);
            var turnoTarde  = BuildTurno(estudiantes.Select(e => (e.IdEstudiante, e.Nombre, e.Apellido, e.Documento)).ToList(),
                                         asistenciaDict, retirosDict, horariosTarde,  clasesDict, esMañana: false);

            turnoManana.Disponible = horariosMañana.Any();
            turnoTarde.Disponible  = horariosTarde.Any();

            return new ParteDiarioResumenDto { Manana = turnoManana, Tarde = turnoTarde };
        }

        private static TurnoParteDto BuildTurno(
            List<(Guid IdEstudiante, string Nombre, string Apellido, string Documento)> estudiantes,
            Dictionary<Guid, Asistencia> asistenciaDict,
            Dictionary<(Guid IdAsistencia, string Turno), RetiroAnticipado> retirosDict,
            List<Horario> horarios,
            Dictionary<Guid, ClaseDictada> clasesDict,  // keyed by IdHorario
            bool esMañana)
        {
            var turno = new TurnoParteDto { TotalEstudiantes = estudiantes.Count };
            string turnoStr = esMañana ? "MANANA" : "TARDE";

            foreach (var est in estudiantes)
            {
                asistenciaDict.TryGetValue(est.IdEstudiante, out var asist);
                var tipoTurno = esMañana ? asist?.TipoManiana : asist?.TipoTarde;
                string? codigo = tipoTurno?.Codigo;

                // Retiro activo del estudiante en este turno (necesario antes del cómputo de contadores)
                RetiroAnticipado? retiro = null;
                if (asist != null)
                    retirosDict.TryGetValue((asist.Id, turnoStr), out retiro);

                string estado;
                if (codigo == null)
                {
                    estado = "SinRegistro";
                    turno.SinRegistro++;
                }
                else if (CodigosPresente.Contains(codigo))
                {
                    estado = "Presente";
                    turno.Presentes++;
                }
                else if (CodigosRetirado.Contains(codigo))
                {
                    estado = "Retirado";
                    turno.Retirados++;
                    // Con reingreso registrado cuenta como presente; sin reingreso, como ausente
                    if (retiro?.HorarioReingreso != null) turno.Presentes++;
                    else turno.Ausentes++;
                }
                else if (CodigosAusente.Contains(codigo))
                {
                    estado = "Ausente";
                    turno.Ausentes++;
                }
                else
                {
                    estado = "SinRegistro";
                    turno.SinRegistro++;
                }

                TimeSpan? entrada = esMañana ? asist?.HoraEntradaManana : asist?.HoraEntradaTarde;
                TimeSpan? salida  = esMañana ? asist?.HoraSalidaManana  : asist?.HoraSalidaTarde;

                // Construir DTO del retiro
                RetiroActivoDto? retiroDto = null;
                if (retiro != null)
                {
                    bool esTutor = retiro.IdTutor.HasValue;
                    var  tutorPd = retiro.Tutor;
                    retiroDto = new RetiroActivoDto
                    {
                        IdRetiro               = retiro.IdRetiro,
                        Turno                  = retiro.Turno,
                        HorarioRetiro          = retiro.HorarioRetiro.ToString(@"HH\:mm"),
                        ConReingreso           = retiro.ConReingreso,
                        HorarioLimiteReingreso = retiro.HorarioLimiteReingreso?.ToString(@"HH\:mm"),
                        HorarioReingreso       = retiro.HorarioReingreso?.ToString(@"HH\:mm"),
                        EtiquetaEstado         = ComputarEtiqueta(retiro),
                        TipoRetiro             = codigo,
                        NombrePreceptor        = retiro.NombrePreceptor,
                        Motivo                 = retiro.Motivo,
                        IdTutor                = retiro.IdTutor,
                        NombreResponsable      = esTutor ? tutorPd?.Nombre            : retiro.NombreResponsable,
                        ApellidoResponsable    = esTutor ? tutorPd?.Apellido          : retiro.ApellidoResponsable,
                        DniResponsable         = esTutor ? tutorPd?.Documento         : retiro.DNIResponsable,
                        RelacionResponsable    = esTutor ? tutorPd?.RelacionEstudiante : retiro.RelacionResponsable,
                        TelefonoResponsable    = esTutor ? (tutorPd?.Telefono == 0 ? null : tutorPd?.Telefono.ToString()) : retiro.TelefonoResponsable,
                        CorreoResponsable      = esTutor ? tutorPd?.Correo            : retiro.CorreoResponsable,
                    };
                }

                turno.Estudiantes.Add(new EstudianteParteDto
                {
                    IdEstudiante        = est.IdEstudiante,
                    Nombre              = est.Nombre,
                    Apellido            = est.Apellido,
                    Documento           = est.Documento,
                    Estado              = estado,
                    CodigoAsistencia    = codigo,
                    CodigoLlegadaManiana = esMañana ? asist?.TipoLlegadaManiana?.Codigo : null,
                    HoraEntrada         = entrada?.ToString(@"hh\:mm"),
                    HoraSalida          = salida?.ToString(@"hh\:mm"),
                    RetiroActivo        = retiroDto,
                });
            }

            turno.PorcentajeAsistencia = turno.TotalEstudiantes == 0
                ? 0
                : Math.Round((double)turno.Presentes / turno.TotalEstudiantes * 100, 1);

            // Un HorarioClaseDto por slot (IdHorario). Cada slot es totalmente independiente:
            // tiene su propio ClaseDictada con sus propios tiempos efectivos.
            foreach (var h in horarios)
            {
                clasesDict.TryGetValue(h.IdHorario, out var clase);
                bool tieneOverride  = clase?.HorarioEntradaEfectiva.HasValue == true;
                TimeSpan entradaEf  = clase?.HorarioEntradaEfectiva ?? h.HorarioEntrada;
                TimeSpan salidaEf   = clase?.HorarioSalidaEfectiva  ?? h.HorarioSalida;

                turno.HorarioClases.Add(new HorarioClaseDto
                {
                    IdHorario           = h.IdHorario,
                    IdEC                = h.IdEC,
                    IdClaseDictada      = clase?.IdClaseDictada,
                    Materia             = h.EspacioCurricular.Curricula.Nombre,
                    Docente             = $"{h.EspacioCurricular.Docente.Apellido}, {h.EspacioCurricular.Docente.Nombre}",
                    HoraEntrada         = entradaEf.ToString(@"hh\:mm"),
                    HoraSalida          = salidaEf.ToString(@"hh\:mm"),
                    HoraEntradaOriginal = tieneOverride ? h.HorarioEntrada.ToString(@"hh\:mm") : null,
                    HoraSalidaOriginal  = tieneOverride ? h.HorarioSalida.ToString(@"hh\:mm")  : null,
                    Dictada             = clase?.Dictada,
                    Motivo              = clase?.Motivo,
                    Tema                = clase?.Tema,
                });
            }

            turno.HorarioClases = turno.HorarioClases.OrderBy(h => h.HoraEntrada).ToList();

            return turno;
        }

        public async Task<List<ComentarioParteDto>> ObtenerComentariosAsync(Guid cursoId, DateOnly fecha)
        {
            var parte = await _context.PartesDiarios
                .AsNoTracking()
                .Include(p => p.Comentarios)
                .FirstOrDefaultAsync(p => p.IdCurso == cursoId && p.Fecha == fecha);

            if (parte == null) return new List<ComentarioParteDto>();

            return parte.Comentarios
                .OrderByDescending(c => c.Timestamp)
                .Select(c =>
                {
                    var (subTipo, titulo, detalle) = ParseContenido(c.Contenido, c.Tipo);
                    return new ComentarioParteDto
                    {
                        IdComentario = c.IdComentario,
                        Timestamp    = c.Timestamp,
                        Contenido    = c.Contenido,
                        Tipo         = c.Tipo.ToString(),
                        SubTipo      = subTipo,
                        Titulo       = titulo,
                        Detalle      = detalle,
                        Autor        = c.Autor,
                    };
                })
                .ToList();
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

        private static (string subTipo, string? titulo, string? detalle) ParseContenido(string contenido, TipoComentarioParte tipo)
        {
            if (tipo == TipoComentarioParte.Comentario)
                return ("NOTA", null, null);

            var lines = contenido.Split('\n');

            // Formato nuevo estructurado: primera línea es [SUBTYPE]
            if (lines.Length > 0 && lines[0].StartsWith("[") && lines[0].EndsWith("]"))
            {
                var subTipo = lines[0][1..^1];
                var titulo  = lines.Length > 1 ? lines[1] : string.Empty;
                var detalle = lines.Length > 2 ? string.Join("\n", lines[2..]) : null;
                return (subTipo, titulo, string.IsNullOrWhiteSpace(detalle) ? null : detalle);
            }

            // Formato legacy — detección heurística
            var firstLine = lines[0];
            var restLines = lines.Length > 1 ? string.Join("\n", lines[1..]) : null;

            if (firstLine.StartsWith("Asistencia actualizada"))
                return ("ASISTENCIA", firstLine.TrimEnd(':'), string.IsNullOrWhiteSpace(restLines) ? null : restLines);

            return ("HORARIO", firstLine.TrimEnd(':'), string.IsNullOrWhiteSpace(restLines) ? null : restLines);
        }

        public async Task<ComentarioParteDto> AgregarComentarioAsync(AgregarComentarioDto dto)
        {
            var parte = await ObtenerOCrearParteAsync(dto.CursoId, dto.Fecha);

            var comentario = new ComentarioParte
            {
                IdComentario = Guid.NewGuid(),
                IdParte      = parte.IdParte,
                Timestamp    = DateTime.UtcNow,
                Contenido    = dto.Contenido,
                Tipo         = TipoComentarioParte.Comentario,
                Autor        = dto.Autor,
            };

            _context.ComentariosParte.Add(comentario);
            await _context.SaveChangesAsync();

            return new ComentarioParteDto
            {
                IdComentario = comentario.IdComentario,
                Timestamp    = comentario.Timestamp,
                Contenido    = comentario.Contenido,
                Tipo         = comentario.Tipo.ToString(),
                SubTipo      = "NOTA",
                Titulo       = null,
                Detalle      = null,
                Autor        = comentario.Autor,
            };
        }

        public async Task RegistrarEventoAsync(Guid cursoId, DateOnly fecha, string subTipo, string titulo, string? detalle = null)
        {
            var parte    = await ObtenerOCrearParteAsync(cursoId, fecha);
            string contenido = detalle != null
                ? $"[{subTipo}]\n{titulo}\n{detalle}"
                : $"[{subTipo}]\n{titulo}";

            _context.ComentariosParte.Add(new ComentarioParte
            {
                IdComentario = Guid.NewGuid(),
                IdParte      = parte.IdParte,
                Timestamp    = DateTime.UtcNow,
                Contenido    = contenido,
                Tipo         = TipoComentarioParte.Evento,
                Autor        = "Sistema",
            });

            await _context.SaveChangesAsync();
        }

        public async Task IntercambiarHorarioClasesAsync(IntercambiarHorarioDto dto)
        {
            var idsHorario = new[] { dto.IdHorario1, dto.IdHorario2 };

            // Cargar los dos slots de horario
            var horarios = await _context.Horarios
                .AsNoTracking()
                .Where(h => idsHorario.Contains(h.IdHorario))
                .ToListAsync();

            var h1 = horarios.FirstOrDefault(h => h.IdHorario == dto.IdHorario1);
            var h2 = horarios.FirstOrDefault(h => h.IdHorario == dto.IdHorario2);

            if (h1 == null || h2 == null)
                throw new InvalidOperationException("No se encontraron los slots de horario especificados.");

            // Cargar o crear ClaseDictadas por slot
            var clases = await _context.ClasesDictadas
                .Where(c => idsHorario.Contains(c.IdHorario) && c.Fecha == dto.Fecha)
                .ToListAsync();

            var clase1 = clases.FirstOrDefault(c => c.IdHorario == dto.IdHorario1);
            var clase2 = clases.FirstOrDefault(c => c.IdHorario == dto.IdHorario2);

            if (clase1 == null)
            {
                clase1 = new ClaseDictada { IdClaseDictada = Guid.NewGuid(), IdHorario = dto.IdHorario1, IdEC = h1.IdEC, Fecha = dto.Fecha, Dictada = true };
                _context.ClasesDictadas.Add(clase1);
            }
            if (clase2 == null)
            {
                clase2 = new ClaseDictada { IdClaseDictada = Guid.NewGuid(), IdHorario = dto.IdHorario2, IdEC = h2.IdEC, Fecha = dto.Fecha, Dictada = true };
                _context.ClasesDictadas.Add(clase2);
            }

            // Tiempos efectivos actuales (override ?? base del slot)
            TimeSpan ef1Entrada = clase1.HorarioEntradaEfectiva ?? h1.HorarioEntrada;
            TimeSpan ef1Salida  = clase1.HorarioSalidaEfectiva  ?? h1.HorarioSalida;
            TimeSpan ef2Entrada = clase2.HorarioEntradaEfectiva ?? h2.HorarioEntrada;
            TimeSpan ef2Salida  = clase2.HorarioSalidaEfectiva  ?? h2.HorarioSalida;

            // Swap: cada slot toma los tiempos efectivos del otro
            TimeSpan nuevo1Entrada = ef2Entrada;
            TimeSpan nuevo1Salida  = ef2Salida;
            TimeSpan nuevo2Entrada = ef1Entrada;
            TimeSpan nuevo2Salida  = ef1Salida;

            // Si el resultado coincide con el horario base del slot, limpiar el override
            clase1.HorarioEntradaEfectiva = (nuevo1Entrada == h1.HorarioEntrada && nuevo1Salida == h1.HorarioSalida) ? null : nuevo1Entrada;
            clase1.HorarioSalidaEfectiva  = (nuevo1Entrada == h1.HorarioEntrada && nuevo1Salida == h1.HorarioSalida) ? null : nuevo1Salida;
            clase2.HorarioEntradaEfectiva = (nuevo2Entrada == h2.HorarioEntrada && nuevo2Salida == h2.HorarioSalida) ? null : nuevo2Entrada;
            clase2.HorarioSalidaEfectiva  = (nuevo2Entrada == h2.HorarioEntrada && nuevo2Salida == h2.HorarioSalida) ? null : nuevo2Salida;

            await _context.SaveChangesAsync();
        }

        public async Task ResetearHorarioClaseAsync(Guid idHorario, DateOnly fecha, Guid cursoId)
        {
            var clase = await _context.ClasesDictadas
                .FirstOrDefaultAsync(c => c.IdHorario == idHorario && c.Fecha == fecha);

            if (clase == null || !clase.HorarioEntradaEfectiva.HasValue) return;

            var materiaNombre = await _context.Horarios
                .AsNoTracking()
                .Include(h => h.EspacioCurricular).ThenInclude(ec => ec.Curricula)
                .Where(h => h.IdHorario == idHorario)
                .Select(h => h.EspacioCurricular.Curricula.Nombre)
                .FirstOrDefaultAsync() ?? "Clase";

            var prevEntrada = clase.HorarioEntradaEfectiva.Value;
            clase.HorarioEntradaEfectiva = null;
            clase.HorarioSalidaEfectiva  = null;
            await _context.SaveChangesAsync();

            await RegistrarEventoAsync(cursoId, fecha, "HORARIO",
                $"{materiaNombre}: horario restablecido",
                $"Horario anterior: {prevEntrada:hh\\:mm}");
        }

        public async Task ReorganizarHorarioAsync(ReorganizarHorarioDto dto)
        {
            var diaSemana  = dto.Fecha.DayOfWeek;
            var idsHorario = dto.Slots.Select(s => s.IdHorario).ToList();

            // Cargar horarios base (para comparar con los tiempos nuevos y para el log)
            var horarios = await _context.Horarios
                .AsNoTracking()
                .Include(h => h.EspacioCurricular).ThenInclude(ec => ec.Curricula)
                .Where(h => idsHorario.Contains(h.IdHorario) && h.DíaSemana == diaSemana)
                .ToDictionaryAsync(h => h.IdHorario);

            // Cargar ClaseDictadas existentes para estos slots
            var clasesExistentes = await _context.ClasesDictadas
                .Where(c => idsHorario.Contains(c.IdHorario) && c.Fecha == dto.Fecha)
                .ToListAsync();
            var clasesDict = clasesExistentes.ToDictionary(c => c.IdHorario);

            var cambios = new List<string>();

            foreach (var slot in dto.Slots)
            {
                if (!horarios.TryGetValue(slot.IdHorario, out var horario)) continue;

                var nuevaEntrada = TimeSpan.Parse(slot.HoraEntrada);
                var nuevaSalida  = TimeSpan.Parse(slot.HoraSalida);

                // Si el preceptor eligió exactamente el horario base, se limpia el override
                bool esBasePropia = nuevaEntrada == horario.HorarioEntrada
                                 && nuevaSalida  == horario.HorarioSalida;

                if (!clasesDict.TryGetValue(slot.IdHorario, out var clase))
                {
                    // Sin registro previo y sin cambio — nada que hacer
                    if (esBasePropia) continue;

                    clase = new ClaseDictada
                    {
                        IdClaseDictada = Guid.NewGuid(),
                        IdHorario      = slot.IdHorario,
                        IdEC           = horario.IdEC,
                        Fecha          = dto.Fecha,
                        Dictada        = true,
                    };
                    _context.ClasesDictadas.Add(clase);
                    clasesDict[slot.IdHorario] = clase;
                }

                var prevEntrada = clase.HorarioEntradaEfectiva ?? horario.HorarioEntrada;
                var prevSalida  = clase.HorarioSalidaEfectiva  ?? horario.HorarioSalida;

                clase.HorarioEntradaEfectiva = esBasePropia ? null : nuevaEntrada;
                clase.HorarioSalidaEfectiva  = esBasePropia ? null : nuevaSalida;

                if (prevEntrada != nuevaEntrada || prevSalida != nuevaSalida)
                    cambios.Add($"• {horario.EspacioCurricular.Curricula.Nombre}: " +
                                $"{prevEntrada:hh\\:mm}–{prevSalida:hh\\:mm} → {nuevaEntrada:hh\\:mm}–{nuevaSalida:hh\\:mm}");
            }

            await _context.SaveChangesAsync();

            if (cambios.Any())
                await RegistrarEventoAsync(dto.CursoId, dto.Fecha, "HORARIO",
                    "Horario reorganizado",
                    string.Join("\n", cambios));
        }

        private async Task<ParteDiario> ObtenerOCrearParteAsync(Guid cursoId, DateOnly fecha)
        {
            var parte = await _context.PartesDiarios
                .FirstOrDefaultAsync(p => p.IdCurso == cursoId && p.Fecha == fecha);

            if (parte == null)
            {
                parte = new ParteDiario
                {
                    IdParte = Guid.NewGuid(),
                    IdCurso = cursoId,
                    Fecha   = fecha,
                };
                _context.PartesDiarios.Add(parte);
                await _context.SaveChangesAsync();
            }

            return parte;
        }
    }
}
