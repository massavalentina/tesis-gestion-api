using Microsoft.EntityFrameworkCore;
using TesisGestorApi.Data;
using TesisGestorApi.DTOs.Programas;
using TesisGestorApi.Entities;
using TesisGestorApi.Interfaces;

namespace TesisGestorApi.Services
{
    public class ProgramaService : IProgramaService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ProgramaService> _logger;

        public ProgramaService(ApplicationDbContext context, ILogger<ProgramaService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<ProgramaResumenDto>> GetProgramasPorECAsync(Guid idEC, Guid idDocente, CancellationToken ct)
        {
            // Hardcodeado: un docente solo puede ver los programas de los EC donde tiene clases asignadas.
            // No depende de la tabla de permisos genérica.
            var ec = await _context.EspaciosCurriculares
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.IdEC == idEC, ct)
                ?? throw new KeyNotFoundException("Espacio curricular no encontrado.");

            if (ec.IdDocente != idDocente)
                throw new UnauthorizedAccessException("No sos el docente titular de este espacio curricular.");

            return await _context.Programas
                .AsNoTracking()
                .Where(p => p.IdEC == idEC)
                .Include(p => p.Curso).ThenInclude(c => c.Anio)
                .Include(p => p.Curso).ThenInclude(c => c.Division)
                .Include(p => p.EspacioCurricular).ThenInclude(ec => ec.Curricula)
                .Include(p => p.Objetivos)
                .Include(p => p.Unidades).ThenInclude(u => u.Temas)
                .OrderByDescending(p => p.AnioLectivo)
                .ThenByDescending(p => p.FechaCreacion)
                .Select(p => new ProgramaResumenDto
                {
                    IdPrograma = p.IdPrograma,
                    Titulo = p.Titulo,
                    AnioLectivo = p.AnioLectivo,
                    Estado = p.Estado.ToString(),
                    Origen = p.Origen.ToString(),
                    NombreMateria = p.EspacioCurricular.Curricula.Nombre,
                    CodigoCurso = p.Curso.Codigo,
                    HorasCatedra = p.HorasCatedra,
                    FechaCreacion = p.FechaCreacion,
                    CantidadUnidades = p.Unidades.Count,
                    CantidadTemas = p.Unidades.SelectMany(u => u.Temas).Count(),
                    CantidadObjetivos = p.Objetivos.Count,
                    NombreDocente = $"{p.Docente.Usuario.Nombre} {p.Docente.Usuario.Apellido}",
                })
                .ToListAsync(ct);
        }

        public async Task<ProgramaDetalleDto> GetProgramaAsync(Guid idPrograma, Guid idDocente, CancellationToken ct)
        {
            var p = await _context.Programas
                .AsNoTracking()
                .Include(p => p.Docente).ThenInclude(d => d.Usuario)
                .Include(p => p.Curso).ThenInclude(c => c.Anio)
                .Include(p => p.Curso).ThenInclude(c => c.Division)
                .Include(p => p.EspacioCurricular).ThenInclude(ec => ec.Curricula)
                .Include(p => p.Objetivos)
                .Include(p => p.Unidades).ThenInclude(u => u.Temas)
                .FirstOrDefaultAsync(p => p.IdPrograma == idPrograma, ct)
                ?? throw new KeyNotFoundException("Programa no encontrado.");

            // Hardcodeado: solo el docente titular del EC del programa puede verlo.
            if (p.IdDocente != idDocente)
                throw new UnauthorizedAccessException("No sos el docente titular de este programa.");

            return MapToDetalle(p);
        }

        public async Task<ProgramaDetalleDto> CrearProgramaAsync(Guid idDocente, CrearProgramaDto dto, CancellationToken ct)
        {
            // Validar que el docente sea titular del EC
            var ec = await _context.EspaciosCurriculares
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.IdEC == dto.IdEC, ct)
                ?? throw new KeyNotFoundException("Espacio curricular no encontrado.");

            if (ec.IdDocente != idDocente)
                throw new UnauthorizedAccessException("No sos el docente titular de este espacio curricular.");

            // CDA016: validar que no exista programa para (IdEC, AnioLectivo)
            var existe = await _context.Programas
                .AnyAsync(p => p.IdEC == dto.IdEC && p.AnioLectivo == dto.AnioLectivo, ct);

