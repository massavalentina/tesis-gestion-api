using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using RepoDB.Entities;
using TesisGestorApi.Data;
using TesisGestorApi.DTOs;
using TesisGestorApi.Interfaces;

namespace TesisGestorApi.Services
{
    public class QrCredentialDeliveryService : IQrCredentialDeliveryService
    {
        private const string ScopeTodos = "TODOS";
        private const string ScopePendientes = "PENDIENTES";

        private const string EstadoEnviado = "ENVIADO";
        private const string EstadoPendiente = "PENDIENTE_ENVIO";
        private const string EstadoSinQr = "SIN_QR";
        private const string EstadoSinTutor = "SIN_TUTOR_PRINCIPAL";
        private const string EstadoEmailInvalido = "EMAIL_INVALIDO";

        private readonly ApplicationDbContext _db;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly QrCredentialDeliveryProgressStore _progress;
        private readonly IQrCredentialVisualService _qrVisualService;
        private static readonly Lazy<byte[]?> InstitutionLogoBytes = new(LoadInstitutionLogo);

        public QrCredentialDeliveryService(
            ApplicationDbContext db,
            IServiceScopeFactory scopeFactory,
            QrCredentialDeliveryProgressStore progress,
            IQrCredentialVisualService qrVisualService)
        {
            _db = db;
            _scopeFactory = scopeFactory;
            _progress = progress;
            _qrVisualService = qrVisualService;
        }

        public async Task<QrCredentialDeliverySummaryDto> GetSummaryAsync(Guid cursoId, string? alcance, CancellationToken ct = default)
        {
            var scope = NormalizeScope(alcance);
            var context = await BuildContextAsync(_db, cursoId, ct);
            return BuildSummary(context, scope);
        }

