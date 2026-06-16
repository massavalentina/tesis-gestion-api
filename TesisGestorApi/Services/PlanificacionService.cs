using Microsoft.EntityFrameworkCore;
using TesisGestorApi.Data;
using TesisGestorApi.DTOs.Planificaciones;
using TesisGestorApi.Entities;
using TesisGestorApi.Interfaces;

namespace TesisGestorApi.Services;

public class PlanificacionService : IPlanificacionService
{
    private readonly ApplicationDbContext _db;

    public PlanificacionService(ApplicationDbContext db)
    {
        _db = db;
    }

    // ─── GET árbol ────────────────────────────────────────────────────────────

    public async Task<ArbolPlanificacionDto> GetArbolAsync(Guid idEC, Guid idDocente, CancellationToken ct)
    {
        // Hardcodeado: un docente solo puede ver/planificar los EC donde tiene clases asignadas.
        await ValidarDocenteDelECAsync(idEC, idDocente, ct);

        var programa = await _db.Programas
            .Include(p => p.EspacioCurricular)
                .ThenInclude(ec => ec.Curricula)
            .Where(p => p.IdEC == idEC && p.Estado == EstadoPrograma.Vigente)
            .FirstOrDefaultAsync(ct);

        if (programa is null)
            return new ArbolPlanificacionDto { SinPrograma = true };

        if (programa.Origen == OrigenPrograma.Archivo)
            return new ArbolPlanificacionDto
            {
                Bloqueado      = true,
                MensajeBloqueo = "El programa fue cargado como archivo PDF. Para planificar clases, el programa debe tener unidades y temas cargados de forma estructurada.",
            };

        var bloques = await _db.BloquesProgramas
            .Where(b => b.IdPrograma == programa.IdPrograma)
            .Include(b => b.Unidad)
            .Include(b => b.Tema)
            .Include(b => b.ClasesBloquePrograma)
                .ThenInclude(cb => cb.Planificacion)
            .ToListAsync(ct);

        // Auto-sync: programas Manual creados antes del fix no tienen BloquePrograma entries.
        if (bloques.Count == 0)
        {
            bloques = await SincronizarBloquesDesdeEstructuraAsync(programa.IdPrograma, ct);
        }

        var bloquesUnidad = bloques
            .Where(b => b.Tipo == TipoBloquePrograma.Unidad)
            .OrderBy(b => b.Unidad.Nro)
            .ToList();

        var bloquesTemaPorUnidad = bloques
            .Where(b => b.Tipo == TipoBloquePrograma.Tema)
            .GroupBy(b => b.IdUnidad)
            .ToDictionary(g => g.Key, g => g.OrderBy(b => b.Tema!.Nro).ToList());

        var unidadesDto = bloquesUnidad.Select(bu =>
        {
            var temasBloques = bloquesTemaPorUnidad.TryGetValue(bu.IdUnidad, out var ts) ? ts : new();
            var estadoUnidad = temasBloques.Any() && temasBloques.All(b => b.Estado == EstadoBloque.Dado)
                ? EstadoBloque.Dado : EstadoBloque.PendienteDar;
            return new UnidadArbolDto
            {
                IdUnidad         = bu.IdUnidad,
                IdBloquePrograma = bu.IdBloquePrograma,
                Titulo           = bu.Unidad.Titulo,
                Descripcion      = bu.Unidad.Descripcion,
                Nro              = bu.Unidad.Nro,
                Estado           = estadoUnidad.ToString(),
                Temas            = temasBloques.Select(bt => MapTema(bt, idDocente)).ToList(),
            };
        }).ToList();

        var todosLosTemasBloque = bloques.Where(b => b.Tipo == TipoBloquePrograma.Tema).ToList();
        (double avance, int totalTemas, int temasCompletos) = CalcularAvance(todosLosTemasBloque);

        return new ArbolPlanificacionDto
        {
            PermiteCrearItems = programa.Origen == OrigenPrograma.Archivo,
            NombreMateria     = programa.EspacioCurricular.Curricula?.Nombre ?? programa.EspacioCurricular.IdCurricula.ToString(),
            TituloPrograma    = programa.Titulo,
            AnioLectivo       = programa.AnioLectivo,
            EstadoPrograma    = programa.Estado.ToString(),
            UrlPrograma       = programa.Url,
            Avance            = avance,
            TotalTemas        = totalTemas,
            TemasCompletos    = temasCompletos,
            Unidades          = unidadesDto,
        };
    }

