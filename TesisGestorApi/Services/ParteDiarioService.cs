using Microsoft.EntityFrameworkCore;
using RepoDB.Entities;
using TesisGestorApi.Data;
using TesisGestorApi.DTOs.ParteDiario;
using TesisGestorApi.Interfaces;

namespace TesisGestorApi.Services
{
    public class ParteDiarioService : IParteDiarioService
    {
        private readonly ApplicationDbContext _context;
        private static readonly TimeSpan LimiteTarde = new(13, 20, 0);

        // RE (retiro express, ≤10% perdido, 0 inasistencia) se trata como Presente.
        // RA y RAE son Retiros Anticipados y se muestran diferenciados de los Ausentes.
        private static readonly HashSet<string> CodigosPresente  = new(StringComparer.OrdinalIgnoreCase) { "P", "LLT", "LLTE", "RE" };
        private static readonly HashSet<string> CodigosRetirado  = new(StringComparer.OrdinalIgnoreCase) { "RA", "RAE" };
        private static readonly HashSet<string> CodigosAusente   = new(StringComparer.OrdinalIgnoreCase) { "A", "LLTC", "ANC" };

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
                .Where(a => a.Fecha == fecha && idsEstudiantes.Contains(a.EstudianteId))
                .ToListAsync();

            var asistenciaDict = asistencias.ToDictionary(a => a.EstudianteId);

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
                                         asistenciaDict, horariosMañana, clasesDict, esMañana: true);
            var turnoTarde  = BuildTurno(estudiantes.Select(e => (e.IdEstudiante, e.Nombre, e.Apellido, e.Documento)).ToList(),
                                         asistenciaDict, horariosTarde, clasesDict, esMañana: false);

            turnoManana.Disponible = horariosMañana.Any();
            turnoTarde.Disponible  = horariosTarde.Any();