            if (existe)
                throw new InvalidOperationException($"Ya existe un programa para este espacio curricular en el año lectivo {dto.AnioLectivo}.");

            var ahora = DateTime.UtcNow;
            var programa = new Programa
            {
                IdPrograma = Guid.NewGuid(),
                IdDocente = idDocente,
                IdCurso = dto.IdCurso,
                IdEC = dto.IdEC,
                AnioLectivo = dto.AnioLectivo,
                Titulo = dto.Titulo.Trim(),
                Descripcion = dto.Descripcion?.Trim(),
                HorasCatedra = dto.HorasCatedra,
                Origen = OrigenPrograma.Manual,
                Estado = EstadoPrograma.Borrador,
                FechaVencimiento = new DateOnly(dto.AnioLectivo, 12, 31),
                FechaCreacion = ahora,
                FechaUltimaModificacion = ahora,
            };

            // Objetivos
            foreach (var obj in dto.Objetivos)
            {
                programa.Objetivos.Add(new ObjetivoPrograma
                {
                    IdObjetivo = Guid.NewGuid(),
                    Descripcion = obj.Descripcion.Trim(),
                    Nro = obj.Nro,
                });
            }

            // Unidades + Temas + BloquePrograma (uno por unidad, uno por tema)
            foreach (var uDto in dto.Unidades)
            {
                var idUnidad = Guid.NewGuid();
                var unidad = new Unidad
                {
                    IdUnidad    = idUnidad,
                    Titulo      = uDto.Titulo.Trim(),
                    Descripcion = uDto.Descripcion?.Trim(),
                    Nro         = uDto.Nro,
                };

                foreach (var tDto in uDto.Temas)
                {
                    var idTema = Guid.NewGuid();
                    unidad.Temas.Add(new Tema
                    {
                        IdTema      = idTema,
                        Titulo      = tDto.Titulo.Trim(),
                        Descripcion = tDto.Descripcion?.Trim(),
                        Nro         = tDto.Nro,
                    });
                    _context.BloquesProgramas.Add(new BloquePrograma
                    {
                        IdBloquePrograma = Guid.NewGuid(),
                        IdPrograma       = programa.IdPrograma,
                        IdUnidad         = idUnidad,
                        IdTema           = idTema,
                        Tipo             = TipoBloquePrograma.Tema,
                        Estado           = EstadoBloque.PendienteDar,
                    });
                }

                programa.Unidades.Add(unidad);
                _context.BloquesProgramas.Add(new BloquePrograma
                {
                    IdBloquePrograma = Guid.NewGuid(),
                    IdPrograma       = programa.IdPrograma,
                    IdUnidad         = idUnidad,
                    Tipo             = TipoBloquePrograma.Unidad,
                    Estado           = EstadoBloque.PendienteDar,
                });
            }

            _context.Programas.Add(programa);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("Programa {Id} creado para EC {IdEC}, año {Anio}.",
                programa.IdPrograma, dto.IdEC, dto.AnioLectivo);

