using Microsoft.EntityFrameworkCore;
using RepoDB.Entities;
using TesisGestorApi.Data;
using TesisGestorApi.DTOs;
using TesisGestorApi.Dtos;
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

            if (asistenciasParaProcesar.Any())
                await ProcesarAsistenciaEspacios(asistenciasParaProcesar);

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
            var asistencias = await _context.Asistencias
                .Where(a => a.Fecha == clase.Fecha)
                .ToListAsync();

            await ProcesarAsistenciaEspacios(asistencias);
        }

        // =========================================
        // METODOS SIN IMPLEMENTAR
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