    private static TemaArbolDto MapTema(BloquePrograma bt, Guid idDocente)
    {
        var clases = bt.ClasesBloquePrograma
            .Select(cb => cb.Planificacion)
            .Where(p => p.IdDocente == idDocente)
            .OrderBy(p => p.FechaCreacion)
            .Select(MapClase)
            .ToList();

        return new TemaArbolDto
        {
            IdTema           = bt.IdTema!.Value,
            IdBloquePrograma = bt.IdBloquePrograma,
            Titulo           = bt.Tema!.Titulo,
            Descripcion      = bt.Tema.Descripcion,
            Nro              = bt.Tema.Nro,
            Estado           = bt.Estado.ToString(),
            Clases           = clases,
        };
    }

    private static ClasePlanificacionDto MapClase(Planificacion p) => new()
    {
        IdPlanificacion = p.IdPlanificacion,
        Titulo          = p.Titulo,
        Descripcion     = p.Descripcion,
        FechaEstimada   = p.FechaDesde?.ToString("yyyy-MM-dd"),
        FechaDictada    = p.FechaHasta?.ToString("yyyy-MM-dd"),
        Estado          = p.Estado.ToString(),
        Url             = p.Url,
        FechaCreacion   = p.FechaCreacion,
    };

    // Avance basado en temas marcados como Dado manualmente (no en el estado de las clases).
    private static (double avance, int total, int completos) CalcularAvance(List<BloquePrograma> temasBloques)
    {
        int n = temasBloques.Count;
        if (n == 0) return (0, 0, 0);
        int completos = temasBloques.Count(b => b.Estado == EstadoBloque.Dado);
        return (Math.Round((double)completos / n * 100, 1), n, completos);
    }

    // ─── Crear unidad ─────────────────────────────────────────────────────────

    public async Task<UnidadArbolDto> CrearUnidadAsync(
        Guid idEC, Guid idDocente, CrearItemArchivoDto dto, CancellationToken ct)
    {
        var programa = await ObtenerProgramaVigenteAsync(idEC, idDocente, ct);
        ValidarOrigen(programa);

        int nro = await _db.Unidades.CountAsync(u => u.IdPrograma == programa.IdPrograma, ct) + 1;

        var unidad = new Unidad
        {
            IdUnidad   = Guid.NewGuid(),
            IdPrograma = programa.IdPrograma,
            Titulo     = dto.Titulo,
            Descripcion = dto.Descripcion,
            Nro        = nro,
        };
        _db.Unidades.Add(unidad);

        var bloque = new BloquePrograma
        {
            IdBloquePrograma = Guid.NewGuid(),
            IdPrograma       = programa.IdPrograma,
            IdUnidad         = unidad.IdUnidad,
            IdTema           = null,
            Tipo             = TipoBloquePrograma.Unidad,
            Estado           = EstadoBloque.PendienteDar,
        };
        _db.BloquesProgramas.Add(bloque);

        await _db.SaveChangesAsync(ct);

        return new UnidadArbolDto
        {
            IdUnidad         = unidad.IdUnidad,
            IdBloquePrograma = bloque.IdBloquePrograma,
            Titulo           = unidad.Titulo,
            Descripcion      = unidad.Descripcion,
            Nro              = unidad.Nro,
            Estado           = bloque.Estado.ToString(),
            Temas            = new(),
        };
    }

    // ─── Crear tema ───────────────────────────────────────────────────────────