            return await GetProgramaAsync(programa.IdPrograma, idDocente, ct);
        }

        public async Task<ProgramaDetalleDto> ActualizarProgramaAsync(Guid idPrograma, Guid idDocente, CrearProgramaDto dto, CancellationToken ct)
        {
            var programa = await _context.Programas
                .Include(p => p.Objetivos)
                .Include(p => p.Unidades).ThenInclude(u => u.Temas)
                .FirstOrDefaultAsync(p => p.IdPrograma == idPrograma, ct)
                ?? throw new KeyNotFoundException("Programa no encontrado.");

            if (programa.IdDocente != idDocente)
                throw new UnauthorizedAccessException("No sos el docente titular de este programa.");

            // CDA008/CDA012: solo editable si es Borrador
            if (programa.Estado != EstadoPrograma.Borrador)
                throw new InvalidOperationException("Solo se pueden editar programas en estado Borrador.");

            // CDA016: si cambió EC o año, validar unicidad
            if ((programa.IdEC != dto.IdEC || programa.AnioLectivo != dto.AnioLectivo) &&
                await _context.Programas.AnyAsync(p => p.IdEC == dto.IdEC && p.AnioLectivo == dto.AnioLectivo && p.IdPrograma != idPrograma, ct))
            {
                throw new InvalidOperationException($"Ya existe un programa para este espacio curricular en el año lectivo {dto.AnioLectivo}.");
            }

            // Actualizar campos
            programa.IdCurso = dto.IdCurso;
            programa.IdEC = dto.IdEC;
            programa.AnioLectivo = dto.AnioLectivo;
            programa.Titulo = dto.Titulo.Trim();
            programa.Descripcion = dto.Descripcion?.Trim();
            programa.HorasCatedra = dto.HorasCatedra;
            programa.FechaVencimiento = new DateOnly(dto.AnioLectivo, 12, 31);
            programa.FechaUltimaModificacion = DateTime.UtcNow;

            // Replace strategy: borrar hijos y recrear.
            // BloquePrograma tiene NoAction hacia Unidad/Tema, hay que eliminarlo antes que ellos.
            var viejosBloques = await _context.BloquesProgramas
                .Where(b => b.IdPrograma == idPrograma)
                .ToListAsync(ct);
            _context.BloquesProgramas.RemoveRange(viejosBloques);

            var viejosTemas = programa.Unidades.SelectMany(u => u.Temas).ToList();
            var viejasUnidades = programa.Unidades.ToList();
            var viejosObjetivos = programa.Objetivos.ToList();

            _context.Temas.RemoveRange(viejosTemas);
            _context.Unidades.RemoveRange(viejasUnidades);
            _context.ObjetivosPrograma.RemoveRange(viejosObjetivos);

            // Agregar directamente al contexto (no a las colecciones de nav trackeadas)
            // para evitar el DbUpdateConcurrencyException por mezcla de estados.
            foreach (var obj in dto.Objetivos)
            {
                _context.ObjetivosPrograma.Add(new ObjetivoPrograma
                {
                    IdObjetivo = Guid.NewGuid(),
                    IdPrograma = idPrograma,
                    Descripcion = obj.Descripcion.Trim(),
                    Nro = obj.Nro,
                });
            }

            foreach (var uDto in dto.Unidades)
            {
                var idUnidad = Guid.NewGuid();
                var unidad = new Unidad
                {
                    IdUnidad    = idUnidad,
                    IdPrograma  = idPrograma,
                    Titulo      = uDto.Titulo.Trim(),
                    Descripcion = uDto.Descripcion?.Trim(),
                    Nro         = uDto.Nro,
                };

                foreach (var tDto in uDto.Temas)
                {
                    var idTema = Guid.NewGuid();
                    unidad.Temas.Add(new Tema
                    {
                        IdTema      = idTema,
                        Titulo      = tDto.Titulo.Trim(),
                        Descripcion = tDto.Descripcion?.Trim(),
                        Nro         = tDto.Nro,
                    });
                    _context.BloquesProgramas.Add(new BloquePrograma
                    {
                        IdBloquePrograma = Guid.NewGuid(),
                        IdPrograma       = idPrograma,
                        IdUnidad         = idUnidad,
                        IdTema           = idTema,
                        Tipo             = TipoBloquePrograma.Tema,
                        Estado           = EstadoBloque.PendienteDar,
                    });
                }

                _context.Unidades.Add(unidad);
                _context.BloquesProgramas.Add(new BloquePrograma
                {
                    IdBloquePrograma = Guid.NewGuid(),
                    IdPrograma       = idPrograma,
                    IdUnidad         = idUnidad,
                    Tipo             = TipoBloquePrograma.Unidad,
                    Estado           = EstadoBloque.PendienteDar,
                });
            }

            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("Programa {Id} actualizado.", idPrograma);
            return await GetProgramaAsync(idPrograma, idDocente, ct);
        }

        public async Task CambiarEstadoAsync(Guid idPrograma, Guid idDocente, string nuevoEstado, CancellationToken ct)
        {
            var programa = await _context.Programas
                .Include(p => p.Objetivos)
                .Include(p => p.Unidades).ThenInclude(u => u.Temas)
                .FirstOrDefaultAsync(p => p.IdPrograma == idPrograma, ct)
                ?? throw new KeyNotFoundException("Programa no encontrado.");

            if (programa.IdDocente != idDocente)
                throw new UnauthorizedAccessException("No sos el docente titular de este programa.");

            if (!Enum.TryParse<EstadoPrograma>(nuevoEstado, out var estado))
                throw new InvalidOperationException($"El estado '{nuevoEstado}' no es válido.");

            switch (estado)
            {
                case EstadoPrograma.Confirmado:
                    if (programa.Estado == EstadoPrograma.Borrador)
                    {
                        // Borrador → Confirmado: validar contenido antes de confirmar
                        ValidarCamposObligatorios(programa);
                        programa.Estado = EstadoPrograma.Confirmado;
                    }
                    else if (programa.Estado == EstadoPrograma.Vigente)
                    {
                        // Vigente → Confirmado: revertir el programa activo
                        programa.Estado = EstadoPrograma.Confirmado;
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            $"Un programa en estado '{programa.Estado}' no puede pasar a Confirmado.");
                    }
                    break;

                case EstadoPrograma.Vigente:
                    if (programa.Estado != EstadoPrograma.Confirmado)
                        throw new InvalidOperationException(
                            "Solo un programa Confirmado puede ponerse como vigente.");

                    var anioActual = DateTime.UtcNow.Year;
                    if (programa.AnioLectivo != anioActual)
                        throw new InvalidOperationException(
                            $"Solo se puede activar como vigente un programa del año lectivo {anioActual}. " +
                            $"Este programa corresponde al año {programa.AnioLectivo}.");

                    // Los programas por archivo no tienen unidades ni objetivos; solo validar contenido estructurado
                    if (programa.Origen == OrigenPrograma.Manual)
                        ValidarCamposObligatorios(programa);

                    // El Vigente previo del mismo EC vuelve a Confirmado
                    var vigentes = await _context.Programas
                        .Where(p => p.IdEC == programa.IdEC && p.Estado == EstadoPrograma.Vigente && p.IdPrograma != idPrograma)
                        .ToListAsync(ct);

                    foreach (var v in vigentes)
                    {
                        v.Estado = EstadoPrograma.Confirmado;
                        v.FechaUltimaModificacion = DateTime.UtcNow;
                    }

                    programa.Estado = EstadoPrograma.Vigente;
                    break;

                case EstadoPrograma.NoVigente:
                    if (programa.Estado != EstadoPrograma.Confirmado && programa.Estado != EstadoPrograma.Vigente)
                        throw new InvalidOperationException(
                            $"Un programa en estado '{programa.Estado}' no puede darse de baja.");

                    programa.Estado = EstadoPrograma.NoVigente;
                    break;

                default:
                    throw new InvalidOperationException(
                        $"La transición al estado '{nuevoEstado}' no está permitida.");
            }

            programa.FechaUltimaModificacion = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("Programa {Id} cambió a estado {Estado}.", idPrograma, estado);
        }

        public async Task EliminarProgramaAsync(Guid idPrograma, Guid idDocente, CancellationToken ct)
        {
            var programa = await _context.Programas
                .FirstOrDefaultAsync(p => p.IdPrograma == idPrograma, ct)
                ?? throw new KeyNotFoundException("Programa no encontrado.");

            if (programa.IdDocente != idDocente)
                throw new UnauthorizedAccessException("No sos el docente titular de este programa.");

            if (programa.Estado == EstadoPrograma.Vigente)
                throw new InvalidOperationException(
                    "No se puede eliminar un programa Vigente. " +
                    "Primero revertilo a Confirmado o dalo de baja.");

            _context.Programas.Remove(programa);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("Programa {Id} eliminado (hard delete).", idPrograma);
        }

        private static void ValidarCamposObligatorios(Programa programa)
        {
            if (string.IsNullOrWhiteSpace(programa.Titulo))
                throw new InvalidOperationException("El programa debe tener un título.");
            if (programa.HorasCatedra < 1)
                throw new InvalidOperationException("El programa debe tener al menos 1 hora cátedra.");
            if (!programa.Objetivos.Any())
                throw new InvalidOperationException("El programa debe tener al menos un objetivo.");
            if (!programa.Unidades.Any())
                throw new InvalidOperationException("El programa debe tener al menos una unidad.");
            if (programa.Unidades.Any(u => !u.Temas.Any()))
                throw new InvalidOperationException("Todas las unidades deben tener al menos un tema.");
        }

        public async Task<ProgramaDetalleDto> CrearDesdeArchivoAsync(
            Guid idDocente, CargarProgramaArchivoDto dto, string urlArchivo, CancellationToken ct)
        {
            var ec = await _context.EspaciosCurriculares
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.IdEC == dto.IdEC, ct)
                ?? throw new KeyNotFoundException("Espacio curricular no encontrado.");

            if (ec.IdDocente != idDocente)
                throw new UnauthorizedAccessException("No sos el docente titular de este espacio curricular.");

            var existe = await _context.Programas
                .AnyAsync(p => p.IdEC == dto.IdEC && p.AnioLectivo == dto.AnioLectivo, ct);
            if (existe)
                throw new InvalidOperationException(
                    $"Ya existe un programa para este espacio curricular en el año lectivo {dto.AnioLectivo}.");

            var ahora = DateTime.UtcNow;
            var programa = new Programa
            {
                IdPrograma             = Guid.NewGuid(),
                IdDocente              = idDocente,
                IdCurso                = dto.IdCurso,
                IdEC                   = dto.IdEC,
                AnioLectivo            = dto.AnioLectivo,
                Titulo                 = dto.Titulo.Trim(),
                Descripcion            = dto.Descripcion?.Trim(),
                HorasCatedra           = dto.HorasCatedra,
                Url                    = urlArchivo,
                Origen                 = OrigenPrograma.Archivo,
                Estado                 = dto.AnioLectivo == DateTime.UtcNow.Year
                    ? EstadoPrograma.Confirmado
                    : EstadoPrograma.NoVigente,
                FechaVencimiento       = new DateOnly(dto.AnioLectivo, 12, 31),
                FechaCreacion          = ahora,
                FechaUltimaModificacion = ahora,
            };

            _context.Programas.Add(programa);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("Programa por archivo {Id} creado para EC {IdEC}, año {Anio}.",
                programa.IdPrograma, dto.IdEC, dto.AnioLectivo);

            return await GetProgramaAsync(programa.IdPrograma, idDocente, ct);
        }

        private static ProgramaDetalleDto MapToDetalle(Programa p)
        {
            return new ProgramaDetalleDto
            {
                IdPrograma = p.IdPrograma,
                IdCurso = p.IdCurso,
                IdEC = p.IdEC,
                AnioLectivo = p.AnioLectivo,
                Titulo = p.Titulo,
                Descripcion = p.Descripcion,
                HorasCatedra = p.HorasCatedra,
                Url = p.Url,
                Estado = p.Estado.ToString(),
                Origen = p.Origen.ToString(),
                FechaVencimiento = p.FechaVencimiento.ToString("dd/MM/yyyy"),
                FechaCreacion = p.FechaCreacion,
                FechaUltimaModificacion = p.FechaUltimaModificacion,
                NombreMateria = p.EspacioCurricular.Curricula.Nombre,
                CodigoCurso = p.Curso.Codigo,
                AnioNumero = p.Curso.Anio.Numero,
                Division = p.Curso.Division.Nombre.ToString(),
                NombreDocente = $"{p.Docente.Usuario.Nombre} {p.Docente.Usuario.Apellido}",
                Objetivos = p.Objetivos.OrderBy(o => o.Nro).Select(o => new ObjetivoDetalleDto
                {
                    IdObjetivo = o.IdObjetivo,
                    Descripcion = o.Descripcion,
                    Nro = o.Nro,
                }).ToList(),
                Unidades = p.Unidades.OrderBy(u => u.Nro).Select(u => new UnidadDetalleDto
                {
                    IdUnidad = u.IdUnidad,
                    Titulo = u.Titulo,
                    Descripcion = u.Descripcion,
                    Nro = u.Nro,
                    Temas = u.Temas.OrderBy(t => t.Nro).Select(t => new TemaDetalleDto
                    {
                        IdTema = t.IdTema,
                        Titulo = t.Titulo,
                        Descripcion = t.Descripcion,
                        Nro = t.Nro,
                    }).ToList(),
                }).ToList(),
            };
        }
    }
}