        public async Task<QrCredentialDeliveryProgressDto> StartDeliveryJobAsync(QrCredentialDeliveryRequestDto req, CancellationToken ct = default)
        {
            var scope = NormalizeScope(req.Alcance);
            var context = await BuildContextAsync(_db, req.IdCurso, ct);
            var summary = BuildSummary(context, scope);

            if (req.ModoEstricto && summary.TotalSinQrGenerado > 0)
            {
                throw new InvalidOperationException(
                    "Modo estricto activo: hay estudiantes sin QR generado. Generá todas las credenciales antes de iniciar el envío.");
            }

            var candidates = SelectCandidates(context.Rows, scope).ToList();
            var job = _progress.Create(candidates.Count);

            if (candidates.Count == 0)
            {
                _progress.Update(job.JobId, p =>
                {
                    p.Estado = "COMPLETED";
                    p.Fin = DateTime.UtcNow;
                    p.UltimoMensaje = "No hay credenciales para enviar con el alcance seleccionado.";
                });

                return job;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    string? lastErrorMessage = null;
                    string? lastErrorStudent = null;
                    string? lastErrorDestination = null;

                    using var scopeFactory = _scopeFactory.CreateScope();
                    var db = scopeFactory.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var emailSender = scopeFactory.ServiceProvider.GetRequiredService<IEmailSender>();
                    var qrVisualService = scopeFactory.ServiceProvider.GetRequiredService<IQrCredentialVisualService>();
                    var templateService = scopeFactory.ServiceProvider.GetRequiredService<IQrCredentialEmailTemplateService>();

                    var liveContext = await BuildContextAsync(db, req.IdCurso, CancellationToken.None);
                    var liveCandidates = SelectCandidates(liveContext.Rows, scope)
                        .ToDictionary(x => x.IdEstudiante, x => x);
                    var logoBytes = TryLoadInstitutionLogo();
                    const string logoContentId = "institution-logo";

                    foreach (var candidate in candidates)
                    {
                        if (!liveCandidates.TryGetValue(candidate.IdEstudiante, out var liveCandidate))
                        {
                            _progress.Update(job.JobId, p =>
                            {
                                p.Procesados++;
                                p.Omitidos++;
                                p.UltimoEstudiante = candidate.NombreCompleto;
                                p.UltimoMensaje = "Omitido: el estado cambió antes del envío.";
                            });
                            continue;
                        }

                        if (!liveCandidate.IdQr.HasValue || !liveCandidate.CodigoQr.HasValue || string.IsNullOrWhiteSpace(liveCandidate.TutorEmail))
                        {
                            _progress.Update(job.JobId, p =>
                            {
                                p.Procesados++;
                                p.Omitidos++;
                                p.UltimoEstudiante = liveCandidate.NombreCompleto;
                                p.UltimoMensaje = "Omitido: faltan datos requeridos para el envío.";
                            });
                            continue;
                        }

                        var credencial = await db.CredencialesQR
                            .FirstOrDefaultAsync(q => q.IdQR == liveCandidate.IdQr.Value, CancellationToken.None);

                        if (credencial is null)
                        {
                            _progress.Update(job.JobId, p =>
                            {
                                p.Procesados++;
                                p.Omitidos++;
                                p.UltimoEstudiante = liveCandidate.NombreCompleto;
                                p.UltimoMensaje = "Omitido: credencial no encontrada.";
                            });
                            continue;
                        }

                        try
                        {
                            var qrBytes = qrVisualService.BuildQrPng(liveCandidate.CodigoQr.Value);
                            var qrContentId = $"qr-{liveCandidate.IdEstudiante:N}";

                            var subject = string.IsNullOrWhiteSpace(req.Asunto)
                                ? $"Credencial QR - {liveContext.AnioLectivo} - {liveCandidate.NombreCompleto}"
                                : req.Asunto.Trim();

                            var htmlBody = templateService.Build(
                                tutorNombre: liveCandidate.TutorPrincipalNombre ?? "Tutor/a",
                                alumnoNombre: liveCandidate.NombreCompleto,
                                alumnoDni: liveCandidate.Dni,
                                anioLectivo: liveContext.AnioLectivo,
                                codigoQr: liveCandidate.CodigoQr.Value,
                                fechaVigencia: credencial.FechaExpiracion,
                                mensajePersonalizado: req.MensajePersonalizado,
                                qrInlineContentId: qrContentId,
                                logoInlineContentId: logoBytes is { Length: > 0 } ? logoContentId : null);

                            var attachment = new EmailAttachmentDto
                            {
                                FileName = $"credencial-{liveCandidate.Dni}.png",
                                ContentType = "image/png",
                                Content = qrBytes
                            };

                            var inlineImage = new EmailInlineResourceDto
                            {
                                ContentId = qrContentId,
                                ContentType = "image/png",
                                Content = qrBytes
                            };

                            var inlineResources = new List<EmailInlineResourceDto> { inlineImage };

                            if (logoBytes is { Length: > 0 })
                            {
                                inlineResources.Add(new EmailInlineResourceDto
                                {
                                    ContentId = logoContentId,
                                    ContentType = "image/png",
                                    Content = logoBytes
                                });
                            }

                            await emailSender.SendAsync(
                                to: liveCandidate.TutorEmail,
                                subject: subject,
                                htmlBody: htmlBody,
                                ct: CancellationToken.None,
                                attachments: [attachment],
                                inlineResources: inlineResources);

                            credencial.Enviado = true;
                            await db.SaveChangesAsync(CancellationToken.None);

                            _progress.Update(job.JobId, p =>
                            {
                                p.Procesados++;
                                p.Enviados++;
                                p.UltimoDestino = liveCandidate.TutorEmail;
                                p.UltimoEstudiante = liveCandidate.NombreCompleto;
                                p.UltimoMensaje = "Enviado.";
                            });
                        }
                        catch (Exception ex)
                        {
                            var errorDetail = $"{liveCandidate.NombreCompleto} <{liveCandidate.TutorEmail}>: {ex.Message}";
                            lastErrorMessage = ex.Message;
                            lastErrorStudent = liveCandidate.NombreCompleto;
                            lastErrorDestination = liveCandidate.TutorEmail;

                            _progress.Update(job.JobId, p =>
                            {
                                p.Procesados++;
                                p.Errores++;
                                p.UltimoDestino = liveCandidate.TutorEmail;
                                p.UltimoEstudiante = liveCandidate.NombreCompleto;
                                p.UltimoMensaje = $"Error: {ex.Message}";

                                // Guarda una traza acotada de errores para mostrar en UI sin crecer indefinidamente en memoria.
                                if (p.DetallesErrores.Count < 50)
                                {
                                    p.DetallesErrores.Add(errorDetail);
                                }
                            });
                        }
                    }

                    _progress.Update(job.JobId, p =>
                    {
                        p.Estado = "COMPLETED";
                        p.Fin = DateTime.UtcNow;

                        if (p.Errores > 0 && !string.IsNullOrWhiteSpace(lastErrorMessage))
                        {
                            p.UltimoEstudiante = lastErrorStudent;
                            p.UltimoDestino = lastErrorDestination;
                            p.UltimoMensaje = $"Finalizado con {p.Errores} error(es). Último error: {lastErrorMessage}";
                        }
                        else
                        {
                            p.UltimoMensaje = "Proceso finalizado.";
                        }
                    });
                }
                catch (Exception ex)
                {
                    _progress.Update(job.JobId, p =>
                    {
                        p.Estado = "FAILED";
                        p.Fin = DateTime.UtcNow;
                        p.UltimoMensaje = ex.Message;
                    });
                }
            });