            return new ParteDiarioResumenDto { Manana = turnoManana, Tarde = turnoTarde };
        }

        private static TurnoParteDto BuildTurno(
            List<(Guid IdEstudiante, string Nombre, string Apellido, string Documento)> estudiantes,
            Dictionary<Guid, Asistencia> asistenciaDict,
            List<Horario> horarios,
            Dictionary<Guid, ClaseDictada> clasesDict,  // keyed by IdHorario
            bool esMañana)
        {
            var turno = new TurnoParteDto { TotalEstudiantes = estudiantes.Count };

            foreach (var est in estudiantes)
            {
                asistenciaDict.TryGetValue(est.IdEstudiante, out var asist);
                var tipoTurno = esMañana ? asist?.TipoManiana : asist?.TipoTarde;
                string? codigo = tipoTurno?.Codigo;

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

                turno.Estudiantes.Add(new EstudianteParteDto
                {
                    IdEstudiante     = est.IdEstudiante,
                    Nombre           = est.Nombre,
                    Apellido         = est.Apellido,
                    Documento        = est.Documento,
                    Estado           = estado,
                    CodigoAsistencia = codigo,
                    HoraEntrada      = entrada?.ToString(@"hh\:mm"),
                    HoraSalida       = salida?.ToString(@"hh\:mm"),
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
                .Select(c => new ComentarioParteDto
                {
                    IdComentario = c.IdComentario,
                    Timestamp    = c.Timestamp,
                    Contenido    = c.Contenido,
                    Tipo         = c.Tipo.ToString(),
                    Autor        = c.Autor,
                })
                .ToList();
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
                Autor        = comentario.Autor,
            };
        }

        public async Task RegistrarEventoAsync(Guid cursoId, DateOnly fecha, string descripcion)
        {
            var parte = await ObtenerOCrearParteAsync(cursoId, fecha);

            _context.ComentariosParte.Add(new ComentarioParte
            {
                IdComentario = Guid.NewGuid(),
                IdParte      = parte.IdParte,
                Timestamp    = DateTime.UtcNow,
                Contenido    = descripcion,
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

            await RegistrarEventoAsync(cursoId, fecha,
                $"{materiaNombre}: horario restablecido (era {prevEntrada:hh\\:mm}).");
        }

        public async Task ReorganizarHorarioAsync(ReorganizarHorarioDto dto)
        {
            var diaSemana    = dto.Fecha.DayOfWeek;
            var idsHorario   = dto.IdHorariosOrdenados;

            // Cargar los slots con nombres de materia para el log
            var horarios = await _context.Horarios
                .AsNoTracking()
                .Include(h => h.EspacioCurricular).ThenInclude(ec => ec.Curricula)
                .Where(h => idsHorario.Contains(h.IdHorario) && h.DíaSemana == diaSemana)
                .ToListAsync();

            // Tiempos base por slot (IdHorario)
            var basePorSlot = horarios.ToDictionary(h => h.IdHorario, h => (
                Entrada: h.HorarioEntrada,
                Salida:  h.HorarioSalida,
                Nombre:  h.EspacioCurricular.Curricula.Nombre,
                IdEC:    h.IdEC
            ));

            // Orden base: slots ordenados por horario de entrada original (define las "posiciones")
            var ordenBase = basePorSlot.OrderBy(kv => kv.Value.Entrada).Select(kv => kv.Key).ToList();

            // Cargar ClaseDictadas existentes para estos slots
            var clasesExistentes = await _context.ClasesDictadas
                .Where(c => idsHorario.Contains(c.IdHorario) && c.Fecha == dto.Fecha)
                .ToListAsync();
            var clasesDict = clasesExistentes.ToDictionary(c => c.IdHorario);

            var cambios = new List<string>();

            for (int i = 0; i < dto.IdHorariosOrdenados.Count; i++)
            {
                var idHorario = dto.IdHorariosOrdenados[i];
                if (!basePorSlot.TryGetValue(idHorario, out var ownBase)) continue;

                // La posición i tiene los tiempos del slot que originalmente estaba en esa posición
                var idHorarioDelSlot = i < ordenBase.Count ? ordenBase[i] : ordenBase.Last();
                if (!basePorSlot.TryGetValue(idHorarioDelSlot, out var slotTimes)) continue;

                // Obtener o crear ClaseDictada para este slot
                if (!clasesDict.TryGetValue(idHorario, out var clase))
                {
                    clase = new ClaseDictada { IdClaseDictada = Guid.NewGuid(), IdHorario = idHorario, IdEC = ownBase.IdEC, Fecha = dto.Fecha, Dictada = true };
                    _context.ClasesDictadas.Add(clase);
                    clasesDict[idHorario] = clase;
                }

                var prevEntrada = clase.HorarioEntradaEfectiva ?? ownBase.Entrada;
                var prevSalida  = clase.HorarioSalidaEfectiva  ?? ownBase.Salida;

                bool usaBasePropia = slotTimes.Entrada == ownBase.Entrada && slotTimes.Salida == ownBase.Salida;
                clase.HorarioEntradaEfectiva = usaBasePropia ? null : slotTimes.Entrada;
                clase.HorarioSalidaEfectiva  = usaBasePropia ? null : slotTimes.Salida;

                var newEntrada = clase.HorarioEntradaEfectiva ?? ownBase.Entrada;
                var newSalida  = clase.HorarioSalidaEfectiva  ?? ownBase.Salida;

                if (prevEntrada != newEntrada || prevSalida != newSalida)
                    cambios.Add($"• {ownBase.Nombre}: {prevEntrada:hh\\:mm}–{prevSalida:hh\\:mm} → {newEntrada:hh\\:mm}–{newSalida:hh\\:mm}");
            }

            await _context.SaveChangesAsync();

            if (cambios.Any())
                await RegistrarEventoAsync(dto.CursoId, dto.Fecha,
                    $"Horario reorganizado:\n{string.Join("\n", cambios)}");
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
