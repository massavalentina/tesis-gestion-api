using Microsoft.EntityFrameworkCore;
using RepoDB.Entities;
using TesisGestorApi.Data;
using TesisGestorApi.DTOs;
using TesisGestorApi.Entities;

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
        // Método de escritura masiva por lote
        public async Task<int> RegistrarLoteAsync(List<RegistrarAsistenciaDto> lista)
        {
            // ✅ Normalizar fechas del request a UTC para que Npgsql no explote
            foreach (var x in lista)
            {
                x.Fecha = ToUtcDate(x.Fecha);
            }

            // Carga de tipos en memoria
            var idsTipos = lista.Select(x => x.TipoAsistenciaId).Distinct().ToList();
            var tiposDict = await _context.TiposAsistencia
                                          .Where(t => idsTipos.Contains(t.IdTipo))
                                          .ToDictionaryAsync(t => t.IdTipo);

            // Carga de asistencias existentes
            var idsEstudiantes = lista.Select(x => x.EstudianteId).Distinct().ToList();
            var fechas = lista.Select(x => x.Fecha).Distinct().ToList();

            var asistenciasExistentes = await _context.Asistencias
                .Include(a => a.TipoManiana)
                .Include(a => a.TipoTarde)
                .Where(a => idsEstudiantes.Contains(a.EstudianteId) &&
                            fechas.Contains(a.Fecha)) // ✅ comparar directo (Fecha guardada como UTC 00:00)
                .ToListAsync();

            int cont = 0;

            foreach (var dto in lista)
            {
                if (!tiposDict.TryGetValue(dto.TipoAsistenciaId, out var tipoEntidad))
                {
                    _logger.LogWarning($"TipoAsistencia {dto.TipoAsistenciaId} no encontrado.");
                    continue;
                }

                DateOnly fechaDto = dto.Fecha;
                var asistencia = asistenciasExistentes
                    .FirstOrDefault(a => a.EstudianteId == dto.EstudianteId && a.Fecha == fechaDto);

                if (asistencia == null)
                {
                    asistencia = new Asistencia
                    {
                        Id = Guid.NewGuid(),
                        EstudianteId = dto.EstudianteId,
                        Fecha = fechaDto,
                        ValorTotalInasistencia = 0
                    };
                    _context.Asistencias.Add(asistencia);
                    asistenciasExistentes.Add(asistencia);
                }
                string codigo = tipoEntidad.Codigo.ToUpper();
                var turno = dto.Turno?.Trim().ToUpper();

                bool esHorarioSalida = codigo.StartsWith("RA"); // Retiros Anticipados, Express y Extendidos
                bool esHorarioEntrada = codigo.StartsWith("LL") || codigo == "P"; 
                if (turno == "MANANA")
                {
                    asistencia.TipoManianaId = dto.TipoAsistenciaId;
                    asistencia.TipoManiana = tipoEntidad;
                    if (dto.Hora.HasValue)
                    {
                        if (esHorarioEntrada) asistencia.HoraEntradaManana = dto.Hora.Value;
                        else if (esHorarioSalida) asistencia.HoraSalidaManana = dto.Hora.Value;
                    }
                }
                else if (turno == "TARDE")
                {
                    asistencia.TipoTardeId = dto.TipoAsistenciaId;
                    asistencia.TipoTarde = tipoEntidad;
                    if (dto.Hora.HasValue)
                    {
                        if (esHorarioSalida) asistencia.HoraSalidaTarde = dto.Hora;
                        else if (esHorarioEntrada) asistencia.HoraEntradaTarde = dto.Hora;
                    }
                }

                asistencia.CalcularAsistencia();
                cont++;
            }

            await _context.SaveChangesAsync();
            return cont;
        }

        public async Task<AsistenciaResponseDto> RegistrarAsistenciaIndividualAsync(RegistrarAsistenciaDto dto)
        {
            // ✅ Normalizar fecha también acá (por si llaman directo)
            dto.Fecha = ToUtcDate(dto.Fecha);

            var lista = new List<RegistrarAsistenciaDto> { dto };
            var procesados = await RegistrarLoteAsync(lista);

            if (procesados == 0)
                throw new Exception("No se pudo procesar el registro (Tipo inválido).");

            DateOnly fechaDto = dto.Fecha;

            // Se recupera el dato del insert único
            var entidad = await _context.Asistencias
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.EstudianteId == dto.EstudianteId && a.Fecha == fechaDto);

            if (entidad == null) throw new Exception("Error al recuperar la asistencia guardada.");

            return new AsistenciaResponseDto
            {
                Id = entidad.Id,
                ValorTotal = entidad.ValorTotalInasistencia,
                Mensaje = "Registrado correctamente."
            };
        }
        public async Task<IEnumerable<AsistenciaGetDTO>> ObtenerAsistenciasAsync(DateOnly? fecha, Guid? estudianteId)
        {
            var query = _context.Asistencias
                .AsNoTracking()
                .Include(a => a.Estudiante)
                .Include(a => a.TipoManiana)
                .Include(a => a.TipoTarde)
                .AsQueryable();

            if (fecha.HasValue)
            {
                query = query.Where(a => a.Fecha == fecha.Value);
            }

            if (estudianteId.HasValue)
            {
                query = query.Where(a => a.EstudianteId == estudianteId.Value);
            }

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
    }
}

