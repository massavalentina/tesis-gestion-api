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
        // RA, RAE, ANC se tratan como Ausente (misma lógica que el filtro de Asistencia Manual).
        private static readonly HashSet<string> CodigosPresente = new(StringComparer.OrdinalIgnoreCase) { "P", "LLT", "LLTE", "RE" };
        private static readonly HashSet<string> CodigosAusente  = new(StringComparer.OrdinalIgnoreCase) { "A", "LLTC", "RA", "RAE", "ANC" };

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

            // IDs de ECs con horario ese día
            var idsEC = horarios.Select(h => h.IdEC).Distinct().ToList();

            // Clases dictadas para esos ECs ese día
            var clasesDictadas = await _context.ClasesDictadas
                .AsNoTracking()
                .Where(c => idsEC.Contains(c.IdEC) && c.Fecha == fecha)
                .ToListAsync();

            var clasesDict = clasesDictadas.ToDictionary(c => c.IdEC);

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
            Dictionary<Guid, ClaseDictada> clasesDict,
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

            // Agrupar horarios por EC para evitar duplicar materias que tienen varios módulos
            var ecsPorTurno = horarios.GroupBy(h => h.IdEC).ToList();
            foreach (var grupo in ecsPorTurno)
            {
                var primerHorario = grupo.OrderBy(h => h.HorarioEntrada).First();
                var ultimoHorario = grupo.OrderBy(h => h.HorarioSalida).Last();
                clasesDict.TryGetValue(grupo.Key, out var clase);

                bool tieneOverride = clase?.HorarioEntradaEfectiva.HasValue == true;
                turno.HorarioClases.Add(new HorarioClaseDto
                {
                    IdEC                = grupo.Key,
                    IdClaseDictada      = clase?.IdClaseDictada,
                    Materia             = primerHorario.EspacioCurricular.Curricula.Nombre,
                    Docente             = $"{primerHorario.EspacioCurricular.Docente.Apellido}, {primerHorario.EspacioCurricular.Docente.Nombre}",
                    HoraEntrada         = (clase?.HorarioEntradaEfectiva ?? primerHorario.HorarioEntrada).ToString(@"hh\:mm"),
                    HoraSalida          = (clase?.HorarioSalidaEfectiva  ?? ultimoHorario.HorarioSalida).ToString(@"hh\:mm"),
                    HoraEntradaOriginal = tieneOverride ? primerHorario.HorarioEntrada.ToString(@"hh\:mm") : null,
                    HoraSalidaOriginal  = tieneOverride ? ultimoHorario.HorarioSalida.ToString(@"hh\:mm")  : null,
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
            var diaSemana = dto.Fecha.DayOfWeek;
            var idsEC     = new[] { dto.IdEC1, dto.IdEC2 };

            // Tiempos base del Horario programado para cada EC
            var horarios = await _context.Horarios
                .AsNoTracking()
                .Where(h => idsEC.Contains(h.IdEC) && h.DíaSemana == diaSemana)
                .ToListAsync();

            var h1 = horarios.Where(h => h.IdEC == dto.IdEC1).OrderBy(h => h.HorarioEntrada).ToList();
            var h2 = horarios.Where(h => h.IdEC == dto.IdEC2).OrderBy(h => h.HorarioEntrada).ToList();

            if (!h1.Any() || !h2.Any())
                throw new InvalidOperationException("No se encontraron horarios para uno o ambos espacios curriculares.");

            TimeSpan baseEntrada1 = h1.First().HorarioEntrada;
            TimeSpan baseSalida1  = h1.Last().HorarioSalida;
            TimeSpan baseEntrada2 = h2.First().HorarioEntrada;
            TimeSpan baseSalida2  = h2.Last().HorarioSalida;

            // Cargar o crear ClaseDictadas
            var clases = await _context.ClasesDictadas
                .Where(c => idsEC.Contains(c.IdEC) && c.Fecha == dto.Fecha)
                .ToListAsync();

            var clase1 = clases.FirstOrDefault(c => c.IdEC == dto.IdEC1);
            var clase2 = clases.FirstOrDefault(c => c.IdEC == dto.IdEC2);

            if (clase1 == null)
            {
                clase1 = new ClaseDictada { IdClaseDictada = Guid.NewGuid(), IdEC = dto.IdEC1, Fecha = dto.Fecha, Dictada = true };
                _context.ClasesDictadas.Add(clase1);
            }
            if (clase2 == null)
            {
                clase2 = new ClaseDictada { IdClaseDictada = Guid.NewGuid(), IdEC = dto.IdEC2, Fecha = dto.Fecha, Dictada = true };
                _context.ClasesDictadas.Add(clase2);
            }

            // Tiempos efectivos actuales (override ?? base)
            TimeSpan ef1Entrada = clase1.HorarioEntradaEfectiva ?? baseEntrada1;
            TimeSpan ef1Salida  = clase1.HorarioSalidaEfectiva  ?? baseSalida1;
            TimeSpan ef2Entrada = clase2.HorarioEntradaEfectiva ?? baseEntrada2;
            TimeSpan ef2Salida  = clase2.HorarioSalidaEfectiva  ?? baseSalida2;

            // Swap: cada clase toma los tiempos efectivos actuales de la otra
            TimeSpan nuevo1Entrada = ef2Entrada;
            TimeSpan nuevo1Salida  = ef2Salida;
            TimeSpan nuevo2Entrada = ef1Entrada;
            TimeSpan nuevo2Salida  = ef1Salida;

            // Si el resultado coincide con el horario base, limpiar el override
            clase1.HorarioEntradaEfectiva = (nuevo1Entrada == baseEntrada1 && nuevo1Salida == baseSalida1) ? null : nuevo1Entrada;
            clase1.HorarioSalidaEfectiva  = (nuevo1Entrada == baseEntrada1 && nuevo1Salida == baseSalida1) ? null : nuevo1Salida;
            clase2.HorarioEntradaEfectiva = (nuevo2Entrada == baseEntrada2 && nuevo2Salida == baseSalida2) ? null : nuevo2Entrada;
            clase2.HorarioSalidaEfectiva  = (nuevo2Entrada == baseEntrada2 && nuevo2Salida == baseSalida2) ? null : nuevo2Salida;

            await _context.SaveChangesAsync();
        }

        public async Task ResetearHorarioClaseAsync(Guid idEC, DateOnly fecha, Guid cursoId)
        {
            var clase = await _context.ClasesDictadas
                .FirstOrDefaultAsync(c => c.IdEC == idEC && c.Fecha == fecha);

            if (clase == null || !clase.HorarioEntradaEfectiva.HasValue) return;

            var materiaNombre = await _context.EspaciosCurriculares
                .AsNoTracking()
                .Include(ec => ec.Curricula)
                .Where(ec => ec.IdEC == idEC)
                .Select(ec => ec.Curricula.Nombre)
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
            var diaSemana = dto.Fecha.DayOfWeek;
            var idsEC     = dto.IdECsOrdenados;

            // Cargar horarios con nombres de materia para el log
            var horarios = await _context.Horarios
                .AsNoTracking()
                .Include(h => h.EspacioCurricular).ThenInclude(ec => ec.Curricula)
                .Where(h => idsEC.Contains(h.IdEC) && h.DíaSemana == diaSemana)
                .ToListAsync();

            // Tiempos base agrupados por EC
            var basePorEC = horarios
                .GroupBy(h => h.IdEC)
                .ToDictionary(g => g.Key, g => (
                    Entrada: g.OrderBy(h => h.HorarioEntrada).First().HorarioEntrada,
                    Salida:  g.OrderBy(h => h.HorarioSalida).Last().HorarioSalida,
                    Nombre:  g.First().EspacioCurricular.Curricula.Nombre
                ));

            // Orden base: ECs ordenados por su horario de entrada original (define los "slots")
            var ordenBase = basePorEC.OrderBy(kv => kv.Value.Entrada).Select(kv => kv.Key).ToList();

            // Cargar ClaseDictadas existentes
            var clasesExistentes = await _context.ClasesDictadas
                .Where(c => idsEC.Contains(c.IdEC) && c.Fecha == dto.Fecha)
                .ToListAsync();
            var clasesDict = clasesExistentes.ToDictionary(c => c.IdEC);

            var cambios = new List<string>();

            for (int i = 0; i < dto.IdECsOrdenados.Count; i++)
            {
                var idEC = dto.IdECsOrdenados[i];
                if (!basePorEC.TryGetValue(idEC, out var ownBase)) continue;

                // El slot en la posición i tiene los tiempos del EC que originalmente estaba ahí
                var idECDelSlot  = i < ordenBase.Count ? ordenBase[i] : ordenBase.Last();
                if (!basePorEC.TryGetValue(idECDelSlot, out var slotTimes)) continue;

                // Obtener o crear ClaseDictada
                if (!clasesDict.TryGetValue(idEC, out var clase))
                {
                    clase = new ClaseDictada { IdClaseDictada = Guid.NewGuid(), IdEC = idEC, Fecha = dto.Fecha, Dictada = true };
                    _context.ClasesDictadas.Add(clase);
                    clasesDict[idEC] = clase;
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
