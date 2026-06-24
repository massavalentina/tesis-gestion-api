using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using TesisGestorApi.Data;
using TesisGestorApi.DTOs.Evaluaciones;
using TesisGestorApi.DTOs.Planificaciones;
using TesisGestorApi.Entities;
using TesisGestorApi.Interfaces;

namespace TesisGestorApi.Services;

public class EvaluacionesService : IEvaluacionesService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public EvaluacionesService(ApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<GestionEvaluacionesDto> GetGestionAsync(Guid idEC, Guid idDocente, CancellationToken ct)
    {
        var espacio = await GetEspacioContextAsync(idEC, idDocente, ct);
        var trazabilidad = await GetTrazabilidadContextAsync(idEC, ct);

        var instancias = await LoadInstanciasAsync(idEC, ct);
        ValidateInstancias(instancias);

        var bloqueIdsPorArchivo = trazabilidad.Disponible
            ? await LoadBloquesPorArchivoAsync(instancias, ct)
            : new Dictionary<Guid, List<Guid>>();

        var slots = new List<InstanciaEvaluativaSlotDto>();
        for (var nro = 1; nro <= 8; nro++)
        {
            var instancia = instancias.FirstOrDefault(i => i.Nro == nro);
            slots.Add(MapSlotDto(nro, instancia, bloqueIdsPorArchivo));
        }

        return new GestionEvaluacionesDto
        {
            SinPrograma = trazabilidad.Programa is null,
            TrazabilidadDisponible = trazabilidad.Disponible,
            MensajeTrazabilidad = trazabilidad.Mensaje,
            IdPrograma = trazabilidad.Programa?.IdPrograma,
            TituloPrograma = trazabilidad.Programa?.Titulo,
            EstadoPrograma = trazabilidad.Programa?.Estado.ToString(),
            OrigenPrograma = trazabilidad.Programa?.Origen.ToString(),
            NombreMateria = espacio.NombreMateria,
            NombreDocente = espacio.NombreDocente,
            NombreCurso = espacio.NombreCurso,
            AnioLectivo = espacio.AnioLectivo,
            Unidades = trazabilidad.Disponible ? trazabilidad.Unidades : new List<UnidadArbolDto>(),
            Instancias = slots,
        };
    }

    public async Task<InstanciaEvaluativaSlotDto> GuardarArchivoAsync(
        Guid idEC,
        Guid idDocente,
        int nro,
        string tipoCalificacion,
        GuardarArchivoIEFormDto dto,
        string? urlArchivo,
        CancellationToken ct)
    {
        if (dto is null)
        {
            throw new ValidationException("No se recibieron datos para guardar el archivo.");
        }

        var espacio = await GetEspacioContextAsync(idEC, idDocente, ct);
        var tipo = ParseTipoCalificacion(tipoCalificacion);
        var tipoIE = ParseTipoIE(dto.TipoIE);
        var fechaEjecucion = ParseFecha(dto.FechaEjecucion);
        var estadoSolicitado = ParseEstado(dto.Estado);
        var trazabilidad = await GetTrazabilidadContextAsync(idEC, ct);
        var now = DateTime.UtcNow;
        var idUsuario = _currentUser.UserId ?? throw new UnauthorizedAccessException("Usuario no autenticado.");

        if (trazabilidad.Programa is null)
        {
            throw new InvalidOperationException(
                "No podés cargar instancias evaluativas porque este espacio todavía no tiene un programa vigente. Primero cargá el programa desde la sección Programas.");
        }

        if (nro is < 1 or > 8)
        {
            throw new ValidationException("El número de la instancia evaluativa debe estar entre 1 y 8.");
        }

        if (fechaEjecucion.Year != espacio.AnioLectivo)
        {
            throw new ValidationException(
                $"La fecha de ejecución debe pertenecer al año lectivo {espacio.AnioLectivo}.");
        }

        var instancia = await _db.InstanciasEvaluativas
            .Include(i => i.Archivos)
                .ThenInclude(a => a.Calificaciones)
            .Include(i => i.Archivos)
                .ThenInclude(a => a.ArchivoAnterior)
            .FirstOrDefaultAsync(i => i.IdEC == idEC && i.Nro == nro, ct);

        var activos = instancia?.Archivos.Where(a => a.Habilitada).ToList() ?? new List<ArchivoIE>();
        ValidateInstanciaNuevosArchivos(activos);
        ValidarDependenciasParaCarga(activos, tipo);
        ValidarOrdenFechasExamenes(activos, tipo, fechaEjecucion);

        var archivoActivo = activos.FirstOrDefault(a => a.TipoCalificacion == tipo);
        var necesitaArchivo = !string.IsNullOrWhiteSpace(urlArchivo);
        var bloqueIds = await ResolverBloquesAsync(trazabilidad, dto.IdBloquesTema);

        if (archivoActivo is null)
        {
            if (!necesitaArchivo)
            {
                throw new InvalidOperationException($"Para crear la instancia '{nro}' en el tipo '{ToTipoCalificacionCode(tipo)}' tenés que adjuntar un archivo.");
            }

            instancia ??= new InstanciaEvaluativa
            {
                IdIE = Guid.NewGuid(),
                IdEC = idEC,
                Nro = nro,
                Estado = fechaEjecucion.Date < DateTime.UtcNow.Date
                    ? EstadoInstanciaEvaluativa.Evaluada
                    : estadoSolicitado,
                FechaCreacion = now,
                FechaModificacion = now,
            };

            if (_db.Entry(instancia).State == EntityState.Detached)
            {
                _db.InstanciasEvaluativas.Add(instancia);
            }

            var nuevo = CrearArchivo(instancia.IdIE, tipo, tipoIE, dto, urlArchivo!, idUsuario, now, null);
            _db.ArchivosIE.Add(nuevo);
            VincularTrazabilidad(nuevo, bloqueIds);

            await _db.SaveChangesAsync(ct);
            instancia.Estado = fechaEjecucion.Date < DateTime.UtcNow.Date
                ? EstadoInstanciaEvaluativa.Evaluada
                : estadoSolicitado;
            instancia.FechaModificacion = now;
            await _db.SaveChangesAsync(ct);
            return await GetSlotAsync(instancia.IdIE, ct);
        }

        if (TieneNotasVigentes(archivoActivo))
        {
            throw new InvalidOperationException(
                $"No se puede modificar el archivo {ToTipoCalificacionCode(tipo)} de la IE {nro} porque ya tiene calificaciones vinculadas.");
        }

        ValidarDependenciasParaReemplazo(activos, tipo);

        if (instancia is null)
        {
            throw new InvalidOperationException("La instancia evaluativa no existe para actualizar el archivo.");
        }

        archivoActivo.Titulo = dto.Titulo.Trim();
        archivoActivo.TipoIE = tipoIE;
        archivoActivo.FechaEjecucion = fechaEjecucion;
        archivoActivo.FechaModificacion = now;

        if (necesitaArchivo)
        {
            archivoActivo.Habilitada = false;

            var nuevo = CrearArchivo(instancia.IdIE, tipo, tipoIE, dto, urlArchivo!, idUsuario, now, archivoActivo.IdArchivoIE);
            _db.ArchivosIE.Add(nuevo);
            VincularTrazabilidad(nuevo, bloqueIds);
        }
        else
        {
            ReemplazarTrazabilidad(archivoActivo, bloqueIds);
        }

        instancia.Estado = estadoSolicitado;
        instancia.FechaModificacion = now;
        await _db.SaveChangesAsync(ct);

        return await GetSlotAsync(instancia.IdIE, ct);
    }

    public async Task<InstanciaEvaluativaSlotDto> CambiarEstadoAsync(
        Guid idEC,
        Guid idDocente,
        int nro,
        CambiarEstadoIEFormDto dto,
        CancellationToken ct)
    {
        if (dto is null)
        {
            throw new ValidationException("No se recibieron datos para actualizar el estado de la IE.");
        }

        _ = await GetEspacioContextAsync(idEC, idDocente, ct);
        var estadoNuevo = ParseEstado(dto.Estado);

        if (nro is < 1 or > 8)
        {
            throw new ValidationException("El número de la instancia evaluativa debe estar entre 1 y 8.");
        }

        var instancia = await _db.InstanciasEvaluativas
            .FirstOrDefaultAsync(i => i.IdEC == idEC && i.Nro == nro, ct)
            ?? throw new KeyNotFoundException("No existe una instancia evaluativa para ese número.");

        instancia.Estado = estadoNuevo;
        instancia.FechaModificacion = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return await GetSlotAsync(instancia.IdIE, ct);
    }

    public async Task EliminarArchivoAsync(Guid idEC, Guid idDocente, int nro, string tipoCalificacion, CancellationToken ct)
    {
        var tipo = ParseTipoCalificacion(tipoCalificacion);
        var ahora = DateTime.UtcNow;

        var instancia = await _db.InstanciasEvaluativas
            .Include(i => i.Archivos)
                .ThenInclude(a => a.Calificaciones)
            .Include(i => i.Archivos)
                .ThenInclude(a => a.ArchivoAnterior)
            .FirstOrDefaultAsync(i => i.IdEC == idEC && i.Nro == nro, ct)
            ?? throw new KeyNotFoundException("No existe una instancia evaluativa para ese número.");

        await GetEspacioContextAsync(idEC, idDocente, ct);

        var activos = instancia.Archivos.Where(a => a.Habilitada).ToList();
        ValidateInstanciaNuevosArchivos(activos);

        var archivoActivo = activos.FirstOrDefault(a => a.TipoCalificacion == tipo)
            ?? throw new KeyNotFoundException($"No existe un archivo activo del tipo '{ToTipoCalificacionCode(tipo)}' en la IE {nro}.");

        if (TieneNotasVigentes(archivoActivo))
        {
            throw new InvalidOperationException(
                $"No se puede eliminar el archivo {ToTipoCalificacionCode(tipo)} de la IE {nro} porque ya tiene calificaciones vinculadas.");
        }

        ValidarDependenciasParaReemplazo(activos, tipo);

        var archivoPrevio = archivoActivo.ArchivoAnterior;
        if (archivoPrevio is not null)
        {
            archivoPrevio.Habilitada = true;
            archivoPrevio.FechaModificacion = ahora;
        }

        _db.ArchivosIE.Remove(archivoActivo);
        instancia.FechaModificacion = ahora;

        await _db.SaveChangesAsync(ct);

        if (!instancia.Archivos.Any(a => a.Habilitada))
        {
            _db.InstanciasEvaluativas.Remove(instancia);
            await _db.SaveChangesAsync(ct);
        }
    }

    private async Task<EspacioContext> GetEspacioContextAsync(Guid idEC, Guid idDocente, CancellationToken ct)
    {
        var espacio = await _db.EspaciosCurriculares
            .AsNoTracking()
            .Where(ec => ec.IdEC == idEC)
            .Select(ec => new EspacioContext(
                ec.IdEC,
                ec.IdCurso,
                ec.IdDocente,
                ec.Curricula.Nombre,
                ec.Docente != null
                    ? $"{ec.Docente.Usuario.Nombre} {ec.Docente.Usuario.Apellido}"
                    : "Sin docente asignado",
                $"{ec.Curso.Anio.Numero}°{ec.Curso.Division.Nombre}",
                ec.Curso.AñoLectivo.Year))
            .FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Espacio curricular no encontrado.");

        if (espacio.IdDocente != idDocente)
        {
            throw new UnauthorizedAccessException("No sos el docente titular de este espacio curricular.");
        }

        return espacio;
    }

    private async Task<TrazabilidadContext> GetTrazabilidadContextAsync(Guid idEC, CancellationToken ct)
    {
        var programa = await _db.Programas
            .AsNoTracking()
            .Where(p => p.IdEC == idEC && p.Estado == EstadoPrograma.Vigente)
            .Include(p => p.EspacioCurricular)
                .ThenInclude(ec => ec.Curricula)
            .FirstOrDefaultAsync(ct);

        if (programa is null)
        {
            return TrazabilidadContext.SinPrograma("No hay programa vigente. Primero cargá un programa desde la sección Programas para habilitar las instancias evaluativas.");
        }

        if (programa.Origen == OrigenPrograma.Archivo)
        {
            return TrazabilidadContext.SinTrazabilidad(
                programa,
                "El programa vigente fue cargado como archivo PDF. Podés gestionar las instancias evaluativas, pero sin vinculación a temas.");
        }

        var bloques = await _db.BloquesProgramas
            .AsNoTracking()
            .Where(b => b.IdPrograma == programa.IdPrograma)
            .Include(b => b.Unidad)
            .Include(b => b.Tema)
            .ToListAsync(ct);

        if (bloques.Count == 0)
        {
            return TrazabilidadContext.SinTrazabilidad(
                programa,
                "El programa vigente todavía no tiene unidades y temas disponibles para vincular instancias evaluativas.");
        }

        var unidades = bloques
            .Where(b => b.Tipo == TipoBloquePrograma.Unidad)
            .OrderBy(b => b.Unidad.Nro)
            .Select(bu =>
            {
                var temas = bloques
                    .Where(bt => bt.Tipo == TipoBloquePrograma.Tema && bt.IdUnidad == bu.IdUnidad)
                    .OrderBy(bt => bt.Tema!.Nro)
                    .Select(bt => new TemaArbolDto
                    {
                        IdTema = bt.IdTema!.Value,
                        IdBloquePrograma = bt.IdBloquePrograma,
                        Titulo = bt.Tema!.Titulo,
                        Descripcion = bt.Tema.Descripcion,
                        Nro = bt.Tema.Nro,
                        Estado = bt.Estado.ToString(),
                        Clases = new List<ClasePlanificacionDto>(),
                    })
                    .ToList();

                return new UnidadArbolDto
                {
                    IdUnidad = bu.IdUnidad,
                    IdBloquePrograma = bu.IdBloquePrograma,
                    Titulo = bu.Unidad.Titulo,
                    Descripcion = bu.Unidad.Descripcion,
                    Nro = bu.Unidad.Nro,
                    Estado = bu.Estado.ToString(),
                    Temas = temas,
                };
            })
            .ToList();

        return TrazabilidadContext.ConTrazabilidad(programa, unidades);
    }

    private async Task<List<InstanciaReadModel>> LoadInstanciasAsync(Guid idEC, CancellationToken ct)
    {
        return await _db.InstanciasEvaluativas
            .AsNoTracking()
            .Where(i => i.IdEC == idEC)
            .OrderBy(i => i.Nro)
            .Select(i => new InstanciaReadModel(
                i.IdIE,
                i.IdEC,
                i.Nro,
                i.Estado,
                i.Archivos
                    .Where(a => a.Habilitada)
                    .OrderBy(a => a.FechaCarga)
                    .Select(a => new ArchivoReadModel(
                        a.IdArchivoIE,
                        a.TipoCalificacion,
                        a.TipoIE,
                        a.Titulo,
                        a.NombreArchivo,
                        a.UrlArchivo,
                        a.FechaEjecucion,
                        a.FechaCarga,
                        a.Habilitada,
                        a.Calificaciones.Any(c => c.Habilitada && c.Puntaje.HasValue),
                        a.IdArchivoIEAnterior))
                    .ToList()))
            .ToListAsync(ct);
    }

    private async Task<Dictionary<Guid, List<Guid>>> LoadBloquesPorArchivoAsync(IEnumerable<InstanciaReadModel> instancias, CancellationToken ct)
    {
        var archivoIds = instancias.SelectMany(i => i.Archivos).Select(a => a.IdArchivoIE).ToList();
        if (archivoIds.Count == 0)
        {
            return new Dictionary<Guid, List<Guid>>();
        }

        var vinculaciones = await _db.ArchivosIEBloquesProgramas
            .AsNoTracking()
            .Where(x => archivoIds.Contains(x.IdArchivoIE))
            .Select(x => new { x.IdArchivoIE, x.IdBloquePrograma })
            .ToListAsync(ct);

        return vinculaciones
            .GroupBy(x => x.IdArchivoIE)
            .ToDictionary(g => g.Key, g => g.Select(x => x.IdBloquePrograma).Distinct().ToList());
    }

    private static void ValidateInstancias(List<InstanciaReadModel> instancias)
    {
        if (instancias.Count > 8)
        {
            throw new InvalidOperationException("El espacio curricular tiene más de 8 instancias evaluativas registradas para el año lectivo.");
        }

        if (instancias.Any(i => i.Nro < 1 || i.Nro > 8))
        {
            throw new InvalidOperationException("Se detectaron instancias evaluativas con un número fuera del rango permitido 1..8.");
        }

        if (instancias.GroupBy(i => i.Nro).Any(g => g.Count() > 1))
        {
            throw new InvalidOperationException("Se detectaron instancias evaluativas duplicadas para el mismo número.");
        }

        if (instancias.Any(i => i.Archivos.GroupBy(a => a.TipoCalificacion).Any(g => g.Count() > 1)))
        {
            throw new InvalidOperationException("Se detectaron múltiples archivos activos para el mismo tipo de calificación en una instancia evaluativa.");
        }
    }

    private static void ValidateInstanciaNuevosArchivos(List<ArchivoIE> activos)
    {
        if (activos.GroupBy(a => a.TipoCalificacion).Any(g => g.Count() > 1))
        {
            throw new InvalidOperationException("Se detectaron múltiples archivos activos para el mismo tipo de calificación en una instancia evaluativa.");
        }
    }

    private static void ValidarOrdenFechasExamenes(List<ArchivoIE> activos, TipoCalificacion tipoObjetivo, DateTime fechaObjetivo)
    {
        var fechas = activos
            .Where(a => a.Habilitada)
            .GroupBy(a => a.TipoCalificacion)
            .ToDictionary(g => g.Key, g => g.First().FechaEjecucion.Date);

        fechas[tipoObjetivo] = fechaObjetivo.Date;

        if (fechas.TryGetValue(TipoCalificacion.NotaOriginal, out var fechaN) &&
            fechas.TryGetValue(TipoCalificacion.Recuperatorio1, out var fechaR1) &&
            fechaR1 < fechaN)
        {
            throw new InvalidOperationException("La fecha de R1 no puede ser anterior a la de N.");
        }

        if (fechas.TryGetValue(TipoCalificacion.NotaOriginal, out fechaN) &&
            fechas.TryGetValue(TipoCalificacion.Recuperatorio2, out var fechaR2) &&
            fechaR2 < fechaN)
        {
            throw new InvalidOperationException("La fecha de R2 no puede ser anterior a la de N.");
        }

        if (fechas.TryGetValue(TipoCalificacion.Recuperatorio1, out fechaR1) &&
            fechas.TryGetValue(TipoCalificacion.Recuperatorio2, out fechaR2) &&
            fechaR2 < fechaR1)
        {
            throw new InvalidOperationException("La fecha de R2 no puede ser anterior a la de R1.");
        }
    }

    private static InstanciaEvaluativaSlotDto MapSlotDto(
        int nro,
        InstanciaReadModel? instancia,
        IReadOnlyDictionary<Guid, List<Guid>> bloqueIdsPorArchivo)
    {
        if (instancia is null)
        {
            return new InstanciaEvaluativaSlotDto
            {
                Nro = nro,
                Existe = false,
                Estado = "SinCarga",
            };
        }

        return new InstanciaEvaluativaSlotDto
        {
            IdIE = instancia.IdIE,
            Nro = instancia.Nro,
            Existe = true,
            Estado = instancia.Estado.ToString(),
            NotaOriginal = MapArchivoDto(instancia.Archivos, TipoCalificacion.NotaOriginal, bloqueIdsPorArchivo),
            Recuperatorio1 = MapArchivoDto(instancia.Archivos, TipoCalificacion.Recuperatorio1, bloqueIdsPorArchivo),
            Recuperatorio2 = MapArchivoDto(instancia.Archivos, TipoCalificacion.Recuperatorio2, bloqueIdsPorArchivo),
        };
    }

    private async Task<InstanciaEvaluativaSlotDto> GetSlotAsync(Guid idIE, CancellationToken ct)
    {
        var instancia = await _db.InstanciasEvaluativas
            .AsNoTracking()
            .Where(i => i.IdIE == idIE)
            .Select(i => new InstanciaReadModel(
                i.IdIE,
                i.IdEC,
                i.Nro,
                i.Estado,
                i.Archivos
                    .Where(a => a.Habilitada)
                    .OrderBy(a => a.FechaCarga)
                    .Select(a => new ArchivoReadModel(
                        a.IdArchivoIE,
                        a.TipoCalificacion,
                        a.TipoIE,
                        a.Titulo,
                        a.NombreArchivo,
                        a.UrlArchivo,
                        a.FechaEjecucion,
                        a.FechaCarga,
                        a.Habilitada,
                        a.Calificaciones.Any(c => c.Habilitada && c.Puntaje.HasValue),
                        a.IdArchivoIEAnterior))
                    .ToList()))
            .FirstOrDefaultAsync(ct);

        if (instancia is null)
        {
            return new InstanciaEvaluativaSlotDto { Existe = false, Estado = "SinCarga" };
        }

        var bloqueIds = await LoadBloquesPorArchivoAsync(new[] { instancia }, ct);
        return MapSlotDto(instancia.Nro, instancia, bloqueIds);
    }

    private static ArchivoIETrazadoDto? MapArchivoDto(
        List<ArchivoReadModel> archivos,
        TipoCalificacion tipo,
        IReadOnlyDictionary<Guid, List<Guid>> bloqueIdsPorArchivo)
    {
        var archivo = archivos.FirstOrDefault(a => a.TipoCalificacion == tipo);
        if (archivo is null)
        {
            return null;
        }

        var tieneDependencias = tipo switch
        {
            TipoCalificacion.NotaOriginal => archivos.Any(a => a.TipoCalificacion is TipoCalificacion.Recuperatorio1 or TipoCalificacion.Recuperatorio2),
            TipoCalificacion.Recuperatorio1 => archivos.Any(a => a.TipoCalificacion == TipoCalificacion.Recuperatorio2),
            _ => false,
        };

        var motivo = archivo.TieneCalificaciones
            ? "Este archivo tiene calificaciones vinculadas y no se puede modificar ni eliminar."
            : tieneDependencias
                ? tipo == TipoCalificacion.NotaOriginal
                    ? "No se puede modificar la Nota Original porque ya existen recuperatorios cargados para esta IE."
                    : "No se puede modificar el Recuperatorio 1 porque ya existe un Recuperatorio 2 cargado para esta IE."
            : null;

        return new ArchivoIETrazadoDto
        {
            IdArchivoIE = archivo.IdArchivoIE,
            TipoCalificacion = ToTipoCalificacionCode(archivo.TipoCalificacion),
            TipoIE = archivo.TipoIE.ToString(),
            Titulo = archivo.Titulo,
            NombreArchivo = archivo.NombreArchivo,
            UrlArchivo = archivo.UrlArchivo,
            FechaEjecucion = archivo.FechaEjecucion,
            FechaCarga = archivo.FechaCarga,
            TieneCalificaciones = archivo.TieneCalificaciones,
            PuedeEditar = !archivo.TieneCalificaciones && !tieneDependencias,
            PuedeEliminar = !archivo.TieneCalificaciones && !tieneDependencias,
            MotivoBloqueo = motivo,
            IdBloquesTema = bloqueIdsPorArchivo.TryGetValue(archivo.IdArchivoIE, out var bloques) ? bloques : new List<Guid>(),
        };
    }

    private static void VincularTrazabilidad(ArchivoIE archivo, List<Guid> bloqueIds)
    {
        foreach (var idBloque in bloqueIds.Distinct())
        {
            archivo.BloquesPrograma.Add(new ArchivoIEBloquePrograma
            {
                IdArchivoIE = archivo.IdArchivoIE,
                IdBloquePrograma = idBloque,
            });
        }
    }

    private static void ReemplazarTrazabilidad(ArchivoIE archivo, List<Guid> bloqueIds)
    {
        archivo.BloquesPrograma.Clear();
        VincularTrazabilidad(archivo, bloqueIds);
    }

    private static ArchivoIE CrearArchivo(
        Guid idIE,
        TipoCalificacion tipo,
        TipoIE tipoIE,
        GuardarArchivoIEFormDto dto,
        string urlArchivo,
        Guid idUsuario,
        DateTime now,
        Guid? idAnterior)
    {
        return new ArchivoIE
        {
            IdArchivoIE = Guid.NewGuid(),
            IdIE = idIE,
            TipoCalificacion = tipo,
            TipoIE = tipoIE,
            Titulo = dto.Titulo.Trim(),
            NombreArchivo = dto.Archivo?.FileName ?? $"archivo-{Guid.NewGuid():N}.pdf",
            UrlArchivo = urlArchivo,
            FechaEjecucion = ParseFecha(dto.FechaEjecucion),
            FechaCarga = now,
            FechaCreacion = now,
            FechaModificacion = now,
            IdUsuarioCarga = idUsuario,
            Habilitada = true,
            IdArchivoIEAnterior = idAnterior,
        };
    }

    private static Task<List<Guid>> ResolverBloquesAsync(TrazabilidadContext trazabilidad, List<Guid> bloqueIdsTema)
    {
        if (!bloqueIdsTema.Any())
        {
            return Task.FromResult(new List<Guid>());
        }

        if (!trazabilidad.Disponible || trazabilidad.Programa is null)
        {
            throw new InvalidOperationException(trazabilidad.Mensaje ?? "No se puede asociar trazabilidad sin un programa estructurado.");
        }

        var bloqueIdsValidos = trazabilidad.Unidades
            .SelectMany(u => u.Temas)
            .Select(t => t.IdBloquePrograma)
            .ToHashSet();

        var bloqueIdsSolicitados = bloqueIdsTema.Distinct().ToList();
        if (!bloqueIdsSolicitados.All(bloqueIdsValidos.Contains))
        {
            throw new InvalidOperationException("Uno o más temas seleccionados no pertenecen al programa vigente.");
        }

        return Task.FromResult(bloqueIdsSolicitados);
    }

    private static void ValidarDependenciasParaReemplazo(List<ArchivoIE> activos, TipoCalificacion tipo)
    {
        var tieneR1 = activos.Any(a => a.TipoCalificacion == TipoCalificacion.Recuperatorio1);
        var tieneR2 = activos.Any(a => a.TipoCalificacion == TipoCalificacion.Recuperatorio2);

        if (tipo == TipoCalificacion.NotaOriginal && (tieneR1 || tieneR2))
        {
            throw new InvalidOperationException("No se puede modificar la Nota Original porque ya existen recuperatorios cargados para esta IE.");
        }

        if (tipo == TipoCalificacion.Recuperatorio1 && tieneR2)
        {
            throw new InvalidOperationException("No se puede modificar el Recuperatorio 1 porque ya existe un Recuperatorio 2 cargado para esta IE.");
        }
    }

    private static void ValidarDependenciasParaCarga(List<ArchivoIE> activos, TipoCalificacion tipo)
    {
        var tieneN = activos.Any(a => a.TipoCalificacion == TipoCalificacion.NotaOriginal);
        var tieneR1 = activos.Any(a => a.TipoCalificacion == TipoCalificacion.Recuperatorio1);

        if (tipo == TipoCalificacion.Recuperatorio1 && !tieneN)
        {
            throw new InvalidOperationException("Primero tenés que cargar la Nota Original para poder cargar el Recuperatorio 1.");
        }

        if (tipo == TipoCalificacion.Recuperatorio2)
        {
            if (!tieneN)
            {
                throw new InvalidOperationException("Primero tenés que cargar la Nota Original para poder cargar el Recuperatorio 2.");
            }

            if (!tieneR1)
            {
                throw new InvalidOperationException("Primero tenés que cargar el Recuperatorio 1 para poder cargar el Recuperatorio 2.");
            }
        }
    }

    private static bool TieneNotasVigentes(ArchivoIE archivo)
    {
        return archivo.Calificaciones.Any(c => c.Habilitada && c.Puntaje.HasValue);
    }

    private static TipoCalificacion ParseTipoCalificacion(string tipoCalificacion) => tipoCalificacion switch
    {
        "N" => TipoCalificacion.NotaOriginal,
        "R1" => TipoCalificacion.Recuperatorio1,
        "R2" => TipoCalificacion.Recuperatorio2,
        _ => throw new InvalidOperationException($"El tipo de calificación '{tipoCalificacion}' no es válido."),
    };

    private static TipoIE ParseTipoIE(string tipoIE) => Enum.TryParse<TipoIE>(tipoIE, true, out var parsed)
        ? parsed
        : throw new InvalidOperationException($"El tipo de IE '{tipoIE}' no es válido.");

    private static EstadoInstanciaEvaluativa ParseEstado(string estado) => Enum.TryParse<EstadoInstanciaEvaluativa>(estado, true, out var parsed)
        ? parsed
        : throw new InvalidOperationException($"El estado de IE '{estado}' no es válido.");

    private static DateTime ParseFecha(string fecha)
    {
        if (!DateTime.TryParseExact(
            fecha,
            "yyyy-MM-dd",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None,
            out var parsed))
        {
            throw new InvalidOperationException($"La fecha '{fecha}' no tiene un formato válido (yyyy-MM-dd).");
        }

        return DateTime.SpecifyKind(parsed.Date, DateTimeKind.Utc);
    }

    private static string ToTipoCalificacionCode(TipoCalificacion tipo) => tipo switch
    {
        TipoCalificacion.NotaOriginal => "N",
        TipoCalificacion.Recuperatorio1 => "R1",
        TipoCalificacion.Recuperatorio2 => "R2",
        _ => tipo.ToString(),
    };

    private sealed record EspacioContext(
        Guid IdEC,
        Guid IdCurso,
        Guid? IdDocente,
        string NombreMateria,
        string NombreDocente,
        string NombreCurso,
        int AnioLectivo);

    private sealed record TrazabilidadContext(
        bool Disponible,
        string? Mensaje,
        Programa? Programa,
        List<UnidadArbolDto> Unidades)
    {
        public static TrazabilidadContext SinPrograma(string mensaje) => new(false, mensaje, null, new List<UnidadArbolDto>());

        public static TrazabilidadContext SinTrazabilidad(Programa programa, string mensaje) =>
            new(false, mensaje, programa, new List<UnidadArbolDto>());

        public static TrazabilidadContext ConTrazabilidad(Programa programa, List<UnidadArbolDto> unidades) =>
            new(true, null, programa, unidades);
    }

    private sealed record InstanciaReadModel(
        Guid IdIE,
        Guid IdEC,
        int Nro,
        EstadoInstanciaEvaluativa Estado,
        List<ArchivoReadModel> Archivos);

    private sealed record ArchivoReadModel(
        Guid IdArchivoIE,
        TipoCalificacion TipoCalificacion,
        TipoIE TipoIE,
        string Titulo,
        string NombreArchivo,
        string UrlArchivo,
        DateTime FechaEjecucion,
        DateTime FechaCarga,
        bool Habilitada,
        bool TieneCalificaciones,
        Guid? IdArchivoIEAnterior);
}
