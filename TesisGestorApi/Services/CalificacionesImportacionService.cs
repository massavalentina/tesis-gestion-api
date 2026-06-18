using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.Json;
using TesisGestorApi.Data;
using TesisGestorApi.DTOs.CalificacionesImportacion;
using TesisGestorApi.Entities;
using TesisGestorApi.Interfaces;

namespace TesisGestorApi.Services
{
    public class CalificacionesImportacionService : ICalificacionesImportacionService
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private readonly ApplicationDbContext _context;
        private readonly ICurrentUserService _currentUser;
        private readonly ICidiCalificacionesPdfParser _parser;
        private readonly ICalificacionesWriteService _writeService;
        private readonly ISupabaseStorageService _storageService;

        public CalificacionesImportacionService(
            ApplicationDbContext context,
            ICurrentUserService currentUser,
            ICidiCalificacionesPdfParser parser,
            ICalificacionesWriteService writeService,
            ISupabaseStorageService storageService)
        {
            _context = context;
            _currentUser = currentUser;
            _parser = parser;
            _writeService = writeService;
            _storageService = storageService;
        }

        public async Task<ImportacionCalificacionesDetalleDto?> GetActivaPorECAsync(Guid idEC, Guid idDocente, CancellationToken ct)
        {
            await GetEspacioContextAsync(idEC, idDocente, ct);

            var session = await _context.ImportacionesCalificaciones
                .AsNoTracking()
                .Where(i => i.IdEC == idEC
                    && i.Estado != EstadoImportacionCalificaciones.Confirmada
                    && i.Estado != EstadoImportacionCalificaciones.Cancelada)
                .OrderByDescending(i => i.FechaUltimaActualizacion)
                .FirstOrDefaultAsync(ct);

            return session == null ? null : await MapDetalleAsync(session, ct, true);
        }

        public async Task<ImportacionCalificacionesDetalleDto> AnalizarAsync(Guid idEC, Guid idDocente, AnalizarImportacionCalificacionesDto dto, CancellationToken ct)
        {
            if (dto.Archivo == null || dto.Archivo.Length == 0)
            {
                throw new ValidationException("Debés seleccionar un PDF exportado desde CiDi.");
            }

            if (!dto.Archivo.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase)
                && !dto.Archivo.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                throw new ValidationException("El archivo debe ser un PDF.");
            }

            if (dto.Archivo.Length > 50 * 1024 * 1024)
            {
                throw new ValidationException("El archivo no puede superar los 50 MB.");
            }

            var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException("Usuario no autenticado.");
            var espacio = await GetEspacioContextAsync(idEC, idDocente, ct);
            var fileBytes = await ReadFileBytesAsync(dto.Archivo, ct);
            var hash = ComputeSha256(fileBytes);

            var analysis = await BuildAnalysisAsync(espacio, fileBytes, ct);
            var now = DateTime.UtcNow;

            var entity = new ImportacionCalificaciones
            {
                IdImportacionCalificaciones = Guid.NewGuid(),
                IdEC = espacio.IdEC,
                IdCurso = espacio.IdCurso,
                IdDocente = idDocente,
                IdUsuario = userId,
                AnioLectivo = espacio.AnioLectivo,
                Estado = analysis.Estado,
                NombreArchivoOriginal = dto.Archivo.FileName,
                ContentType = dto.Archivo.ContentType,
                TamanioArchivoBytes = dto.Archivo.Length,
                HashArchivoSha256 = hash,
                MotorLectura = "PdfPig",
                ArchivoTemporalContenido = fileBytes,
                ResumenAnalisisJson = JsonSerializer.Serialize(analysis.Resumen, JsonOptions),
                RevisionJson = analysis.Revision == null ? null : JsonSerializer.Serialize(analysis.Revision, JsonOptions),
                ResumenConfirmacionJson = JsonSerializer.Serialize(analysis.Confirmacion, JsonOptions),
                FechaCreacion = now,
                FechaUltimaActualizacion = now,
            };

            _context.ImportacionesCalificaciones.Add(entity);
            await _context.SaveChangesAsync(ct);

