using Microsoft.EntityFrameworkCore;
using RepoDB.Entities;
using TesisGestorApi.Data;
using TesisGestorApi.Exceptions;
using TesisGestorApi.Dtos;



namespace TesisGestorApi.Services;

public class AttendanceService
{
    private readonly ApplicationDbContext _context;

    public AttendanceService(ApplicationDbContext context)
    {
        _context = context;
    }

public async Task<AttendanceScanResponse> PreviewAsync(AttendancePreviewRequest request)
{
    // 1️⃣ Parsear QR (string → Guid)
    if (!Guid.TryParse(request.Qr, out var qrGuid))
        throw new AttendanceException("QR_INVALID", "Código QR inválido");

    // 2️⃣ Buscar credencial
    var credencial = await _context.CredencialesQR
        .Include(c => c.Estudiante)
            .ThenInclude(e => e.DetallesCursado)
                .ThenInclude(dc => dc.Curso)
        .FirstOrDefaultAsync(c => c.Codigo == qrGuid);

    if (credencial == null)
        throw new AttendanceException("QR_INVALID", "Código no reconocido");

    if (!credencial.Activo)
        throw new AttendanceException("QR_INACTIVE", "QR inactivo");

    if (credencial.FechaExpiracion < DateTime.UtcNow)
        throw new AttendanceException("QR_EXPIRED", "QR expirado");

    // 3️⃣ Estudiante activo
    var estudiante = credencial.Estudiante;

    var cursadoActivo = estudiante.DetallesCursado.FirstOrDefault(dc => dc.Estado);
    if (cursadoActivo == null)
        throw new AttendanceException("STUDENT_INACTIVE", "Estudiante inactivo");

    // 4️⃣ Validar asistencia existente por día
    var hoy = DateOnly.FromDateTime(DateTime.UtcNow);

    var asistencia = await _context.Asistencias.FirstOrDefaultAsync(a =>
        a.EstudianteId == estudiante.IdEstudiante &&
        a.Fecha == hoy
    );

    if (asistencia != null)
    {
        if (request.Turno == "Mañana" && asistencia.TipoManianaId != null)
            throw new AttendanceException(
                "ALREADY_SCANNED",
                "Este alumno ya tiene asistencia cargada en el turno mañana"
            );

        if (request.Turno == "Tarde" && asistencia.TipoTardeId != null)
            throw new AttendanceException(
                "ALREADY_SCANNED",
                "Este alumno ya tiene asistencia cargada en el turno tarde"
            );
    }

    // 5️⃣ Preview OK
    return new AttendanceScanResponse
    {
        Student = new StudentDto
        {
            Id = estudiante.IdEstudiante,
            Name = estudiante.Nombre,
            LastName = estudiante.Apellido,
            Course = cursadoActivo.Curso.Codigo
        }
    };
}




    public async Task ConfirmAsync(AttendanceConfirmRequest request)
{
    var tipo = await _context.TiposAsistencia
        .FirstOrDefaultAsync(t => t.IdTipo == request.AttendanceTypeId);

    if (tipo == null)
        throw new AttendanceException("INVALID_ATTENDANCE_TYPE", "Tipo inválido");

    var hoy = DateOnly.FromDateTime(DateTime.UtcNow);

    foreach (var estudianteId in request.StudentIds)
    {
        var asistencia = await _context.Asistencias
            .Include(a => a.TipoManiana)
            .Include(a => a.TipoTarde)
            .FirstOrDefaultAsync(a =>
                a.EstudianteId == estudianteId &&
                a.Fecha == hoy
            );

        if (asistencia == null)
        {
            asistencia = new Asistencia
            {
                Id = Guid.NewGuid(),
                EstudianteId = estudianteId,
                Fecha = hoy
            };

            _context.Asistencias.Add(asistencia);
        }

        if (request.Turno == "Mañana")
            asistencia.TipoManianaId = tipo.IdTipo;
        else
            asistencia.TipoTardeId = tipo.IdTipo;

        asistencia.CalcularAsistencia();
    }

    await _context.SaveChangesAsync();
}


    public async Task<List<SelectOptionDto>> GetCursosAsync()
        {
            return await _context.Cursos
                .Include(c => c.Anio)
                .Include(c => c.Division)
                .Where(c => c.Estado)
                .Select(c => new SelectOptionDto
                {
                    Id = c.IdCurso.ToString(), 
                    Label = $"{c.Anio.Numero}{c.Division.Nombre}"
                })
                .ToListAsync();
        }

    public List<SelectOptionDto> GetTurnos()
        {
            return Enum.GetValues<Turno>()
                .Select(t => new SelectOptionDto
                {
            Id = ((int)t).ToString(),         
                    Label = t.ToString()
                })
                .ToList();
        }

    public async Task<List<SelectOptionDto>> GetTiposAsistenciaAsync()
        {
            return await _context.TiposAsistencia
                .Select(t => new SelectOptionDto
                {
            Id = t.IdTipo.ToString(),   
                    Label = t.Codigo
                })
                .ToListAsync();
        }

}

