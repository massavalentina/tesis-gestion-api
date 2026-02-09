using Microsoft.EntityFrameworkCore;
using RepoDB.Entities;
using TesisGestorApi.Data;
using TesisGestorApi.Entities;

namespace TesisGestorApi.Services
{
    /// <summary>
    /// Clase que implementa la interfaz del servicio de asistencia para poder implementar los métodos abstractos y tener la lógica de negocio del cálculo de asistencias, tanto individual como para lotes. 
    /// </summary>
    public class AsistenciaService : IAsistenciaService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AsistenciaService> _logger;

        public AsistenciaService(ApplicationDbContext context, ILogger<AsistenciaService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<int> RegistrarLoteAsync(List<RegistrarAsistenciaDto> lista)
        {
            // Carga de tipos en memoria
            var idsTipos = lista.Select(x => x.TipoAsistenciaId).Distinct().ToList();
            var tiposDict = await _context.TiposAsistencia
                                          .Where(t => idsTipos.Contains(t.IdTipo))
                                          .ToDictionaryAsync(t => t.IdTipo);

            // Carga de asistencias existentes
            var idsEstudiantes = lista.Select(x => x.EstudianteId).Distinct().ToList();
            var fechas = lista.Select(x => x.Fecha.Date).Distinct().ToList();

            var asistenciasExistentes = await _context.Asistencias
                .Include(a => a.TipoManiana)
                .Include(a => a.TipoTarde)
                .Where(a => idsEstudiantes.Contains(a.EstudianteId) &&
                            fechas.Contains(a.Fecha))
                .ToListAsync();

            int cont = 0;

            // Procesamiento
            foreach (var dto in lista)
            {
                if (!tiposDict.TryGetValue(dto.TipoAsistenciaId, out var tipoEntidad))
                {
                    _logger.LogWarning($"TipoAsistencia {dto.TipoAsistenciaId} no encontrado.");
                    continue;
                }

                var asistencia = asistenciasExistentes
                    .FirstOrDefault(a => a.EstudianteId == dto.EstudianteId && a.Fecha.Date == dto.Fecha.Date);

                if (asistencia == null)
                {
                    asistencia = new Asistencia
                    {
                        Id = Guid.NewGuid(),
                        EstudianteId = dto.EstudianteId,
                        Fecha = dto.Fecha.Date,
                        ValorTotalInasistencia = 0
                    };
                    _context.Asistencias.Add(asistencia);
                    asistenciasExistentes.Add(asistencia);
                }

                var turno = dto.Turno?.Trim().ToUpper();

                if (turno == "MANANA")
                {
                    asistencia.TipoManianaId = dto.TipoAsistenciaId;
                    asistencia.TipoManiana = tipoEntidad;
                }
                else if (turno == "TARDE")
                {
                    asistencia.TipoTardeId = dto.TipoAsistenciaId;
                    asistencia.TipoTarde = tipoEntidad;
                }

                // Lógica de la entidad de dominio
                asistencia.CalcularAsistencia();
                cont++;
            }

            await _context.SaveChangesAsync();
            return cont;
        }

        public async Task<AsistenciaResponseDto> RegistrarAsistenciaIndividualAsync(RegistrarAsistenciaDto dto)
        {
            // Se reutiliza la lógica del método de procesamiento por lote
            var lista = new List<RegistrarAsistenciaDto> { dto };
            var procesados = await RegistrarLoteAsync(lista);

            if (procesados == 0)
                throw new Exception("No se pudo procesar el registro (Tipo inválido).");

            // Se recupera el dato del insert único
            var entidad = await _context.Asistencias
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.EstudianteId == dto.EstudianteId && a.Fecha == dto.Fecha.Date);

            return new AsistenciaResponseDto
            {
                Id = entidad.Id,
                ValorTotal = entidad.ValorTotalInasistencia,
                Mensaje = "Registrado correctamente."
            };
        }
    }
}