    public async Task<TemaArbolDto> CrearTemaAsync(
        Guid idEC, Guid idUnidad, Guid idDocente, CrearItemArchivoDto dto, CancellationToken ct)
    {
        var programa = await ObtenerProgramaVigenteAsync(idEC, idDocente, ct);
        ValidarOrigen(programa);

        var unidad = await _db.Unidades
            .Where(u => u.IdUnidad == idUnidad && u.IdPrograma == programa.IdPrograma)
            .FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Unidad no encontrada en el programa vigente.");

        int nro = await _db.Temas.CountAsync(t => t.IdUnidad == idUnidad, ct) + 1;

        var tema = new Tema
        {
            IdTema      = Guid.NewGuid(),
            IdUnidad    = idUnidad,
            Titulo      = dto.Titulo,
            Descripcion = dto.Descripcion,
            Nro         = nro,
        };
        _db.Temas.Add(tema);

        var bloque = new BloquePrograma
        {
            IdBloquePrograma = Guid.NewGuid(),
            IdPrograma       = programa.IdPrograma,
            IdUnidad         = idUnidad,
            IdTema           = tema.IdTema,
            Tipo             = TipoBloquePrograma.Tema,
            Estado           = EstadoBloque.PendienteDar,
        };
        _db.BloquesProgramas.Add(bloque);

        await _db.SaveChangesAsync(ct);

        return new TemaArbolDto
        {
            IdTema           = tema.IdTema,
            IdBloquePrograma = bloque.IdBloquePrograma,
            Titulo           = tema.Titulo,
            Descripcion      = tema.Descripcion,
            Nro              = tema.Nro,
            Estado           = bloque.Estado.ToString(),
            Clases           = new(),
        };
    }

    // ─── Crear clase ──────────────────────────────────────────────────────────

    public async Task<ClasePlanificacionDto> CrearClaseAsync(
        Guid idEC, Guid idDocente, CrearClaseDto dto, string? urlArchivo, CancellationToken ct)
    {
        await ObtenerProgramaVigenteAsync(idEC, idDocente, ct); // valida vigente + pertenencia al docente

        var estado = ParseEstadoBloque(dto.Estado);

        var planificacion = new Planificacion
        {
            IdPlanificacion = Guid.NewGuid(),
            IdDocente       = idDocente,
            Titulo          = dto.Titulo,
            Descripcion     = dto.Descripcion,
            FechaDesde      = ParseFecha(dto.FechaDesde),
            FechaHasta      = ParseFecha(dto.FechaHasta),
            Estado          = estado,
            Url             = urlArchivo,
            FechaCreacion   = DateTime.UtcNow,
        };
        _db.Planificaciones.Add(planificacion);

        if (dto.IdBloqueTema.HasValue)
        {
            var bloque = await _db.BloquesProgramas.FindAsync(new object[] { dto.IdBloqueTema.Value }, ct)
                ?? throw new KeyNotFoundException("Bloque de tema no encontrado.");

            _db.ClasesBloquesProgramas.Add(new ClaseBloquePrograma
            {
                IdClasePlanificacion = planificacion.IdPlanificacion,
                IdBloquePrograma     = bloque.IdBloquePrograma,
            });
        }

        await _db.SaveChangesAsync(ct);

        return MapClase(planificacion);
    }

    // ─── Editar clase ─────────────────────────────────────────────────────────

