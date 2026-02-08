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

    public async Task<AttendanceScanResponse> ScanAsync(AttendanceScanRequest request)
    {
        // 1️⃣ Buscar credencial por código QR
        var credencial = await _context.CredencialesQR
            .Include(c => c.Estudiante)
                .ThenInclude(e => e.DetallesCursado)
                    .ThenInclude(dc => dc.Curso)
            .FirstOrDefaultAsync(c => c.Codigo == request.Qr);

        if (credencial == null)
            throw new AttendanceException("QR_INVALID", "Código no reconocido");

        if (!credencial.Activo)
            throw new AttendanceException("QR_INACTIVE", "Este código QR no se encuentra activo");

        if (credencial.FechaExpiracion < DateTime.UtcNow)
            throw new AttendanceException("QR_EXPIRED", "Este código QR se encuentra expirado");

        // 2️⃣ Estudiante y cursado activo
        var estudiante = credencial.Estudiante;

        var cursadoActivo = estudiante.DetallesCursado.FirstOrDefault(dc => dc.Estado);
        if (cursadoActivo == null)
            throw new AttendanceException("STUDENT_INACTIVE", "Este código QR pertenece a un estudiante inactivo");

        // 3️⃣ Parsear Turno
        if (!Enum.TryParse<Turno>(request.Turno, true, out var turno))
            throw new AttendanceException("INVALID_TURNO", "Turno inválido");

        // 4️⃣ Validar no escaneado hoy + turno
        var today = DateTime.UtcNow.Date;

        var yaEscaneado = await _context.Asistencias.AnyAsync(a =>
            a.IdEstudiante == estudiante.IdEstudiante &&
            a.Turno == turno &&
            a.FechaAsistencia.Date == today
        );

        if (yaEscaneado)
        {
            throw new AttendanceException(
                "ALREADY_SCANNED",
                $"El código del estudiante {estudiante.Nombre} {estudiante.Apellido} ya fue escaneado hoy en el turno {turno}"
            );
        }

        // 5️⃣ Obtener TipoAsistencia (mapear int → Guid)
        var tipoAsistencia = await _context.TiposAsistencia
        .FirstOrDefaultAsync(t => t.IdTipo == request.AttendanceTypeId);

        if (tipoAsistencia == null)
            throw new AttendanceException(
                "INVALID_ATTENDANCE_TYPE",
                "Tipo de asistencia inválido"
        );


        // 6️⃣ Hora del servidor
        var now = DateTime.UtcNow;

        // 7️⃣ Crear asistencia
        var asistencia = new Asistencia
        {
            IdAsistencia = Guid.NewGuid(),
            FechaAsistencia = now,
            Turno = turno,

            IdEstudiante = estudiante.IdEstudiante,
            Estudiante = estudiante, // 👈 CLAVE

            IdTipoAsistencia = tipoAsistencia.IdTipo,
            TipoAsistencia = tipoAsistencia // 👈 recomendado
        };


        _context.Asistencias.Add(asistencia);
        await _context.SaveChangesAsync();

        // 8️⃣ Response
        return new AttendanceScanResponse
        {
            Student = new StudentDto
            {
                Id = estudiante.IdEstudiante,
                Name = estudiante.Nombre,
                LastName = estudiante.Apellido,
                Course = cursadoActivo.Curso.Codigo
            },
            Attendance = new AttendanceDto
            {
                Time = now.ToString("HH:mm:ss"),
                AttendanceType = tipoAsistencia.Codigo,
                Turno = turno.ToString()
            }
        };
    }

    public async Task<AttendanceScanResponse> PreviewAsync(AttendancePreviewRequest request)
{
    // 1️⃣ Buscar credencial por código QR
    var credencial = await _context.CredencialesQR
        .Include(c => c.Estudiante)
            .ThenInclude(e => e.DetallesCursado)
                .ThenInclude(dc => dc.Curso)
        .FirstOrDefaultAsync(c => c.Codigo == request.Qr);

    if (credencial == null)
        throw new AttendanceException("QR_INVALID", "Código no reconocido");

    if (!credencial.Activo)
        throw new AttendanceException("QR_INACTIVE", "Este código QR no se encuentra activo");

    if (credencial.FechaExpiracion < DateTime.UtcNow)
        throw new AttendanceException("QR_EXPIRED", "Este código QR se encuentra expirado");

    // 2️⃣ Estudiante y cursado activo
    var estudiante = credencial.Estudiante;

    var cursadoActivo = estudiante.DetallesCursado.FirstOrDefault(dc => dc.Estado);
    if (cursadoActivo == null)
        throw new AttendanceException(
            "STUDENT_INACTIVE",
            "Este código QR pertenece a un estudiante inactivo"
        );

    // 3️⃣ Response (NO guarda nada)
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
        // 1️⃣ Validar turno
        if (!Enum.TryParse<Turno>(request.Turno, true, out var turno))
            throw new AttendanceException("INVALID_TURNO", "Turno inválido");

        // 2️⃣ Obtener TipoAsistencia (ENTIDAD TRACKED)
        var tipoAsistencia = await _context.TiposAsistencia
            .FirstOrDefaultAsync(t => t.IdTipo == request.AttendanceTypeId);

        if (tipoAsistencia == null)
            throw new AttendanceException(
                "INVALID_ATTENDANCE_TYPE",
                "Tipo de asistencia inválido"
            );

        // 🔑 MUY IMPORTANTE
        // Le decimos a EF: "este TipoAsistencia ya existe"
        _context.Attach(tipoAsistencia);

        var now = DateTime.UtcNow;
        var today = now.Date;

        // 3️⃣ Traer estudiantes (ENTIDADES TRACKED)
        var estudiantes = await _context.Estudiantes
            .Where(e => request.StudentIds.Contains(e.IdEstudiante))
            .ToListAsync();

        if (estudiantes.Count != request.StudentIds.Count)
            throw new AttendanceException(
                "STUDENT_NOT_FOUND",
                "Uno o más estudiantes no existen"
            );

        // 🔑 MUY IMPORTANTE
        _context.AttachRange(estudiantes);

        // 4️⃣ Crear asistencias
        foreach (var estudiante in estudiantes)
        {
            var yaExiste = await _context.Asistencias.AnyAsync(a =>
                a.IdEstudiante == estudiante.IdEstudiante &&
                a.Turno == turno &&
                a.FechaAsistencia.Date == today
            );

            if (yaExiste)
                throw new AttendanceException(
                    "ALREADY_SCANNED",
                    $"El estudiante {estudiante.Nombre} {estudiante.Apellido} ya tiene asistencia cargada hoy"
                );

            _context.Asistencias.Add(new Asistencia
            {
                IdAsistencia = Guid.NewGuid(),
                FechaAsistencia = now,
                Turno = turno,

                // 🔑 FK + navigation (POR LA SHADOW FK)
                IdEstudiante = estudiante.IdEstudiante,
                Estudiante = estudiante,

                // 🔑 FK + navigation (POR LA SHADOW FK)
                IdTipoAsistencia = tipoAsistencia.IdTipo,
                TipoAsistencia = tipoAsistencia
            });
        }

        // 5️⃣ Guardar todo
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
                    Id = c.IdCurso.ToString(), // 👈 Guid → string
                    Label = $"{c.Anio.Numero}{c.Division.Nombre}"
                })
                .ToListAsync();
        }

    public List<SelectOptionDto> GetTurnos()
        {
            return Enum.GetValues<Turno>()
                .Select(t => new SelectOptionDto
                {
            Id = ((int)t).ToString(),         // 👈 "Mañana", "Tarde"
                    Label = t.ToString()
                })
                .ToList();
        }

    public async Task<List<SelectOptionDto>> GetTiposAsistenciaAsync()
        {
            return await _context.TiposAsistencia
                .Select(t => new SelectOptionDto
                {
            Id = t.IdTipo.ToString(),   // 👈 Guid → string
                    Label = t.Codigo
                })
                .ToListAsync();
        }

}