            return await MapDetalleAsync(entity, ct, true);
        }

        public async Task<ImportacionCalificacionesDetalleDto> GetDetalleAsync(Guid idImportacion, Guid idDocente, CancellationToken ct)
        {
            var entity = await GetOwnedImportacionAsync(idImportacion, idDocente, ct);
            return await MapDetalleAsync(entity, ct, entity.Estado != EstadoImportacionCalificaciones.Confirmada);
        }

        public async Task<ImportacionCalificacionesDetalleDto> ReanalizarAsync(Guid idImportacion, Guid idDocente, CancellationToken ct)
        {
            var entity = await GetOwnedImportacionAsync(idImportacion, idDocente, ct);
            if (entity.Estado == EstadoImportacionCalificaciones.Confirmada || entity.Estado == EstadoImportacionCalificaciones.Cancelada)
            {
                throw new InvalidOperationException("La importación ya no puede reanalizarse.");
            }

            if (entity.ArchivoTemporalContenido == null || entity.ArchivoTemporalContenido.Length == 0)
            {
                throw new InvalidOperationException("No se encontró el archivo temporal de esta importación.");
            }

            var espacio = await GetEspacioContextAsync(entity.IdEC, idDocente, ct);
            var analysis = await BuildAnalysisAsync(espacio, entity.ArchivoTemporalContenido, ct);

            entity.Estado = analysis.Estado;
            entity.ResumenAnalisisJson = JsonSerializer.Serialize(analysis.Resumen, JsonOptions);
            entity.RevisionJson = analysis.Revision == null ? null : JsonSerializer.Serialize(analysis.Revision, JsonOptions);
            entity.ResumenConfirmacionJson = JsonSerializer.Serialize(analysis.Confirmacion, JsonOptions);
            entity.ErrorTecnico = null;
            entity.FechaUltimaActualizacion = DateTime.UtcNow;

            await _context.SaveChangesAsync(ct);
            return await MapDetalleAsync(entity, ct, true);
        }

        public async Task<ImportacionRevisionDto> GetRevisionAsync(Guid idImportacion, Guid idDocente, CancellationToken ct)
        {
            var entity = await GetOwnedImportacionAsync(idImportacion, idDocente, ct);
            var revision = GetRevisionOrThrow(entity);
            return revision;
        }

        public async Task<ImportacionRevisionDto> GuardarRevisionAsync(Guid idImportacion, Guid idDocente, ActualizarImportacionRevisionDto dto, CancellationToken ct)
        {
            var entity = await GetOwnedImportacionAsync(idImportacion, idDocente, ct);
            if (entity.Estado == EstadoImportacionCalificaciones.Confirmada || entity.Estado == EstadoImportacionCalificaciones.Cancelada)
            {
                throw new InvalidOperationException("La importación ya no admite cambios.");
            }

            var revision = GetRevisionOrThrow(entity);
            if (revision.Bloqueos.Count > 0)
            {
                throw new InvalidOperationException("La importación tiene bloqueos técnicos pendientes y no puede editarse.");
            }

            var rowsById = revision.Rows.ToDictionary(row => row.RowId);
            var estudianteIdsValidos = revision.EstudiantesCurso.Select(option => option.IdEstudiante).ToHashSet();

            foreach (var rowUpdate in dto.Rows)
            {
                if (!rowsById.TryGetValue(rowUpdate.RowId, out var row))
                {
                    throw new ValidationException($"La fila '{rowUpdate.RowId}' no existe en la sesión de importación.");
                }

                if (rowUpdate.EstudianteAsociadoId.HasValue && !estudianteIdsValidos.Contains(rowUpdate.EstudianteAsociadoId.Value))
                {
                    throw new ValidationException($"El estudiante '{rowUpdate.EstudianteAsociadoId}' no pertenece al curso del espacio curricular.");
                }

                row.EstudianteAsociadoId = rowUpdate.EstudianteAsociadoId;
                row.Omitida = rowUpdate.Omitida;

                var cellsBySlot = row.Cells.ToDictionary(cell => cell.SlotKey);
                foreach (var cellUpdate in rowUpdate.Cells)
                {
                    if (!cellsBySlot.TryGetValue(cellUpdate.SlotKey, out var cell))
                    {
                        throw new ValidationException($"La celda '{cellUpdate.SlotKey}' no existe en la fila '{row.RowId}'.");
                    }

                    if (cellUpdate.ValorFinal is < 1 or > 10)
                    {
                        throw new ValidationException("Las notas importadas corregidas solo permiten enteros de 1 a 10.");
                    }

                    cell.Resolucion = cellUpdate.Resolucion?.Trim().ToLowerInvariant() ?? string.Empty;
                    cell.ValorFinal = cellUpdate.ValorFinal;
                }
            }

            revision = await RecomputeRevisionAsync(entity, revision, ct);
            entity.RevisionJson = JsonSerializer.Serialize(revision, JsonOptions);
            entity.ResumenAnalisisJson = JsonSerializer.Serialize(revision.Resumen, JsonOptions);
            entity.ResumenConfirmacionJson = JsonSerializer.Serialize(BuildConfirmacionResumen(revision), JsonOptions);
            entity.Estado = revision.PuedeConfirmar
                ? EstadoImportacionCalificaciones.ListaParaConfirmar
                : EstadoImportacionCalificaciones.EnRevision;
            entity.FechaUltimaActualizacion = DateTime.UtcNow;

            await _context.SaveChangesAsync(ct);
            return revision;
        }

        public async Task<ImportacionConfirmacionDto> GetConfirmacionAsync(Guid idImportacion, Guid idDocente, CancellationToken ct)
        {
            var entity = await GetOwnedImportacionAsync(idImportacion, idDocente, ct);
            var revision = GetRevisionOrThrow(entity);
            revision = await RecomputeRevisionAsync(entity, revision, ct);

            return new ImportacionConfirmacionDto
            {
                IdImportacionCalificaciones = entity.IdImportacionCalificaciones,
                Estado = entity.Estado.ToString(),
                Resumen = BuildConfirmacionResumen(revision),
                PuedeConfirmar = revision.PuedeConfirmar,
                Bloqueos = revision.Bloqueos,
            };
        }

        public async Task<ConfirmarImportacionCalificacionesResponseDto> ConfirmarAsync(Guid idImportacion, Guid idDocente, CancellationToken ct)
        {
            var entity = await GetOwnedImportacionAsync(idImportacion, idDocente, ct);
            if (entity.Estado == EstadoImportacionCalificaciones.Confirmada)
            {
                throw new InvalidOperationException("La importación ya fue confirmada.");
            }

            if (entity.Estado == EstadoImportacionCalificaciones.Cancelada)
            {
                throw new InvalidOperationException("La importación fue cancelada.");
            }

            if (entity.ArchivoTemporalContenido == null || entity.ArchivoTemporalContenido.Length == 0)
            {
                throw new InvalidOperationException("No se encontró el PDF temporal de esta importación.");
            }

            var revision = await RecomputeRevisionAsync(entity, GetRevisionOrThrow(entity), ct);
            if (!revision.PuedeConfirmar)
            {
                throw new ValidationException("La importación todavía tiene conflictos pendientes y no puede confirmarse.");
            }

            var cambios = BuildApplyChanges(revision);
            var finalPath = $"calificaciones/importaciones/{entity.IdImportacionCalificaciones}.pdf";
            var uploaded = false;

            try
            {
                await using (var stream = new MemoryStream(entity.ArchivoTemporalContenido, writable: false))
                {
                    await _storageService.SubirArchivoAsync(stream, finalPath, entity.ContentType, ct);
                    uploaded = true;
                }

                var result = await _writeService.ApplyChangesAsync(
                    new CalificacionesApplyRequest(
                        entity.IdEC,
                        entity.IdUsuario,
                        _currentUser.NombreCompleto,
                        OrigenCarga.Importacion,
                        entity.IdImportacionCalificaciones,
                        cambios),
                    ct);

                entity.Estado = EstadoImportacionCalificaciones.Confirmada;
                entity.RutaArchivoFinal = finalPath;
                entity.ArchivoTemporalContenido = null;
                entity.ResumenConfirmacionJson = JsonSerializer.Serialize(BuildConfirmacionResumen(revision), JsonOptions);
                entity.FechaConfirmacion = DateTime.UtcNow;
                entity.FechaUltimaActualizacion = entity.FechaConfirmacion.Value;

                await _context.SaveChangesAsync(ct);

                return new ConfirmarImportacionCalificacionesResponseDto
                {
                    IdImportacionCalificaciones = entity.IdImportacionCalificaciones,
                    Estado = entity.Estado.ToString(),
                    RutaArchivoFinal = _storageService.GetUrlPublica(finalPath),
                    CambiosAplicados = result.CambiosAplicados,
                    IdSesionAuditoria = result.SesionAuditoria?.IdSesionAuditoria,
                };
            }
            catch
            {
                if (uploaded)
                {
                    try
                    {
                        await _storageService.EliminarArchivoAsync(finalPath, ct);
                    }
                    catch
                    {
                        // Best-effort cleanup; the original error is the relevant one for the caller.
                    }
                }

                throw;
            }
        }

        public async Task CancelarAsync(Guid idImportacion, Guid idDocente, CancellationToken ct)
        {
            var entity = await GetOwnedImportacionAsync(idImportacion, idDocente, ct);
            if (entity.Estado == EstadoImportacionCalificaciones.Confirmada)
            {
                throw new InvalidOperationException("La importación ya fue confirmada y no puede cancelarse.");
            }

            entity.Estado = EstadoImportacionCalificaciones.Cancelada;
            entity.ArchivoTemporalContenido = null;
            entity.FechaUltimaActualizacion = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);
        }

        private async Task<ImportAnalysisResult> BuildAnalysisAsync(EspacioContext espacio, byte[] fileBytes, CancellationToken ct)
        {
            using var stream = new MemoryStream(fileBytes, writable: false);
            var parsed = _parser.Parse(stream);
            var normalizedText = CalificacionesDomainHelper.NormalizeText(parsed.FullText);
            var blockers = BuildContextBlockers(espacio, normalizedText);

            if (parsed.Rows.Count == 0)
            {
                throw new ValidationException("El PDF no contiene estudiantes detectables.");
            }

            var studentOptions = await LoadStudentOptionsAsync(espacio.IdCurso, ct);
            var instancias = await LoadInstanciasAsync(espacio.IdEC, ct);
            ValidateInstancias(instancias);

            var slotMap = BuildSlotMap(instancias);
            var activeCalificaciones = await LoadCalificacionesVigentesByEcAsync(espacio.IdEC, ct);

            var review = BuildInitialRevision(
                espacio,
                parsed.Rows,
                studentOptions,
                slotMap,
                activeCalificaciones,
                blockers);

            if (review.Rows.Count == 0)
            {
                throw new ValidationException("El PDF no contiene filas válidas para analizar.");
            }

            if (!review.Slots.Any(slot => slot.TieneNotasImportadas))
            {
                throw new ValidationException("El PDF no contiene al menos una nota interpretable para importar.");
            }

            foreach (var slot in review.Slots.Where(slot => slot.TieneNotasImportadas && !slot.TieneEstructuraPrevia))
            {
                blockers.Add(new ImportacionIssueDto
                {
                    Codigo = "estructura_faltante",
                    Severidad = "blocking",
                    Mensaje = $"Se detectaron notas para {slot.Label}, pero esa evaluación todavía no tiene la instancia evaluativa o el ArchivoIE necesarios en el sistema.",
                    SlotKey = slot.Label,
                });
            }

            review.Bloqueos = blockers;
            review.PuedeConfirmar = blockers.Count == 0 && review.Resumen.PendientesDeRevision == 0;
            review.Estado = review.PuedeConfirmar
                ? EstadoImportacionCalificaciones.ListaParaConfirmar.ToString()
                : EstadoImportacionCalificaciones.EnRevision.ToString();

            var estado = blockers.Count > 0
                ? EstadoImportacionCalificaciones.Analizada
                : review.PuedeConfirmar
                    ? EstadoImportacionCalificaciones.ListaParaConfirmar
                    : EstadoImportacionCalificaciones.EnRevision;

            return new ImportAnalysisResult(
                estado,
                review.Resumen,
                blockers,
                review,
                BuildConfirmacionResumen(review));
        }

        private static List<ImportacionIssueDto> BuildContextBlockers(EspacioContext espacio, string normalizedText)
        {
            var blockers = new List<ImportacionIssueDto>();
            blockers.AddRange(BuildFormatBlockers(normalizedText));

            var expectedMateria = CalificacionesDomainHelper.NormalizeText(espacio.NombreMateria);
            if (!normalizedText.Contains(expectedMateria))
            {
                blockers.Add(new ImportacionIssueDto
                {
                    Codigo = "materia_no_coincide",
                    Severidad = "blocking",
                    Mensaje = $"El espacio curricular detectado en el PDF no coincide con \"{espacio.NombreMateria}\".",
                });
            }

            if (!normalizedText.Contains(espacio.AnioLectivo.ToString()))
            {
                blockers.Add(new ImportacionIssueDto
                {
                    Codigo = "anio_lectivo_no_coincide",
                    Severidad = "blocking",
                    Mensaje = $"El ciclo lectivo del PDF no coincide con {espacio.AnioLectivo}.",
                });
            }

            var cursoVariants = BuildCourseVariants(espacio);
            if (!cursoVariants.Any(normalizedText.Contains))
            {
                blockers.Add(new ImportacionIssueDto
                {
                    Codigo = "curso_no_coincide",
                    Severidad = "blocking",
                    Mensaje = $"El PDF no coincide con {BuildExpectedCourseLabel(espacio)}.",
                });
            }

            return blockers;
        }

        private static List<ImportacionIssueDto> BuildFormatBlockers(string normalizedText)
        {
            var blockers = new List<ImportacionIssueDto>();

            if (!normalizedText.Contains("listado de calificaciones", StringComparison.Ordinal))
            {
                blockers.Add(new ImportacionIssueDto
                {
                    Codigo = "formato_no_cidi",
                    Severidad = "blocking",
                    Mensaje = "El archivo no parece ser el listado de calificaciones exportado desde CiDi.",
                });
            }

            if (!normalizedText.Contains("espacio curricular", StringComparison.Ordinal)
                || !normalizedText.Contains("curso", StringComparison.Ordinal)
                || !normalizedText.Contains("division", StringComparison.Ordinal)
                || !normalizedText.Contains("ciclo lectivo", StringComparison.Ordinal))
            {
                blockers.Add(new ImportacionIssueDto
                {
                    Codigo = "encabezado_incompleto",
                    Severidad = "blocking",
                    Mensaje = "El PDF no trae el encabezado esperado de CiDi con curso, división, espacio curricular y ciclo lectivo.",
                });
            }

            if (!normalizedText.Contains("estudiantes", StringComparison.Ordinal)
                || !normalizedText.Contains("eval 1", StringComparison.Ordinal)
                || !normalizedText.Contains("eval 8", StringComparison.Ordinal))
            {
                blockers.Add(new ImportacionIssueDto
                {
                    Codigo = "tabla_no_reconocida",
                    Severidad = "blocking",
                    Mensaje = "No se pudo reconocer la tabla estándar de calificaciones de CiDi.",
                });
            }

            return blockers;
        }

        private static List<string> BuildCourseVariants(EspacioContext espacio)
        {
            var codigoBase = espacio.CodigoCurso.Split('-', StringSplitOptions.RemoveEmptyEntries)[0];
            var yearText = BuildYearText(espacio.AnioNumero);
            return new List<string>
            {
                CalificacionesDomainHelper.NormalizeText($"{espacio.AnioNumero}{espacio.Division}"),
                CalificacionesDomainHelper.NormalizeText($"{espacio.AnioNumero} {espacio.Division}"),
                CalificacionesDomainHelper.NormalizeText(codigoBase),
                CalificacionesDomainHelper.NormalizeText(yearText),
                CalificacionesDomainHelper.NormalizeText($"{yearText} {espacio.Division}"),
                CalificacionesDomainHelper.NormalizeText($"curso {yearText}"),
                CalificacionesDomainHelper.NormalizeText($"curso {yearText} division {espacio.Division}"),
                CalificacionesDomainHelper.NormalizeText($"division {espacio.Division}"),
            }.Distinct().ToList();
        }

        private static string BuildYearText(int anioNumero)
        {
            return anioNumero switch
            {
                1 => "Primer Año",
                2 => "Segundo Año",
                3 => "Tercer Año",
                4 => "Cuarto Año",
                5 => "Quinto Año",
                6 => "Sexto Año",
                7 => "Séptimo Año",
                _ => $"{anioNumero} Año",
            };
        }

        private static string BuildExpectedCourseLabel(EspacioContext espacio)
            => $"{BuildYearText(espacio.AnioNumero)}, División {espacio.Division}";

        private static ImportacionRevisionDto BuildInitialRevision(
            EspacioContext espacio,
            IReadOnlyList<CidiCalificacionesParsedRow> parsedRows,
            IReadOnlyList<ImportacionStudentOptionDto> studentOptions,
            IReadOnlyDictionary<string, SlotDefinition> slotMap,
            IReadOnlyDictionary<(Guid StudentId, string SlotKey), int?> activeCalificaciones,
            List<ImportacionIssueDto> blockers)
        {
            var rows = new List<ImportacionRevisionRowDto>();
            var slots = BuildAllSlots(slotMap, parsedRows);

            foreach (var parsedRow in parsedRows)
            {
                var match = MatchStudent(parsedRow.StudentRaw, studentOptions);
                var row = new ImportacionRevisionRowDto
                {
                    RowId = $"row-{parsedRow.Order}",
                    Orden = parsedRow.Order,
                    EstudiantePdf = parsedRow.StudentRaw,
                    EstudianteAsociadoId = match.SelectedStudentId,
                    CandidatosEstudianteIds = match.CandidateIds.ToList(),
                    Issues = match.Issues.ToList(),
                };

                foreach (var slot in slots)
                {
                    parsedRow.Cells.TryGetValue(slot.SlotKey, out var rawValue);
                    var importedValue = TryParseImportedGrade(rawValue, out var invalidMessage);
                    var dbValue = row.EstudianteAsociadoId.HasValue
                        && activeCalificaciones.TryGetValue((row.EstudianteAsociadoId.Value, slot.SlotKey), out var currentValue)
                        ? currentValue
                        : null;

                    var cell = BuildInitialCell(slot, rawValue, importedValue, invalidMessage, dbValue, row.EstudianteAsociadoId.HasValue);
                    row.Cells.Add(cell);
                }

                rows.Add(row);
            }

            ApplyDuplicateConflicts(rows);
            RecomputeRowStates(rows);

            return new ImportacionRevisionDto
            {
                Estado = blockers.Count == 0
                    ? EstadoImportacionCalificaciones.EnRevision.ToString()
                    : EstadoImportacionCalificaciones.Analizada.ToString(),
                EstudiantesCurso = studentOptions.ToList(),
                Slots = slots,
                Rows = rows,
                Resumen = BuildResumen(rows, slots),
                PuedeConfirmar = blockers.Count == 0 && !HasPendingRows(rows),
                Bloqueos = blockers,
            };
        }

        private static ImportacionRevisionCellDto BuildInitialCell(
            ImportacionSlotDto slot,
            string? rawValue,
            int? importedValue,
            string? invalidMessage,
            int? dbValue,
            bool hasResolvedStudent)
        {
            var cell = new ImportacionRevisionCellDto
            {
                SlotKey = slot.SlotKey,
                EvaluacionNumero = slot.EvaluacionNumero,
                TipoCalificacion = slot.TipoCalificacion,
                ValorImportadoRaw = rawValue,
                ValorImportado = importedValue,
                ValorDb = dbValue,
                Editable = slot.TieneEstructuraPrevia,
                Resolucion = "omit",
                Estado = "clean",
            };

            if (!string.IsNullOrWhiteSpace(rawValue) && importedValue == null)
            {
                cell.Estado = "blocking";
                cell.Resolucion = "pending";
                cell.Mensaje = invalidMessage ?? "La nota detectada no es válida.";
                return cell;
            }

            if (importedValue == null)
            {
                if (dbValue != null)
                {
                    cell.Estado = "review";
                    cell.Resolucion = "pending";
                    cell.Mensaje = "En el sistema ya existe una nota vigente y el PDF viene vacío.";
                }

                return cell;
            }

            cell.ValorFinal = importedValue;
            if (!hasResolvedStudent)
            {
                cell.Estado = "blocking";
                cell.Resolucion = "pending";
                cell.Mensaje = "Asociá el estudiante o marcá la fila como omitida.";
                return cell;
            }

            if (dbValue == null)
            {
                cell.Estado = "clean";
                cell.Resolucion = "use_imported";
                return cell;
            }

            if (dbValue == importedValue)
            {
                cell.Estado = "clean";
                cell.Resolucion = "keep_db";
                return cell;
            }

            cell.Estado = "review";
            cell.Resolucion = "pending";
            cell.Mensaje = $"El PDF trae {importedValue} y el sistema ya tiene {dbValue}.";
            return cell;
        }

        private async Task<ImportacionRevisionDto> RecomputeRevisionAsync(
            ImportacionCalificaciones entity,
            ImportacionRevisionDto revision,
            CancellationToken ct)
        {
            var instancias = await LoadInstanciasAsync(entity.IdEC, ct);
            ValidateInstancias(instancias);

            var slotMap = BuildSlotMap(instancias);
            var activeCalificaciones = await LoadCalificacionesVigentesByEcAsync(entity.IdEC, ct);
            var validStudents = revision.EstudiantesCurso.ToDictionary(student => student.IdEstudiante);

            foreach (var row in revision.Rows)
            {
                row.Issues = row.Issues
                    .Where(issue => issue.Codigo.StartsWith("student_", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                var hasResolvedStudent = row.Omitida || (row.EstudianteAsociadoId.HasValue && validStudents.ContainsKey(row.EstudianteAsociadoId.Value));
                if (!row.Omitida && row.EstudianteAsociadoId.HasValue && !validStudents.ContainsKey(row.EstudianteAsociadoId.Value))
                {
                    throw new ValidationException($"El estudiante '{row.EstudianteAsociadoId}' ya no pertenece al curso del espacio curricular.");
                }

                foreach (var cell in row.Cells)
                {
                    cell.Editable = slotMap.TryGetValue(cell.SlotKey, out var slotDefinition) && slotDefinition.HasStructure;
                    cell.ValorDb = row.EstudianteAsociadoId.HasValue
                        && activeCalificaciones.TryGetValue((row.EstudianteAsociadoId.Value, cell.SlotKey), out var dbValue)
                            ? dbValue
                            : null;

                    if (row.Omitida)
                    {
                        cell.Estado = "clean";
                        cell.Resolucion = "omit";
                        cell.Mensaje = "Fila omitida en la importación.";
                        continue;
                    }

                    if (!cell.Editable && cell.ValorImportado != null)
                    {
                        cell.Estado = "blocking";
                        cell.Resolucion = "pending";
                        cell.Mensaje = "Falta el alta previa de la instancia evaluativa o del ArchivoIE para esta celda.";
                        continue;
                    }

                    if (!hasResolvedStudent && cell.ValorImportado != null)
                    {
                        cell.Estado = "blocking";
                        cell.Resolucion = "pending";
                        cell.Mensaje = "Asociá el estudiante o marcá la fila como omitida.";
                        continue;
                    }

                    if (cell.ValorImportado != null && cell.ValorDb == null)
                    {
                        if (cell.Resolucion is not ("use_imported" or "manual_edit" or "omit"))
                        {
                            cell.Resolucion = "use_imported";
                        }
                    }

                    if (cell.ValorImportado != null && cell.ValorDb != null && cell.ValorImportado == cell.ValorDb)
                    {
                        cell.Estado = "clean";
                        cell.Resolucion = "keep_db";
                        cell.ValorFinal = cell.ValorDb;
                        cell.Mensaje = null;
                        continue;
                    }

                    if (cell.ValorImportado == null && cell.ValorDb == null)
                    {
                        cell.Estado = "clean";
                        cell.Resolucion = "omit";
                        cell.ValorFinal = null;
                        cell.Mensaje = null;
                        continue;
                    }

                    if (cell.ValorImportado == null && cell.ValorDb != null)
                    {
                        ApplyDecision(cell, allowImportedFallback: false);
                        continue;
                    }

                    if (cell.ValorImportado != null && cell.ValorDb == null)
                    {
                        ApplyDecision(cell, allowImportedFallback: true);
                        continue;
                    }

                    ApplyDecision(cell, allowImportedFallback: true);
                }
            }

            ApplyDuplicateConflicts(revision.Rows);
            RecomputeRowStates(revision.Rows);
            revision.Resumen = BuildResumen(revision.Rows, revision.Slots);
            revision.PuedeConfirmar = revision.Bloqueos.Count == 0 && !HasPendingRows(revision.Rows);
            revision.Estado = revision.PuedeConfirmar
                ? EstadoImportacionCalificaciones.ListaParaConfirmar.ToString()
                : EstadoImportacionCalificaciones.EnRevision.ToString();
            return revision;
        }

        private static void ApplyDecision(ImportacionRevisionCellDto cell, bool allowImportedFallback)
        {
            var resolution = cell.Resolucion?.Trim().ToLowerInvariant() ?? string.Empty;
            switch (resolution)
            {
                case "keep_db":
                    cell.Estado = "clean";
                    cell.ValorFinal = cell.ValorDb;
                    cell.Mensaje = null;
                    return;
                case "omit":
                    cell.Estado = "clean";
                    cell.ValorFinal = null;
                    cell.Mensaje = null;
                    return;
                case "use_imported":
                    if (!allowImportedFallback || cell.ValorImportado == null)
                    {
                        cell.Estado = "review";
                        cell.Resolucion = "pending";
                        cell.Mensaje = "La celda requiere una decisión antes de confirmar.";
                        return;
                    }

                    cell.Estado = "clean";
                    cell.ValorFinal = cell.ValorImportado;
                    cell.Mensaje = null;
                    return;
                case "manual_edit":
                    if (cell.ValorFinal is < 1 or > 10)
                    {
                        cell.Estado = "blocking";
                        cell.Resolucion = "pending";
                        cell.Mensaje = "Ingresá una nota válida entre 1 y 10.";
                        return;
                    }

                    cell.Estado = "clean";
                    cell.Mensaje = null;
                    return;
                default:
                    cell.Estado = cell.ValorDb != null || cell.ValorImportado != null ? "review" : "clean";
                    cell.Resolucion = cell.Estado == "review" ? "pending" : "omit";
                    cell.Mensaje = cell.Estado == "review"
                        ? "La celda requiere una decisión antes de confirmar."
                        : null;
                    return;
            }
        }

        private static void ApplyDuplicateConflicts(List<ImportacionRevisionRowDto> rows)
        {
            var duplicatedRows = rows
                .Where(row => !row.Omitida && row.EstudianteAsociadoId.HasValue)
                .GroupBy(row => row.EstudianteAsociadoId!.Value)
                .Where(group => group.Count() > 1)
                .ToList();

            foreach (var duplicateGroup in duplicatedRows)
            {
                var cellGroups = duplicateGroup
                    .SelectMany(row => row.Cells.Where(cell => !string.Equals(cell.Resolucion, "omit", StringComparison.OrdinalIgnoreCase))
                        .Select(cell => new { Row = row, Cell = cell }))
                    .GroupBy(item => item.Cell.SlotKey);

                foreach (var cellGroup in cellGroups)
                {
                    var finalValues = cellGroup
                        .Select(item => item.Cell.Resolucion == "keep_db" ? item.Cell.ValorDb : item.Cell.ValorFinal)
                        .Where(value => value != null)
                        .Distinct()
                        .ToList();

                    if (finalValues.Count > 1)
                    {
                        foreach (var item in cellGroup)
                        {
                            item.Cell.Estado = "blocking";
                            item.Cell.Resolucion = "pending";
                            item.Cell.Mensaje = "El mismo estudiante aparece repetido con valores distintos en esta celda.";
                        }
                    }
                    else
                    {
                        foreach (var row in duplicateGroup)
                        {
                            row.Issues.Add(new ImportacionIssueDto
                            {
                                Codigo = "student_duplicate_pdf",
                                Severidad = "review",
                                Mensaje = "El mismo estudiante aparece más de una vez en el PDF con notas compatibles.",
                            });
                        }
                    }
                }
            }
        }

        private static void RecomputeRowStates(List<ImportacionRevisionRowDto> rows)
        {
            foreach (var row in rows)
            {
                if (row.Omitida)
                {
                    row.Estado = "clean";
                    row.Mensaje = "Fila omitida.";
                    continue;
                }

                if (!row.EstudianteAsociadoId.HasValue)
                {
                    row.Estado = "blocking";
                    row.Mensaje = "Debés asociar un estudiante o marcar la fila como omitida.";
                    continue;
                }

                var hasBlockingIssue = row.Issues.Any(issue => issue.Severidad == "blocking")
                    || row.Cells.Any(cell => cell.Estado == "blocking");
                if (hasBlockingIssue)
                {
                    row.Estado = "blocking";
                    row.Mensaje = "La fila tiene conflictos bloqueantes pendientes.";
                    continue;
                }

                var hasReviewIssue = row.Issues.Any(issue => issue.Severidad == "review")
                    || row.Cells.Any(cell => cell.Resolucion == "pending" || cell.Estado == "review");
                row.Estado = hasReviewIssue ? "review" : "clean";
                row.Mensaje = hasReviewIssue
                    ? "La fila requiere revisión antes de confirmar."
                    : null;
            }
        }

        private static bool HasPendingRows(IEnumerable<ImportacionRevisionRowDto> rows)
        {
            return rows.Any(row =>
                !row.Omitida
                && (row.Estado != "clean"
                    || row.Cells.Any(cell => cell.Resolucion == "pending" || cell.Estado == "blocking")));
        }

        private static ImportacionAnalisisResumenDto BuildResumen(
            IReadOnlyList<ImportacionRevisionRowDto> rows,
            IReadOnlyList<ImportacionSlotDto> slots)
        {
            var noteCells = rows.SelectMany(row => row.Cells.Select(cell => new { Row = row, Cell = cell })).ToList();

            return new ImportacionAnalisisResumenDto
            {
                EstudiantesDetectados = rows.Count,
                EstudiantesSinConflicto = rows.Count(row => row.Estado == "clean"),
                EstudiantesConConflicto = rows.Count(row => row.Estado != "clean"),
                EvaluacionesDetectadasConNotas = slots.Count(slot => slot.TieneNotasImportadas),
                NotasNuevas = noteCells.Count(item => !item.Row.Omitida && item.Cell.ValorImportado != null && item.Cell.ValorDb == null),
                NotasYaExistentes = noteCells.Count(item => !item.Row.Omitida && item.Cell.ValorDb != null),
                ConflictosDeNotas = noteCells.Count(item => item.Cell.Estado != "clean" && item.Cell.ValorImportadoRaw != null),
                NotasInvalidas = noteCells.Count(item => !string.IsNullOrWhiteSpace(item.Cell.ValorImportadoRaw) && item.Cell.ValorImportado == null),
                PendientesDeRevision = noteCells.Count(item => item.Cell.Resolucion == "pending") + rows.Count(row => row.Estado == "blocking"),
            };
        }

        private static ImportacionConfirmacionResumenDto BuildConfirmacionResumen(ImportacionRevisionDto revision)
        {
            var noteCells = revision.Rows
                .Where(row => !row.Omitida && row.EstudianteAsociadoId.HasValue)
                .SelectMany(row => row.Cells)
                .ToList();

            return new ImportacionConfirmacionResumenDto
            {
                EstudiantesValidados = revision.Rows.Count(row => !row.Omitida && row.EstudianteAsociadoId.HasValue),
                NotasNuevas = noteCells.Count(cell => cell.Resolucion is "use_imported" or "manual_edit" && cell.ValorDb == null && cell.ValorFinal != null),
                NotasExistentesMantenidas = noteCells.Count(cell => cell.Resolucion == "keep_db"),
                NotasReemplazadas = noteCells.Count(cell => cell.Resolucion is "use_imported" or "manual_edit" && cell.ValorDb != null && cell.ValorFinal != null && cell.ValorDb != cell.ValorFinal),
                CorreccionesManuales = noteCells.Count(cell => cell.Resolucion == "manual_edit"),
                NotasOmitidas = revision.Rows.Count(row => row.Omitida) + noteCells.Count(cell => cell.Resolucion == "omit"),
            };
        }

        private static IReadOnlyCollection<CalificacionApplyChange> BuildApplyChanges(ImportacionRevisionDto revision)
        {
            var slotByKey = revision.Slots.ToDictionary(slot => slot.SlotKey);
            var grouped = revision.Rows
                .Where(row => !row.Omitida && row.EstudianteAsociadoId.HasValue)
                .SelectMany(row => row.Cells
                    .Where(cell => cell.Resolucion is "use_imported" or "manual_edit" or "keep_db")
                    .Select(cell => new
                    {
                        row.EstudianteAsociadoId,
                        row.RowId,
                        Cell = cell,
                    }))
                .Where(item => item.Cell.ValorFinal != null)
                .GroupBy(item => (item.EstudianteAsociadoId!.Value, item.Cell.SlotKey));

            var changes = new List<CalificacionApplyChange>();
            foreach (var group in grouped)
            {
                var chosen = group
                    .OrderByDescending(item => item.Cell.Resolucion == "manual_edit")
                    .ThenBy(item => item.RowId)
                    .First()
                    .Cell;

                if (chosen.ValorDb == chosen.ValorFinal || chosen.ValorFinal == null)
                {
                    continue;
                }

                if (!CalificacionesDomainHelper.TryParseSlotKey(chosen.SlotKey, out var evaluacionNumero, out var tipoCalificacion))
                {
                    throw new ValidationException($"El slot '{chosen.SlotKey}' no es válido.");
                }

                if (!slotByKey.TryGetValue(chosen.SlotKey, out var slot) || !slot.IdIE.HasValue)
                {
                    throw new ValidationException($"El slot '{chosen.SlotKey}' no tiene una instancia evaluativa válida para confirmar.");
                }

                changes.Add(new CalificacionApplyChange(
                    slot.IdIE.Value,
                    group.Key.Value,
                    tipoCalificacion,
                    chosen.ValorFinal));
            }

            return changes;
        }

        private async Task<Dictionary<(Guid StudentId, string SlotKey), int?>> LoadCalificacionesVigentesByEcAsync(Guid idEC, CancellationToken ct)
        {
            var instancias = await _context.InstanciasEvaluativas
                .AsNoTracking()
                .Where(i => i.IdEC == idEC)
                .Select(i => new { i.IdIE, i.Nro })
                .ToListAsync(ct);

            var instanciasById = instancias.ToDictionary(i => i.IdIE, i => i.Nro);
            var calificaciones = await _context.Calificaciones
                .AsNoTracking()
                .Where(c => c.Habilitada && instanciasById.Keys.Contains(c.IdIE))
                .ToListAsync(ct);

            return calificaciones.ToDictionary(
                calificacion => (calificacion.IdEstudiante, CalificacionesDomainHelper.BuildSlotKey(instanciasById[calificacion.IdIE], calificacion.TipoCalificacion)),
                calificacion => calificacion.Puntaje);
        }

        private async Task<List<ImportacionStudentOptionDto>> LoadStudentOptionsAsync(Guid idCurso, CancellationToken ct)
        {
            return await _context.DetallesCursado
                .AsNoTracking()
                .Where(dc => dc.IdCurso == idCurso && dc.Estado)
                .OrderBy(dc => dc.Estudiante.Apellido)
                .ThenBy(dc => dc.Estudiante.Nombre)
                .Select(dc => new ImportacionStudentOptionDto
                {
                    IdEstudiante = dc.IdEstudiante,
                    Label = $"{dc.Estudiante.Apellido}, {dc.Estudiante.Nombre}",
                    Documento = dc.Estudiante.Documento,
                })
                .ToListAsync(ct);
        }

        private async Task<List<InstanciaReadModel>> LoadInstanciasAsync(Guid idEC, CancellationToken ct)
        {
            return await _context.InstanciasEvaluativas
                .AsNoTracking()
                .Where(i => i.IdEC == idEC)
                .OrderBy(i => i.Nro)
                .Select(i => new InstanciaReadModel(
                    i.IdIE,
                    i.Nro,
                    i.Archivos
                        .Where(a => a.Habilitada)
                        .Select(a => new ArchivoReadModel(a.IdArchivoIE, a.TipoCalificacion))
                        .ToList()))
                .ToListAsync(ct);
        }

        private static Dictionary<string, SlotDefinition> BuildSlotMap(IEnumerable<InstanciaReadModel> instancias)
        {
            var map = Enumerable.Range(1, 8)
                .SelectMany(nro => new[]
                {
                    new SlotDefinition(CalificacionesDomainHelper.BuildSlotKey(nro, TipoCalificacion.NotaOriginal), nro, "N", false, null),
                    new SlotDefinition(CalificacionesDomainHelper.BuildSlotKey(nro, TipoCalificacion.Recuperatorio1), nro, "R1", false, null),
                    new SlotDefinition(CalificacionesDomainHelper.BuildSlotKey(nro, TipoCalificacion.Recuperatorio2), nro, "R2", false, null),
                })
                .ToDictionary(slot => slot.SlotKey);

            foreach (var instancia in instancias)
            {
                foreach (var archivo in instancia.Archivos)
                {
                    var slotKey = CalificacionesDomainHelper.BuildSlotKey(instancia.Nro, archivo.TipoCalificacion);
                    map[slotKey] = new SlotDefinition(slotKey, instancia.Nro, CalificacionesDomainHelper.ToTipoCalificacionCode(archivo.TipoCalificacion), true, instancia.IdIE);
                }
            }

            return map;
        }

        private static List<ImportacionSlotDto> BuildAllSlots(
            IReadOnlyDictionary<string, SlotDefinition> slotMap,
            IEnumerable<CidiCalificacionesParsedRow> rows)
        {
            var importedSlots = rows
                .SelectMany(row => row.Cells)
                .Where(cell => !string.IsNullOrWhiteSpace(cell.Value))
                .Select(cell => cell.Key)
                .ToHashSet();

            return slotMap.Values
                .OrderBy(slot => slot.EvaluacionNumero)
                .ThenBy(slot => slot.TipoCalificacionCode switch { "N" => 0, "R1" => 1, _ => 2 })
                .Select(slot => new ImportacionSlotDto
                {
                    SlotKey = slot.SlotKey,
                    IdIE = slot.IdIE,
                    EvaluacionNumero = slot.EvaluacionNumero,
                    TipoCalificacion = slot.TipoCalificacionCode,
                    Label = $"Eval {slot.EvaluacionNumero} / {slot.TipoCalificacionCode}",
                    TieneNotasImportadas = importedSlots.Contains(slot.SlotKey),
                    TieneEstructuraPrevia = slot.HasStructure,
                })
                .ToList();
        }

        private static StudentMatchResult MatchStudent(string rawStudent, IReadOnlyList<ImportacionStudentOptionDto> options)
        {
            var trimmedRaw = rawStudent.Trim();
            var normalizedRaw = CalificacionesDomainHelper.NormalizeText(trimmedRaw);

            var literalMatches = options
                .Where(option => option.Label.Trim().Equals(trimmedRaw, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (literalMatches.Count == 1)
            {
                return new StudentMatchResult(
                    literalMatches[0].IdEstudiante,
                    Array.Empty<Guid>(),
                    Array.Empty<ImportacionIssueDto>());
            }

            var normalizedMatches = options
                .Where(option => CalificacionesDomainHelper.NormalizeText(option.Label) == normalizedRaw)
                .ToList();

            if (normalizedMatches.Count == 1)
            {
                return new StudentMatchResult(
                    normalizedMatches[0].IdEstudiante,
                    new[] { normalizedMatches[0].IdEstudiante },
                    new[]
                    {
                        new ImportacionIssueDto
                        {
                            Codigo = "student_normalized_match",
                            Severidad = "review",
                            Mensaje = "Coincidencia encontrada por nombre normalizado.",
                        },
                    });
            }

            var scoredCandidates = options
                .Select(option => new
                {
                    option.IdEstudiante,
                    Score = ScoreStudentCandidate(normalizedRaw, CalificacionesDomainHelper.NormalizeText(option.Label)),
                })
                .Where(item => item.Score > 0)
                .OrderByDescending(item => item.Score)
                .Take(5)
                .ToList();

            if (scoredCandidates.Count == 0)
            {
                return new StudentMatchResult(
                    null,
                    Array.Empty<Guid>(),
                    new[]
                    {
                        new ImportacionIssueDto
                        {
                            Codigo = "student_not_found",
                            Severidad = "blocking",
                            Mensaje = "No se encontró un estudiante del curso que coincida con la fila del PDF.",
                        },
                    });
            }

            return new StudentMatchResult(
                null,
                scoredCandidates.Select(candidate => candidate.IdEstudiante).ToArray(),
                new[]
                {
                    new ImportacionIssueDto
                    {
                        Codigo = "student_ambiguous",
                        Severidad = "blocking",
                        Mensaje = "La fila del PDF requiere que el docente seleccione manualmente el estudiante.",
                    },
                });
        }

        private static int ScoreStudentCandidate(string normalizedPdfStudent, string normalizedCandidate)
        {
            if (string.IsNullOrWhiteSpace(normalizedPdfStudent) || string.IsNullOrWhiteSpace(normalizedCandidate))
            {
                return 0;
            }

            var pdfTokens = normalizedPdfStudent.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var candidateTokens = normalizedCandidate.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var overlap = pdfTokens.Intersect(candidateTokens).Count();

            if (normalizedCandidate.Contains(normalizedPdfStudent, StringComparison.Ordinal))
            {
                overlap += 2;
            }

            if (normalizedPdfStudent.Contains(normalizedCandidate, StringComparison.Ordinal))
            {
                overlap += 1;
            }

            return overlap;
        }

        private static int? TryParseImportedGrade(string? rawValue, out string? invalidMessage)
        {
            invalidMessage = null;
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return null;
            }

            var compact = rawValue.Trim();
            if (!int.TryParse(compact, out var grade))
            {
                invalidMessage = $"El valor '{rawValue}' no es una nota numérica válida.";
                return null;
            }

            if (grade is < 1 or > 10)
            {
                invalidMessage = $"La nota '{rawValue}' está fuera del rango permitido 1..10.";
                return null;
            }

            return grade;
        }

        private async Task<ImportacionCalificaciones> GetOwnedImportacionAsync(Guid idImportacion, Guid idDocente, CancellationToken ct)
        {
            var entity = await _context.ImportacionesCalificaciones
                .FirstOrDefaultAsync(i => i.IdImportacionCalificaciones == idImportacion, ct)
                ?? throw new KeyNotFoundException("Importación no encontrada.");

            await GetEspacioContextAsync(entity.IdEC, idDocente, ct);
            return entity;
        }

        private ImportacionRevisionDto GetRevisionOrThrow(ImportacionCalificaciones entity)
        {
            if (string.IsNullOrWhiteSpace(entity.RevisionJson))
            {
                throw new InvalidOperationException("La importación no tiene una revisión editable disponible.");
            }

            return JsonSerializer.Deserialize<ImportacionRevisionDto>(entity.RevisionJson, JsonOptions)
                ?? throw new InvalidOperationException("No se pudo reconstruir la revisión de la importación.");
        }

        private async Task<ImportacionCalificacionesDetalleDto> MapDetalleAsync(ImportacionCalificaciones entity, CancellationToken ct, bool tieneSesionPendiente)
        {
            var espacio = await GetEspacioContextAsync(entity.IdEC, entity.IdDocente, ct);
            var resumen = JsonSerializer.Deserialize<ImportacionAnalisisResumenDto>(entity.ResumenAnalisisJson, JsonOptions) ?? new ImportacionAnalisisResumenDto();
            var revision = string.IsNullOrWhiteSpace(entity.RevisionJson)
                ? null
                : JsonSerializer.Deserialize<ImportacionRevisionDto>(entity.RevisionJson, JsonOptions);
            var bloqueos = revision?.Bloqueos ?? new List<ImportacionIssueDto>();

            return new ImportacionCalificacionesDetalleDto
            {
                IdImportacionCalificaciones = entity.IdImportacionCalificaciones,
                Estado = entity.Estado.ToString(),
                NombreArchivoOriginal = entity.NombreArchivoOriginal,
                RutaArchivoFinal = string.IsNullOrWhiteSpace(entity.RutaArchivoFinal)
                    ? null
                    : _storageService.GetUrlPublica(entity.RutaArchivoFinal),
                FechaCreacion = entity.FechaCreacion,
                FechaUltimaActualizacion = entity.FechaUltimaActualizacion,
                FechaConfirmacion = entity.FechaConfirmacion,
                TieneSesionPendiente = tieneSesionPendiente,
                PuedeRevisar = revision != null && bloqueos.Count == 0,
                PuedeConfirmar = revision?.PuedeConfirmar ?? false,
                Resumen = resumen,
                Bloqueos = bloqueos,
                Contexto = new ImportacionContextoDto
                {
                    IdEC = espacio.IdEC,
                    IdCurso = espacio.IdCurso,
                    NombreMateria = espacio.NombreMateria,
                    CodigoCurso = espacio.CodigoCurso,
                    AnioNumero = espacio.AnioNumero,
                    Division = espacio.Division,
                    AnioLectivo = espacio.AnioLectivo,
                },
            };
        }

        private async Task<EspacioContext> GetEspacioContextAsync(Guid idEC, Guid idDocente, CancellationToken ct)
        {
            var espacio = await _context.EspaciosCurriculares
                .AsNoTracking()
                .Where(ec => ec.IdEC == idEC)
                .Select(ec => new EspacioContext(
                    ec.IdEC,
                    ec.IdCurso,
                    ec.IdDocente,
                    ec.Curricula.Nombre,
                    ec.Curso.Codigo,
                    ec.Curso.Anio.Numero,
                    ec.Curso.Division.Nombre.ToString(),
                    ec.Curso.AñoLectivo.Year))
                .FirstOrDefaultAsync(ct)
                ?? throw new KeyNotFoundException("Espacio curricular no encontrado.");

            if (espacio.IdDocente != idDocente)
            {
                throw new UnauthorizedAccessException("No sos el docente titular de este espacio curricular.");
            }

            return espacio;
        }

        private static async Task<byte[]> ReadFileBytesAsync(Microsoft.AspNetCore.Http.IFormFile file, CancellationToken ct)
        {
            await using var input = file.OpenReadStream();
            using var memory = new MemoryStream();
            await input.CopyToAsync(memory, ct);
            return memory.ToArray();
        }

        private static string ComputeSha256(byte[] fileBytes)
        {
            using var sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(fileBytes));
        }

        private static void ValidateInstancias(List<InstanciaReadModel> instancias)
        {
            if (instancias.Count > 8)
            {
                throw new InvalidOperationException("El espacio curricular tiene más de 8 instancias evaluativas registradas para el año lectivo.");
            }

            if (instancias.Any(i => i.Nro is < 1 or > 8))
            {
                throw new InvalidOperationException("Se detectaron instancias evaluativas con un número fuera del rango permitido 1..8.");
            }

            if (instancias.GroupBy(i => i.Nro).Any(group => group.Count() > 1))
            {
                throw new InvalidOperationException("Se detectaron instancias evaluativas duplicadas para el mismo número.");
            }
        }

        private sealed record EspacioContext(
            Guid IdEC,
            Guid IdCurso,
            Guid? IdDocente,
            string NombreMateria,
            string CodigoCurso,
            int AnioNumero,
            string Division,
            int AnioLectivo);

        private sealed record InstanciaReadModel(Guid IdIE, int Nro, List<ArchivoReadModel> Archivos);
        private sealed record ArchivoReadModel(Guid IdArchivoIE, TipoCalificacion TipoCalificacion);
        private sealed record SlotDefinition(string SlotKey, int EvaluacionNumero, string TipoCalificacionCode, bool HasStructure, Guid? IdIE);
        private sealed record StudentMatchResult(Guid? SelectedStudentId, IReadOnlyList<Guid> CandidateIds, IReadOnlyList<ImportacionIssueDto> Issues);
        private sealed record ImportAnalysisResult(
            EstadoImportacionCalificaciones Estado,
            ImportacionAnalisisResumenDto Resumen,
            List<ImportacionIssueDto> Bloqueos,
            ImportacionRevisionDto? Revision,
            ImportacionConfirmacionResumenDto Confirmacion);
    }
}
