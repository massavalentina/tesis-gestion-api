using Microsoft.EntityFrameworkCore;
using RepoDB.Entities;
using TesisGestorApi.Data;
using TesisGestorApi.DTOs;
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

        // ✅ Helper: convierte cualquier fecha a "fecha UTC" (00:00:00Z)
        private static DateTime ToUtcDate(DateTime value)
        {
            // value.Date deja Kind=Unspecified => lo marcamos como UTC
            return DateTime.SpecifyKind(value.Date, DateTimeKind.Utc);
        }

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
            var fechas = lista.Select(x => x.Fecha).Distinct().ToList(); // ✅ ya viene en UTC

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

                var fechaDto = dto.Fecha; // ✅ ya está normalizada

                var asistencia = asistenciasExistentes
                    .FirstOrDefault(a => a.EstudianteId == dto.EstudianteId && a.Fecha == fechaDto);

                if (asistencia == null)
                {
                    asistencia = new Asistencia
                    {
                        Id = Guid.NewGuid(),
                        EstudianteId = dto.EstudianteId,
                        Fecha = fechaDto, // ✅ UTC 00:00
                        ValorTotalInasistencia = 0
                    };
                    _context.Asistencias.Add(asistencia);
                    asistenciasExistentes.Add(asistencia);
                }

                var turno = dto.Turno?.Trim().ToUpperInvariant();

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

            var entidad = await _context.Asistencias
                .AsNoTracking()
                .FirstOrDefaultAsync(a =>
                    a.EstudianteId == dto.EstudianteId &&
                    a.Fecha == dto.Fecha);

            if (entidad == null)
                throw new Exception("No se encontró la asistencia luego de registrar.");

            return new AsistenciaResponseDto
            {
                Id = entidad.Id,
                ValorTotal = entidad.ValorTotalInasistencia,
                Mensaje = "Registrado correctamente."
            };
        }
    }
}