            return job;
        }

        public async Task<QrCredentialDeliveryStudentsPageDto> GetStudentsPageAsync(
            Guid cursoId,
            string? estado,
            string? busqueda,
            int page,
            int pageSize,
            CancellationToken ct = default)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;
            if (pageSize > 100) pageSize = 100;

            var context = await BuildContextAsync(_db, cursoId, ct);
            var rows = context.Rows.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(estado))
            {
                var estadoFiltro = estado.Trim().ToUpperInvariant();
                if (estadoFiltro != "TODOS")
                    rows = rows.Where(r => r.Estado == estadoFiltro);
            }

            if (!string.IsNullOrWhiteSpace(busqueda))
            {
                var term = busqueda.Trim();
                rows = rows.Where(r =>
                    r.NombreCompleto.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || r.Dni.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || (r.TutorPrincipalNombre?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (r.TutorEmail?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            var ordered = rows
                .OrderBy(r => r.NombreCompleto)
                .ToList();

            var totalItems = ordered.Count;
            var totalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);
            var skip = (page - 1) * pageSize;

            var items = ordered
                .Skip(skip)
                .Take(pageSize)
                .Select(r => new QrCredentialDeliveryStudentRowDto
                {
                    IdEstudiante = r.IdEstudiante,
                    NombreCompleto = r.NombreCompleto,
                    Dni = r.Dni,
                    TutorPrincipalNombre = r.TutorPrincipalNombre,
                    TutorPrincipalEmail = r.TutorEmail,
                    Estado = r.Estado,
                    FechaGeneracionQr = r.FechaGeneracionQr
                })
                .ToList();

            return new QrCredentialDeliveryStudentsPageDto
            {
                Page = page,
                PageSize = pageSize,
                TotalItems = totalItems,
                TotalPages = totalPages,
                Items = items
            };
        }

        public async Task<(byte[] Bytes, string FileName)> GetStudentQrImageAsync(Guid estudianteId, CancellationToken ct = default)
        {
            var credencial = await _db.CredencialesQR
                .AsNoTracking()
                .Where(c => c.IdEstudiante == estudianteId && c.Activo)
                .OrderByDescending(c => c.FechaGeneracion)
                .Select(c => new { c.Codigo })
                .FirstOrDefaultAsync(ct);

            if (credencial is null)
                throw new InvalidOperationException("El estudiante no tiene una credencial QR activa.");

            var bytes = _qrVisualService.BuildQrPng(credencial.Codigo);
            return (bytes, $"credencial-{estudianteId:N}.png");
        }

        private static string NormalizeScope(string? alcance)
        {
            var normalized = (alcance ?? ScopePendientes).Trim().ToUpperInvariant();
            return normalized switch
            {
                ScopeTodos => ScopeTodos,
                ScopePendientes => ScopePendientes,
                _ => throw new InvalidOperationException("Alcance inválido. Valores permitidos: TODOS, PENDIENTES.")
            };
        }

        private static bool IsValidEmail(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;

            try
            {
                var address = new MailAddress(value.Trim());
                return address.Address.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static IEnumerable<DeliveryStudentRow> SelectCandidates(IEnumerable<DeliveryStudentRow> rows, string scope)
        {
            return scope == ScopeTodos
                ? rows.Where(r => r.Estado is EstadoEnviado or EstadoPendiente)
                : rows.Where(r => r.Estado == EstadoPendiente);
        }

        private static QrCredentialDeliverySummaryDto BuildSummary(DeliveryContext context, string scope)
        {
            var candidates = SelectCandidates(context.Rows, scope).Count();

            return new QrCredentialDeliverySummaryDto
            {
                IdCurso = context.IdCurso,
                CursoCodigo = context.CursoCodigo,
                AnioLectivo = context.AnioLectivo,
                Alcance = scope,
                TotalAlumnosActivos = context.Rows.Count,
                TotalTutoresPrincipales = context.Rows.Count(r => r.TutorPrincipalNombre is not null),
                TotalQrEnviados = context.Rows.Count(r => r.Estado == EstadoEnviado),
                TotalQrPendientesEnvio = context.Rows.Count(r => r.Estado == EstadoPendiente),
                TotalSinQrGenerado = context.Rows.Count(r => r.Estado == EstadoSinQr),
                TotalSinTutorPrincipal = context.Rows.Count(r => r.Estado == EstadoSinTutor),
                TotalEmailInvalido = context.Rows.Count(r => r.Estado == EstadoEmailInvalido),
                TotalCandidatosSegunAlcance = candidates,
                EstimacionSegundos = (int)Math.Ceiling(candidates * 2d),
                PuedeIniciarEnvio = candidates > 0,
                Mensaje = candidates > 0
                    ? "Resumen generado correctamente."
                    : "No hay credenciales para enviar con el alcance seleccionado."
            };
        }

        private static string ResolveEstado(bool hasQr, bool hasTutorPrincipal, bool hasValidEmail, bool enviado)
        {
            if (!hasQr) return EstadoSinQr;
            if (!hasTutorPrincipal) return EstadoSinTutor;
            if (!hasValidEmail) return EstadoEmailInvalido;
            return enviado ? EstadoEnviado : EstadoPendiente;
        }

        private static string BuildFullName(string apellido, string nombre)
            => $"{apellido}, {nombre}";

        private static string BuildTutorName(string apellido, string nombre)
            => string.Join(" ", new[] { nombre?.Trim(), apellido?.Trim() }.Where(x => !string.IsNullOrWhiteSpace(x)));

        private static byte[]? TryLoadInstitutionLogo()
            => InstitutionLogoBytes.Value;

        private static byte[]? LoadInstitutionLogo()
        {
            var candidates = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "utils", "robles.png"),
                Path.Combine(Directory.GetCurrentDirectory(), "utils", "robles.png")
            };

            foreach (var candidate in candidates)
            {
                try
                {
                    var fullPath = Path.GetFullPath(candidate);

                    if (File.Exists(fullPath))
                    {
                        return File.ReadAllBytes(fullPath);
                    }
                }
                catch
                {
                    // Si no se puede leer el logo, el email se envía sin imagen institucional.
                }
            }

            return null;
        }

        private static async Task<DeliveryContext> BuildContextAsync(ApplicationDbContext db, Guid cursoId, CancellationToken ct)
        {
            var curso = await db.Cursos
                .AsNoTracking()
                .Where(c => c.IdCurso == cursoId)
                .Select(c => new { c.IdCurso, c.Codigo, c.Estado, c.AñoLectivo })
                .FirstOrDefaultAsync(ct);

            if (curso is null)
                throw new InvalidOperationException("No existe el curso seleccionado.");

            if (!curso.Estado)
                throw new InvalidOperationException("El curso seleccionado está inactivo.");

            var anioLectivo = curso.AñoLectivo.Year;

            var students = await db.DetallesCursado
                .AsNoTracking()
                .Where(dc => dc.IdCurso == cursoId && dc.Estado)
                .Select(dc => new
                {
                    dc.IdEstudiante,
                    dc.Estudiante.Nombre,
                    dc.Estudiante.Apellido,
                    dc.Estudiante.Documento
                })
                .Distinct()
                .ToListAsync(ct);

            var studentIds = students.Select(s => s.IdEstudiante).ToList();

            var qrByStudent = await db.CredencialesQR
                .AsNoTracking()
                .Where(q => studentIds.Contains(q.IdEstudiante) && q.Activo && q.AñoLectivo.Year == anioLectivo)
                .OrderByDescending(q => q.FechaGeneracion)
                .Select(q => new
                {
                    q.IdQR,
                    q.IdEstudiante,
                    q.Codigo,
                    q.Enviado,
                    q.FechaGeneracion
                })
                .ToListAsync(ct);

            var activeQrMap = qrByStudent
                .GroupBy(x => x.IdEstudiante)
                .ToDictionary(g => g.Key, g => g.First());

            var principalTutorByStudent = await db.Set<TutorEstudiante>()
                .AsNoTracking()
                .Where(te => studentIds.Contains(te.IdEstudiante) && te.EsPrincipal)
                .Select(te => new
                {
                    te.IdEstudiante,
                    te.Tutor.Nombre,
                    te.Tutor.Apellido,
                    te.Tutor.Correo
                })
                .ToListAsync(ct);

            var tutorMap = principalTutorByStudent
                .GroupBy(x => x.IdEstudiante)
                .ToDictionary(
                    g => g.Key,
                    g => g
                        .OrderBy(t => t.Apellido)
                        .ThenBy(t => t.Nombre)
                        .First());

            var rows = new List<DeliveryStudentRow>(students.Count);

            foreach (var student in students)
            {
                var hasQr = activeQrMap.TryGetValue(student.IdEstudiante, out var qr);
                var hasTutor = tutorMap.TryGetValue(student.IdEstudiante, out var tutor);
                var tutorEmail = hasTutor ? tutor!.Correo?.Trim() : null;
                var validEmail = IsValidEmail(tutorEmail);

                rows.Add(new DeliveryStudentRow
                {
                    IdEstudiante = student.IdEstudiante,
                    NombreCompleto = BuildFullName(student.Apellido, student.Nombre),
                    Dni = student.Documento,
                    TutorPrincipalNombre = hasTutor ? BuildTutorName(tutor!.Apellido, tutor!.Nombre) : null,
                    TutorEmail = tutorEmail,
                    IdQr = hasQr ? qr!.IdQR : null,
                    CodigoQr = hasQr ? qr!.Codigo : null,
                    QrEnviado = hasQr && qr!.Enviado,
                    FechaGeneracionQr = hasQr ? qr!.FechaGeneracion : null,
                    Estado = ResolveEstado(hasQr, hasTutor, validEmail, hasQr && qr!.Enviado)
                });
            }

            return new DeliveryContext
            {
                IdCurso = curso.IdCurso,
                CursoCodigo = curso.Codigo,
                AnioLectivo = anioLectivo,
                Rows = rows
            };
        }

        private sealed class DeliveryContext
        {
            public Guid IdCurso { get; set; }
            public string CursoCodigo { get; set; } = string.Empty;
            public int AnioLectivo { get; set; }
            public List<DeliveryStudentRow> Rows { get; set; } = new();
        }

        private sealed class DeliveryStudentRow
        {
            public Guid IdEstudiante { get; set; }
            public string NombreCompleto { get; set; } = string.Empty;
            public string Dni { get; set; } = string.Empty;
            public string? TutorPrincipalNombre { get; set; }
            public string? TutorEmail { get; set; }
            public Guid? IdQr { get; set; }
            public Guid? CodigoQr { get; set; }
            public bool QrEnviado { get; set; }
            public DateTime? FechaGeneracionQr { get; set; }
            public string Estado { get; set; } = EstadoSinQr;
        }
    }
}