    public async Task<ClasePlanificacionDto> EditarClaseAsync(
        Guid idClase, Guid idDocente, EditarClaseDto dto, string? urlArchivo, CancellationToken ct)
    {
        var planificacion = await _db.Planificaciones
            .Include(p => p.ClasesBloquePrograma)
            .FirstOrDefaultAsync(p => p.IdPlanificacion == idClase, ct)
            ?? throw new KeyNotFoundException("Clase no encontrada.");

        if (planificacion.IdDocente != idDocente)
            throw new UnauthorizedAccessException("No tenés permiso para editar esta clase.");

        var bloquesAnteriores = planificacion.ClasesBloquePrograma
            .Select(cb => cb.IdBloquePrograma).ToList();

        planificacion.Titulo      = dto.Titulo;
        planificacion.Descripcion = dto.Descripcion;
        planificacion.FechaDesde  = ParseFecha(dto.FechaDesde);
        planificacion.FechaHasta  = ParseFecha(dto.FechaHasta);
        planificacion.Estado      = ParseEstadoBloque(dto.Estado);

        if (urlArchivo != null)
            planificacion.Url = urlArchivo;
        else if (!dto.MantieneArchivo)
            planificacion.Url = null;

        // Actualizar vínculo a tema si cambió
        if (dto.IdBloqueTema.HasValue)
        {
            var nuevoIdBloque = dto.IdBloqueTema.Value;
            var existeVinculo = planificacion.ClasesBloquePrograma
                .Any(cb => cb.IdBloquePrograma == nuevoIdBloque);

            if (!existeVinculo)
            {
                // Quitar todos los vínculos anteriores y crear el nuevo
                _db.ClasesBloquesProgramas.RemoveRange(planificacion.ClasesBloquePrograma);
                _db.ClasesBloquesProgramas.Add(new ClaseBloquePrograma
                {
                    IdClasePlanificacion = idClase,
                    IdBloquePrograma     = nuevoIdBloque,
                });
            }
        }

        await _db.SaveChangesAsync(ct);

        return MapClase(planificacion);
    }

    // ─── Cambiar estado clase ─────────────────────────────────────────────────

    public async Task CambiarEstadoClaseAsync(
        Guid idClase, Guid idDocente, string nuevoEstado, CancellationToken ct)
    {
        var planificacion = await _db.Planificaciones.FindAsync(new object[] { idClase }, ct)
            ?? throw new KeyNotFoundException("Clase no encontrada.");

        if (planificacion.IdDocente != idDocente)
            throw new UnauthorizedAccessException("No tenés permiso para modificar esta clase.");

        planificacion.Estado = ParseEstadoBloque(nuevoEstado);
        await _db.SaveChangesAsync(ct);
    }

    // ─── Eliminar clase ───────────────────────────────────────────────────────

    public async Task EliminarClaseAsync(Guid idClase, Guid idDocente, CancellationToken ct)
    {
        var planificacion = await _db.Planificaciones
            .Include(p => p.ClasesBloquePrograma)
            .FirstOrDefaultAsync(p => p.IdPlanificacion == idClase, ct)
            ?? throw new KeyNotFoundException("Clase no encontrada.");

        if (planificacion.IdDocente != idDocente)
            throw new UnauthorizedAccessException("No tenés permiso para eliminar esta clase.");

        _db.Planificaciones.Remove(planificacion); // cascade elimina ClaseBloquePrograma
        await _db.SaveChangesAsync(ct);
    }

    // ─── Marcar tema como Dado / Pendiente (manual) ──────────────────────────

    public async Task CambiarEstadoBloqueAsync(Guid idBloque, Guid idDocente, string nuevoEstado, CancellationToken ct)
    {
        var bloque = await _db.BloquesProgramas
            .Include(b => b.Programa)
            .FirstOrDefaultAsync(b => b.IdBloquePrograma == idBloque, ct)
            ?? throw new KeyNotFoundException("Bloque no encontrado.");

        if (bloque.Tipo != TipoBloquePrograma.Tema)
            throw new InvalidOperationException("Solo se puede marcar como dado un bloque de tipo Tema.");

        if (bloque.Programa.IdDocente != idDocente)
            throw new UnauthorizedAccessException("No tenés permiso para modificar este bloque.");

        bloque.Estado = nuevoEstado == "Dado" ? EstadoBloque.Dado : EstadoBloque.PendienteDar;
        await _db.SaveChangesAsync(ct);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    // Crea BloquePrograma para programas Manual que carecen de ellos (datos históricos).
    private async Task<List<BloquePrograma>> SincronizarBloquesDesdeEstructuraAsync(Guid idPrograma, CancellationToken ct)
    {
        var unidades = await _db.Unidades
            .Where(u => u.IdPrograma == idPrograma)
            .Include(u => u.Temas)
            .OrderBy(u => u.Nro)
            .ToListAsync(ct);

        foreach (var unidad in unidades)
        {
            _db.BloquesProgramas.Add(new BloquePrograma
            {
                IdBloquePrograma = Guid.NewGuid(),
                IdPrograma       = idPrograma,
                IdUnidad         = unidad.IdUnidad,
                Tipo             = TipoBloquePrograma.Unidad,
                Estado           = EstadoBloque.PendienteDar,
            });

            foreach (var tema in unidad.Temas.OrderBy(t => t.Nro))
            {
                _db.BloquesProgramas.Add(new BloquePrograma
                {
                    IdBloquePrograma = Guid.NewGuid(),
                    IdPrograma       = idPrograma,
                    IdUnidad         = unidad.IdUnidad,
                    IdTema           = tema.IdTema,
                    Tipo             = TipoBloquePrograma.Tema,
                    Estado           = EstadoBloque.PendienteDar,
                });
            }
        }

        if (unidades.Count > 0)
            await _db.SaveChangesAsync(ct);

        return await _db.BloquesProgramas
            .Where(b => b.IdPrograma == idPrograma)
            .Include(b => b.Unidad)
            .Include(b => b.Tema)
            .Include(b => b.ClasesBloquePrograma)
                .ThenInclude(cb => cb.Planificacion)
            .ToListAsync(ct);
    }

    private async Task<Programa> ObtenerProgramaVigenteAsync(Guid idEC, Guid idDocente, CancellationToken ct)
    {
        // Hardcodeado: un docente solo puede ver/planificar los EC donde tiene clases asignadas.
        await ValidarDocenteDelECAsync(idEC, idDocente, ct);

        var programa = await _db.Programas
            .Include(p => p.EspacioCurricular)
                .ThenInclude(ec => ec.Curricula)
            .Where(p => p.IdEC == idEC && p.Estado == EstadoPrograma.Vigente)
            .FirstOrDefaultAsync(ct);

        if (programa is null)
            throw new KeyNotFoundException("No hay programa vigente para este espacio curricular.");

        return programa;
    }

    // Hardcodeado: filtro explícito por IdDocente vía la asignación EspacioCurricular.IdDocente
    // (en sync con DocenteEspacioCurricular). No depende de la tabla de permisos genérica.
    private async Task ValidarDocenteDelECAsync(Guid idEC, Guid idDocente, CancellationToken ct)
    {
        var ec = await _db.EspaciosCurriculares
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.IdEC == idEC, ct)
            ?? throw new KeyNotFoundException("Espacio curricular no encontrado.");

        if (ec.IdDocente != idDocente)
            throw new UnauthorizedAccessException("No sos el docente titular de este espacio curricular.");
    }

    private static void ValidarOrigen(Programa programa)
    {
        if (programa.Origen != OrigenPrograma.Archivo)
            throw new InvalidOperationException(
                "Solo se pueden crear unidades y temas en programas cargados por archivo.");
    }

    private static EstadoBloque ParseEstadoBloque(string estado) => estado switch
    {
        "Dado"            => EstadoBloque.Dado,
        "PendienteDar"    => EstadoBloque.PendienteDar,
        "PendienteEvaluar"=> EstadoBloque.PendienteEvaluar,
        "Evaluado"        => EstadoBloque.Evaluado,
        _                 => throw new InvalidOperationException($"Estado desconocido: '{estado}'."),
    };

    private static DateOnly? ParseFecha(string? fecha)
    {
        if (string.IsNullOrWhiteSpace(fecha)) return null;
        return DateOnly.TryParseExact(fecha, "yyyy-MM-dd", out var d) ? d : null;
    }
}
