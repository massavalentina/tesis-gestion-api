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

        public async Task<ImportacionCalificacionesAnalisisDto> AnalizarAsync(
            Guid idEC,
            Guid idDocente,
            AnalizarImportacionCalificacionesDto dto,
            CancellationToken ct)
        {
            ValidatePdfInput(dto.Archivo);

            var espacio = await GetEspacioContextAsync(idEC, idDocente, ct);
            var fileBytes = await ReadFileBytesAsync(dto.Archivo, ct);
            var hash = ComputeSha256(fileBytes);

            return await BuildAnalysisAsync(espacio, dto.Archivo.FileName, hash, fileBytes, ct);
        }

        public async Task<ConfirmarImportacionCalificacionesResponseDto> ConfirmarAsync(
            Guid idEC,
            Guid idDocente,
            ConfirmarImportacionCalificacionesDto dto,
            CancellationToken ct)
        {
            ValidatePdfInput(dto.Archivo);

            var payload = ParsePayload(dto.PayloadJson);
            var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException("Su sesión no es válida. Vuelva a ingresar para continuar.");
            var espacio = await GetEspacioContextAsync(idEC, idDocente, ct);
            var fileBytes = await ReadFileBytesAsync(dto.Archivo, ct);
            var hash = ComputeSha256(fileBytes);

            if (!string.Equals(hash, payload.HashArchivoSha256?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("El archivo cambió desde el análisis inicial. Vuelva a analizar el PDF antes de confirmar.");
            }

            var analysis = await BuildAnalysisAsync(espacio, dto.Archivo.FileName, hash, fileBytes, ct);
            var expectedBaseIds = ApplyPayloadToAnalysis(analysis, payload);
            analysis = await RecomputeAnalysisAsync(analysis, espacio.IdEC, ct);
            ValidateConcurrencySnapshot(analysis, expectedBaseIds);
            ValidateFinalCellResolutions(analysis);

            if (analysis.Bloqueos.Count > 0 || !analysis.PuedeConfirmar)
            {
                throw new ValidationException("Todavía quedan conflictos o asociaciones pendientes por resolver antes de confirmar la importación.");
            }

            var applyPlan = BuildApplyPlan(analysis);
            var now = DateTime.UtcNow;
            var idImportacion = Guid.NewGuid();
            var finalPath = $"calificaciones/importaciones/{idImportacion}.pdf";
            var importacion = new ImportacionCalificaciones
            {
                IdImportacionCalificaciones = idImportacion,
                IdEC = espacio.IdEC,
                IdCurso = espacio.IdCurso,
                IdDocente = idDocente,
                IdUsuario = userId,
                AnioLectivo = espacio.AnioLectivo,
                Estado = EstadoImportacionCalificaciones.Confirmada,
                NombreArchivoOriginal = dto.Archivo.FileName,
                ContentType = dto.Archivo.ContentType,
                TamanioArchivoBytes = dto.Archivo.Length,
                HashArchivoSha256 = hash,
                MotorLectura = "PdfPig",
                RutaArchivoFinal = finalPath,
                ResumenAnalisisJson = JsonSerializer.Serialize(analysis.Resumen, JsonOptions),
                ResumenConfirmacionJson = JsonSerializer.Serialize(BuildConfirmacionResumen(analysis.Rows), JsonOptions),
                FechaCreacion = now,
                FechaUltimaActualizacion = now,
                FechaConfirmacion = now,
            };

            _context.ImportacionesCalificaciones.Add(importacion);

            var uploaded = false;
            try
            {
                await using (var stream = new MemoryStream(fileBytes, writable: false))
                {
                    await _storageService.SubirArchivoAsync(stream, finalPath, dto.Archivo.ContentType, ct);
                    uploaded = true;
                }

                CalificacionesApplyResult result;
                if (applyPlan.Cambios.Count == 0 && applyPlan.ConflictosConservados.Count == 0)
                {
                    await _context.SaveChangesAsync(ct);
                    result = new CalificacionesApplyResult(0, Array.Empty<Guid>(), null);
                }
                else
                {
                    result = await _writeService.ApplyChangesAsync(
                        new CalificacionesApplyRequest(
                            espacio.IdEC,
                            userId,
                            _currentUser.NombreCompleto,
                            OrigenCarga.Importacion,
                            idImportacion,
                            applyPlan.Cambios,
                            applyPlan.ConflictosConservados),
                        ct);
                }

                return new ConfirmarImportacionCalificacionesResponseDto
                {
                    IdImportacionCalificaciones = idImportacion,
                    Estado = EstadoImportacionCalificaciones.Confirmada.ToString(),
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
                    }
                }

                throw;
            }
        }

        private async Task<ImportacionCalificacionesAnalisisDto> BuildAnalysisAsync(
            EspacioContext espacio,
            string nombreArchivoOriginal,
            string hashArchivoSha256,
            byte[] fileBytes,
            CancellationToken ct)
        {
            using var stream = new MemoryStream(fileBytes, writable: false);
            var parsed = _parser.Parse(stream);
            var normalizedText = CalificacionesDomainHelper.NormalizeText(parsed.FullText);
            var bloqueos = BuildContextBlockers(espacio, normalizedText);

            if (parsed.Rows.Count == 0)
            {
                throw new ValidationException("No se encontraron estudiantes en el PDF.");
            }

            var estudiantesCurso = await LoadStudentOptionsAsync(espacio.IdCurso, ct);
            var instancias = await LoadInstanciasAsync(espacio.IdEC, ct);
            ValidateInstancias(instancias);

            var slotMap = BuildSlotMap(instancias);
            var calificacionesVigentes = await LoadCalificacionesVigentesByEcAsync(espacio.IdEC, ct);
            var slots = BuildAllSlots(slotMap, parsed.Rows);
            var rows = BuildInitialRows(parsed.Rows, estudiantesCurso, slots, slotMap, calificacionesVigentes);

            if (rows.Count == 0)
            {
                throw new ValidationException("No se encontraron filas válidas para analizar en el PDF.");
            }

            if (!slots.Any(slot => slot.TieneNotasImportadas))
            {
                throw new ValidationException("No se encontraron notas para importar en el PDF.");
            }

            foreach (var slot in slots.Where(slot => slot.TieneNotasImportadas && !slot.TieneEstructuraPrevia))
            {
                bloqueos.Add(new ImportacionIssueDto
                {
                    Codigo = "estructura_faltante",
                    Severidad = "blocking",
                    Mensaje = $"Se encontraron notas para {slot.Label}, pero la evaluación correspondiente todavía no está cargada en la sección Evaluaciones.",
                    SlotKey = slot.SlotKey,
                });
            }

            foreach (var slot in slots.Where(slot => slot.TieneNotasImportadas && slot.TieneEstructuraPrevia && !slot.AdmiteCargaNotas))
            {
                bloqueos.Add(new ImportacionIssueDto
                {
                    Codigo = "evaluacion_no_evaluada",
                    Severidad = "blocking",
                    Mensaje = $"Se encontraron notas para {slot.Label}, pero aún no ha sido evaluada. Revise la sección de Evaluaciones.",
                    SlotKey = slot.SlotKey,
                });
            }

            ApplyDuplicateConflicts(rows);
            RecomputeRowStates(rows);
            PromoteUnrecoverableRowBlockers(rows, bloqueos);

            return new ImportacionCalificacionesAnalisisDto
            {
                Estado = bloqueos.Count > 0
                    ? EstadoImportacionCalificaciones.Analizada.ToString()
                    : HasPendingRows(rows)
                        ? EstadoImportacionCalificaciones.EnRevision.ToString()
                        : EstadoImportacionCalificaciones.ListaParaConfirmar.ToString(),
                NombreArchivoOriginal = nombreArchivoOriginal,
                HashArchivoSha256 = hashArchivoSha256,
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
                Resumen = BuildResumen(rows, slots),
                Bloqueos = bloqueos,
                EstudiantesCurso = estudiantesCurso,
                Slots = slots,
                Rows = rows,
                ResumenConfirmacionInicial = BuildConfirmacionResumen(rows),
                PuedeConfirmar = bloqueos.Count == 0 && !HasPendingRows(rows),
            };
        }

        private static void ValidatePdfInput(Microsoft.AspNetCore.Http.IFormFile? archivo)
        {
            if (archivo == null || archivo.Length == 0)
            {
                throw new ValidationException("Seleccione el PDF exportado desde CiDi para continuar.");
            }

            if (!archivo.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase)
                && !archivo.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                throw new ValidationException("El archivo elegido no es un PDF. Cargue el PDF exportado desde CiDi.");
            }

            if (archivo.Length > 50 * 1024 * 1024)
            {
                throw new ValidationException("El archivo supera el tamaño permitido. Intente con un PDF más liviano.");
            }
        }

        private static ConfirmarImportacionCalificacionesPayloadDto ParsePayload(string payloadJson)
        {
            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                throw new ValidationException("No se recibieron las decisiones de revisión para confirmar la importación.");
            }

            try
            {
                return JsonSerializer.Deserialize<ConfirmarImportacionCalificacionesPayloadDto>(payloadJson, JsonOptions)
                    ?? throw new ValidationException("No se pudo interpretar el payload de confirmación.");
            }
            catch (JsonException)
            {
                throw new ValidationException("No se pudo interpretar el payload de confirmación.");
            }
        }

        private static Dictionary<(string RowId, string SlotKey), Guid?> ApplyPayloadToAnalysis(
            ImportacionCalificacionesAnalisisDto analysis,
            ConfirmarImportacionCalificacionesPayloadDto payload)
        {
            if (payload.Rows.Count != analysis.Rows.Count)
            {
                throw new ValidationException("El análisis quedó desactualizado. Vuelva a analizar el PDF antes de confirmar.");
            }

            var rowsById = analysis.Rows.ToDictionary(row => row.RowId);
            var expectedBaseIds = new Dictionary<(string RowId, string SlotKey), Guid?>();

            foreach (var rowPayload in payload.Rows)
            {
                if (!rowsById.TryGetValue(rowPayload.RowId, out var row))
                {
                    throw new ValidationException("No se pudo reconocer una de las filas del análisis. Vuelva a analizar el PDF.");
                }

                if (rowPayload.Cells.Count != row.Cells.Count)
                {
                    throw new ValidationException("No se pudo reconocer una de las celdas del análisis. Vuelva a analizar el PDF.");
                }

                if (row.RequiereAsociacionManual)
                {
                    if (rowPayload.EstudianteAsociadoId.HasValue && !row.CandidatosEstudianteIds.Contains(rowPayload.EstudianteAsociadoId.Value))
                    {
                        throw new ValidationException("La asociación manual elegida no coincide con los homónimos detectados en el curso.");
                    }

                    row.EstudianteAsociadoId = rowPayload.EstudianteAsociadoId;
                }
                else if (rowPayload.EstudianteAsociadoId != row.EstudianteAsociadoId)
                {
                    throw new ValidationException("Solo se permite resolver manualmente filas con homónimos reales.");
                }

                var cellsBySlot = row.Cells.ToDictionary(cell => cell.SlotKey);
                foreach (var cellPayload in rowPayload.Cells)
                {
                    if (!cellsBySlot.TryGetValue(cellPayload.SlotKey, out var cell))
                    {
                        throw new ValidationException("No se pudo reconocer una de las celdas del análisis. Vuelva a analizar el PDF.");
                    }

                    var resolucion = NormalizeResolution(cellPayload.Resolucion);
                    if (resolucion is not ("use_imported" or "keep_db" or "clear_db" or "pending"))
                    {
                        throw new ValidationException("Se recibió una resolución de celda no permitida para este importador.");
                    }

                    if (resolucion == "use_imported" && cell.ValorImportado == null)
                    {
                        throw new ValidationException("Se recibió una resolución inválida: no se puede usar CiDi en una celda sin nota importada.");
                    }

                    if (resolucion == "clear_db" && cell.ValorImportado != null)
                    {
                        throw new ValidationException("Se recibió una resolución inválida: solo se puede quitar una nota cuando CiDi no trae valor.");
                    }

                    cell.Resolucion = resolucion;
                    expectedBaseIds[(row.RowId, cell.SlotKey)] = cellPayload.IdCalificacionBase;
                }
            }

            return expectedBaseIds;
        }

        private async Task<ImportacionCalificacionesAnalisisDto> RecomputeAnalysisAsync(
            ImportacionCalificacionesAnalisisDto analysis,
            Guid idEC,
            CancellationToken ct)
        {
            var instancias = await LoadInstanciasAsync(idEC, ct);
            ValidateInstancias(instancias);

            var slotMap = BuildSlotMap(instancias);
            var slotByKey = analysis.Slots.ToDictionary(slot => slot.SlotKey);
            foreach (var slotDefinition in slotMap.Values)
            {
                if (!slotByKey.TryGetValue(slotDefinition.SlotKey, out var slot))
                {
                    continue;
                }

                slot.IdIE = slotDefinition.IdIE;
                slot.TieneEstructuraPrevia = slotDefinition.HasStructure;
                slot.AdmiteCargaNotas = slotDefinition.AdmiteCargaNotas;
            }

            var validStudents = analysis.EstudiantesCurso.ToDictionary(student => student.IdEstudiante);
            var activeCalificaciones = await LoadCalificacionesVigentesByEcAsync(idEC, ct);

            foreach (var row in analysis.Rows)
            {
                row.Issues = row.Issues
                    .Where(issue => issue.Codigo is not ("student_duplicate_pdf_consistent" or "student_duplicate_pdf_conflict"))
                    .ToList();

                if (row.EstudianteAsociadoId.HasValue)
                {
                    if (!validStudents.ContainsKey(row.EstudianteAsociadoId.Value))
                    {
                        throw new ValidationException("El estudiante seleccionado ya no pertenece a este curso.");
                    }

                    if (row.RequiereAsociacionManual && !row.CandidatosEstudianteIds.Contains(row.EstudianteAsociadoId.Value))
                    {
                        throw new ValidationException("La asociación manual elegida ya no es válida para esta fila.");
                    }
                }

                foreach (var cell in row.Cells)
                {
                    var dbSnapshot = row.EstudianteAsociadoId.HasValue
                        && activeCalificaciones.TryGetValue((row.EstudianteAsociadoId.Value, cell.SlotKey), out var active)
                            ? active
                            : null;

                    cell.IdCalificacionBase = dbSnapshot?.IdCalificacion;
                    cell.ValorDb = dbSnapshot?.Puntaje;

                    slotMap.TryGetValue(cell.SlotKey, out var slotDefinition);
                    RecomputeCellState(cell, row, slotDefinition);
                }
            }

            ApplyDuplicateConflicts(analysis.Rows);
            RecomputeRowStates(analysis.Rows);
            analysis.Resumen = BuildResumen(analysis.Rows, analysis.Slots);
            analysis.ResumenConfirmacionInicial = BuildConfirmacionResumen(analysis.Rows);
            analysis.PuedeConfirmar = analysis.Bloqueos.Count == 0 && !HasPendingRows(analysis.Rows);
            analysis.Estado = analysis.Bloqueos.Count > 0
                ? EstadoImportacionCalificaciones.Analizada.ToString()
                : analysis.PuedeConfirmar
                    ? EstadoImportacionCalificaciones.ListaParaConfirmar.ToString()
                    : EstadoImportacionCalificaciones.EnRevision.ToString();

            return analysis;
        }

        private static void ValidateConcurrencySnapshot(
            ImportacionCalificacionesAnalisisDto analysis,
            IReadOnlyDictionary<(string RowId, string SlotKey), Guid?> expectedBaseIds)
        {
            foreach (var row in analysis.Rows)
            {
                foreach (var cell in row.Cells)
                {
                    if (!expectedBaseIds.TryGetValue((row.RowId, cell.SlotKey), out var expectedId))
                    {
                        throw new ValidationException("Faltan datos de concurrencia para confirmar la importación. Vuelva a analizar el PDF.");
                    }

                    if (expectedId != cell.IdCalificacionBase)
                    {
                        throw new InvalidOperationException("Las calificaciones del sistema cambiaron desde el análisis inicial. Vuelva a analizar el PDF antes de confirmar.");
                    }
                }
            }
        }

        private static void ValidateFinalCellResolutions(ImportacionCalificacionesAnalisisDto analysis)
        {
            foreach (var cell in analysis.Rows.SelectMany(row => row.Cells))
            {
                switch (NormalizeResolution(cell.Resolucion))
                {
                    case "pending":
                        throw new ValidationException("Todavía quedan celdas pendientes de resolución.");
                    case "use_imported" when cell.ValorImportado == null:
                        throw new ValidationException("Se recibió una resolución inválida: no se puede usar CiDi en una celda sin nota importada.");
                    case "clear_db" when cell.ValorImportado != null || cell.ValorDb == null:
                        throw new ValidationException("Se recibió una resolución inválida: solo se puede quitar una nota cuando CiDi no trae valor y el sistema sí tiene una nota vigente.");
                }
            }
        }

        private static ApplyPlan BuildApplyPlan(ImportacionCalificacionesAnalisisDto analysis)
        {
            var slotByKey = analysis.Slots.ToDictionary(slot => slot.SlotKey);
            var groupedCells = analysis.Rows
                .Where(row => row.EstudianteAsociadoId.HasValue)
                .SelectMany(row => row.Cells.Select(cell => new { Row = row, Cell = cell }))
                .GroupBy(item => (item.Row.EstudianteAsociadoId!.Value, item.Cell.SlotKey));

            var cambios = new List<CalificacionApplyChange>();
            var conflictosConservados = new List<CalificacionConflictoConservado>();

            foreach (var group in groupedCells)
            {
                var chosen = group.OrderBy(item => item.Row.Orden).First().Cell;
                var resolucion = NormalizeResolution(chosen.Resolucion);
                var requiresSlot = resolucion switch
                {
                    "use_imported" => chosen.ValorImportado != null && chosen.ValorImportado != chosen.ValorDb,
                    "clear_db" => chosen.ValorDb != null,
                    "keep_db" => chosen.ValorDb != null && (chosen.ValorImportado == null || chosen.ValorImportado != chosen.ValorDb),
                    _ => false,
                };

                if (!requiresSlot)
                {
                    continue;
                }

                if (!CalificacionesDomainHelper.TryParseSlotKey(chosen.SlotKey, out _, out var tipoCalificacion))
                {
                    throw new ValidationException("No se pudo reconocer una evaluación del análisis. Vuelva a analizar el PDF.");
                }

                if (!slotByKey.TryGetValue(chosen.SlotKey, out var slot) || !slot.IdIE.HasValue)
                {
                    throw new ValidationException("Hay una evaluación del análisis que no está correctamente cargada en la sección Evaluaciones.");
                }

                switch (resolucion)
                {
                    case "use_imported":
                        cambios.Add(new CalificacionApplyChange(
                            slot.IdIE.Value,
                            group.Key.Value,
                            tipoCalificacion,
                            chosen.ValorImportado));
                        break;

                    case "clear_db":
                        cambios.Add(new CalificacionApplyChange(
                            slot.IdIE.Value,
                            group.Key.Value,
                            tipoCalificacion,
                            null));
                        break;

                    case "keep_db":
                        conflictosConservados.Add(new CalificacionConflictoConservado(
                            slot.IdIE.Value,
                            group.Key.Value,
                            tipoCalificacion,
                            chosen.ValorDb,
                            chosen.ValorImportadoRaw));
                        break;
                }
            }

            return new ApplyPlan(cambios, conflictosConservados);
        }

        private static List<ImportacionRevisionRowDto> BuildInitialRows(
            IReadOnlyList<CidiCalificacionesParsedRow> parsedRows,
            IReadOnlyList<ImportacionStudentOptionDto> studentOptions,
            IReadOnlyList<ImportacionSlotDto> slots,
            IReadOnlyDictionary<string, SlotDefinition> slotMap,
            IReadOnlyDictionary<(Guid StudentId, string SlotKey), ActiveCalificacionSnapshot> activeCalificaciones)
        {
            var rowsByStudentName = parsedRows
                .GroupBy(row => CalificacionesDomainHelper.NormalizeText(row.StudentRaw))
                .ToDictionary(group => group.Key, group => group.Count());

            var studentsByNormalizedName = studentOptions
                .GroupBy(option => CalificacionesDomainHelper.NormalizeText(option.Label))
                .ToDictionary(group => group.Key, group => group.OrderBy(option => option.Label).ToList());

            var rows = new List<ImportacionRevisionRowDto>();
            foreach (var parsedRow in parsedRows.OrderBy(row => row.Order))
            {
                var studentResolution = ResolveStudent(parsedRow.StudentRaw, rowsByStudentName, studentsByNormalizedName);
                var row = new ImportacionRevisionRowDto
                {
                    RowId = $"row-{parsedRow.Order}",
                    Orden = parsedRow.Order,
                    EstudiantePdf = parsedRow.StudentRaw,
                    Estado = "clean",
                    EstudianteAsociadoId = studentResolution.SelectedStudentId,
                    RequiereAsociacionManual = studentResolution.RequiresManualAssociation,
                    CandidatosEstudianteIds = studentResolution.CandidateIds.ToList(),
                    Issues = studentResolution.Issues.ToList(),
                };

                foreach (var slot in slots)
                {
                    parsedRow.Cells.TryGetValue(slot.SlotKey, out var rawValue);
                    var importedValue = TryParseImportedGrade(rawValue, out var invalidMessage);
                    var dbSnapshot = row.EstudianteAsociadoId.HasValue
                        && activeCalificaciones.TryGetValue((row.EstudianteAsociadoId.Value, slot.SlotKey), out var active)
                            ? active
                            : null;

                    row.Cells.Add(BuildInitialCell(
                        slot,
                        rawValue,
                        importedValue,
                        invalidMessage,
                        dbSnapshot,
                        row.RequiereAsociacionManual,
                        row.Issues.Any(issue => issue.Severidad == "blocking")));
                }

                rows.Add(row);
            }

            return rows;
        }

        private static ImportacionRevisionCellDto BuildInitialCell(
            ImportacionSlotDto slot,
            string? rawValue,
            int? importedValue,
            string? invalidMessage,
            ActiveCalificacionSnapshot? dbSnapshot,
            bool requiresManualAssociation,
            bool hasBlockingRowIssue)
        {
            var cell = new ImportacionRevisionCellDto
            {
                SlotKey = slot.SlotKey,
                IdCalificacionBase = dbSnapshot?.IdCalificacion,
                EvaluacionNumero = slot.EvaluacionNumero,
                TipoCalificacion = slot.TipoCalificacion,
                ValorImportadoRaw = rawValue,
                ValorImportado = importedValue,
                ValorDb = dbSnapshot?.Puntaje,
                ValorFinal = dbSnapshot?.Puntaje,
                Estado = "clean",
                Resolucion = "keep_db",
            };

            if (!string.IsNullOrWhiteSpace(rawValue) && importedValue == null)
            {
                cell.Estado = "blocking";
                cell.Resolucion = "pending";
                cell.Mensaje = invalidMessage ?? "La nota detectada en el PDF no es válida.";
                return cell;
            }

            if (!slot.TieneEstructuraPrevia && importedValue != null)
            {
                cell.Estado = "blocking";
                cell.Resolucion = "pending";
                cell.Mensaje = "Antes de importar esta nota, debe cargar la evaluación correspondiente en la sección Evaluaciones.";
                return cell;
            }

            if (importedValue == null && dbSnapshot?.Puntaje == null)
            {
                return cell;
            }

            if (requiresManualAssociation)
            {
                cell.Estado = "review";
                cell.Resolucion = "pending";
                cell.ValorFinal = importedValue ?? dbSnapshot?.Puntaje;
                cell.Mensaje = "Seleccione cuál de los homónimos del curso corresponde a esta fila.";
                return cell;
            }

            if (hasBlockingRowIssue)
            {
                cell.Estado = importedValue != null || dbSnapshot?.Puntaje != null ? "blocking" : "clean";
                cell.Resolucion = cell.Estado == "blocking" ? "pending" : "keep_db";
                cell.ValorFinal = importedValue ?? dbSnapshot?.Puntaje;
                cell.Mensaje = cell.Estado == "blocking"
                    ? "No se puede continuar con esta fila hasta resolver la identidad del estudiante."
                    : null;
                return cell;
            }

            if (!slot.AdmiteCargaNotas && importedValue != null)
            {
                if (dbSnapshot?.Puntaje == importedValue)
                {
                    cell.ValorFinal = dbSnapshot.Puntaje;
                    return cell;
                }

                cell.Estado = "blocking";
                cell.Resolucion = "pending";
                cell.ValorFinal = importedValue;
                cell.Mensaje = "Antes de importar esta nota, debe marcar ese examen como evaluado en la sección Evaluaciones.";
                return cell;
            }

            if (importedValue == null && dbSnapshot?.Puntaje != null)
            {
                cell.Estado = "review";
                cell.Resolucion = "pending";
                cell.Mensaje = "CiDi no trae nota en esta celda y el sistema sí tiene una nota vigente.";
                return cell;
            }

            if (importedValue != null && dbSnapshot?.Puntaje == null)
            {
                cell.Estado = "clean";
                cell.Resolucion = "use_imported";
                cell.ValorFinal = importedValue;
                return cell;
            }

            if (importedValue != null && dbSnapshot?.Puntaje == importedValue)
            {
                cell.Estado = "clean";
                cell.Resolucion = "keep_db";
                cell.ValorFinal = dbSnapshot.Puntaje;
                return cell;
            }

            if (importedValue != null && dbSnapshot?.Puntaje != null)
            {
                cell.Estado = "review";
                cell.Resolucion = "pending";
                cell.ValorFinal = importedValue;
                cell.Mensaje = $"CiDi trae {importedValue} y en el sistema ya hay cargado un {dbSnapshot.Puntaje}.";
            }

            return cell;
        }

        private static void RecomputeCellState(
            ImportacionRevisionCellDto cell,
            ImportacionRevisionRowDto row,
            SlotDefinition? slotDefinition)
        {
            if (!string.IsNullOrWhiteSpace(cell.ValorImportadoRaw) && cell.ValorImportado == null)
            {
                cell.Estado = "blocking";
                cell.Resolucion = "pending";
                cell.Mensaje = $"El valor '{cell.ValorImportadoRaw}' no se pudo interpretar como una nota válida.";
                return;
            }

            if (slotDefinition == null || !slotDefinition.HasStructure)
            {
                if (cell.ValorImportado != null)
                {
                    cell.Estado = "blocking";
                    cell.Resolucion = "pending";
                    cell.Mensaje = "Antes de importar esta nota, debe cargar la evaluación correspondiente en la sección Evaluaciones.";
                }
                else
                {
                    cell.Estado = "clean";
                    cell.Resolucion = "keep_db";
                    cell.ValorFinal = cell.ValorDb;
                    cell.Mensaje = null;
                }

                return;
            }

            if (!row.EstudianteAsociadoId.HasValue)
            {
                if (row.RequiereAsociacionManual)
                {
                    cell.Estado = (cell.ValorImportado != null || cell.ValorDb != null) ? "review" : "clean";
                    cell.Resolucion = cell.Estado == "review" ? "pending" : "keep_db";
                    cell.ValorFinal = cell.ValorImportado ?? cell.ValorDb;
                    cell.Mensaje = cell.Estado == "review"
                        ? "Seleccione cuál de los homónimos del curso corresponde a esta fila."
                        : null;
                }
                else
                {
                    cell.Estado = (cell.ValorImportado != null || cell.ValorDb != null) ? "blocking" : "clean";
                    cell.Resolucion = cell.Estado == "blocking" ? "pending" : "keep_db";
                    cell.ValorFinal = cell.ValorImportado ?? cell.ValorDb;
                    cell.Mensaje = cell.Estado == "blocking"
                        ? "No se puede continuar con esta fila porque el estudiante del PDF no pertenece al curso."
                        : null;
                }

                return;
            }

            if (cell.ValorImportado == null && cell.ValorDb == null)
            {
                cell.Estado = "clean";
                cell.Resolucion = "keep_db";
                cell.ValorFinal = null;
                cell.Mensaje = null;
                return;
            }

            if (!slotDefinition.AdmiteCargaNotas && cell.ValorImportado != null)
            {
                if (cell.ValorDb == cell.ValorImportado)
                {
                    cell.Estado = "clean";
                    cell.Resolucion = "keep_db";
                    cell.ValorFinal = cell.ValorDb;
                    cell.Mensaje = null;
                }
                else
                {
                    cell.Estado = "blocking";
                    cell.Resolucion = "pending";
                    cell.ValorFinal = cell.ValorImportado;
                    cell.Mensaje = "Antes de importar esta nota, debe marcar ese examen como evaluado en la sección Evaluaciones.";
                }

                return;
            }

            if (cell.ValorImportado == null && cell.ValorDb != null)
            {
                switch (NormalizeResolution(cell.Resolucion))
                {
                    case "keep_db":
                        cell.Estado = "clean";
                        cell.ValorFinal = cell.ValorDb;
                        cell.Mensaje = null;
                        break;
                    case "clear_db":
                        cell.Estado = "clean";
                        cell.ValorFinal = null;
                        cell.Mensaje = null;
                        break;
                    default:
                        cell.Estado = "review";
                        cell.Resolucion = "pending";
                        cell.ValorFinal = cell.ValorDb;
                        cell.Mensaje = "CiDi no trae nota en esta celda y el sistema sí tiene una nota vigente.";
                        break;
                }

                return;
            }

            if (cell.ValorImportado != null && cell.ValorDb == null)
            {
                cell.Estado = "clean";
                cell.Resolucion = "use_imported";
                cell.ValorFinal = cell.ValorImportado;
                cell.Mensaje = null;
                return;
            }

            if (cell.ValorImportado == cell.ValorDb)
            {
                cell.Estado = "clean";
                cell.Resolucion = "keep_db";
                cell.ValorFinal = cell.ValorDb;
                cell.Mensaje = null;
                return;
            }

            switch (NormalizeResolution(cell.Resolucion))
            {
                case "keep_db":
                    cell.Estado = "clean";
                    cell.ValorFinal = cell.ValorDb;
                    cell.Mensaje = null;
                    break;
                case "use_imported":
                    cell.Estado = "clean";
                    cell.ValorFinal = cell.ValorImportado;
                    cell.Mensaje = null;
                    break;
                default:
                    cell.Estado = "review";
                    cell.Resolucion = "pending";
                    cell.ValorFinal = cell.ValorImportado;
                    cell.Mensaje = $"CiDi trae {cell.ValorImportado} y en el sistema ya hay cargado un {cell.ValorDb}.";
                    break;
            }
        }

        private static StudentResolution ResolveStudent(
            string rawStudent,
            IReadOnlyDictionary<string, int> rowsByStudentName,
            IReadOnlyDictionary<string, List<ImportacionStudentOptionDto>> studentsByNormalizedName)
        {
            var normalizedName = CalificacionesDomainHelper.NormalizeText(rawStudent);
            var pdfOccurrences = rowsByStudentName.TryGetValue(normalizedName, out var count) ? count : 1;
            var matchedStudents = studentsByNormalizedName.TryGetValue(normalizedName, out var matches)
                ? matches
                : new List<ImportacionStudentOptionDto>();

            if (matchedStudents.Count == 0)
            {
                return new StudentResolution(
                    null,
                    false,
                    Array.Empty<Guid>(),
                    new[]
                    {
                        new ImportacionIssueDto
                        {
                            Codigo = "student_not_found",
                            Severidad = "blocking",
                            Mensaje = "El estudiante del PDF no existe en la nómina activa de este curso.",
                        },
                    });
            }

            if (matchedStudents.Count == 1)
            {
                var issues = pdfOccurrences > 1
                    ? new[]
                    {
                        new ImportacionIssueDto
                        {
                            Codigo = "student_duplicate_name_pdf",
                            Severidad = "info",
                            Mensaje = "El mismo estudiante aparece más de una vez en el PDF.",
                        },
                    }
                    : Array.Empty<ImportacionIssueDto>();

                return new StudentResolution(
                    matchedStudents[0].IdEstudiante,
                    false,
                    new[] { matchedStudents[0].IdEstudiante },
                    issues);
            }

            if (pdfOccurrences > 1 && matchedStudents.Count != pdfOccurrences)
            {
                return new StudentResolution(
                    null,
                    false,
                    Array.Empty<Guid>(),
                    new[]
                    {
                        new ImportacionIssueDto
                        {
                            Codigo = "student_homonym_mismatch",
                            Severidad = "blocking",
                            Mensaje = "El PDF repite este nombre, pero la cantidad de homónimos reales del curso no coincide. Revise la nómina o el archivo antes de continuar.",
                        },
                    });
            }

            return new StudentResolution(
                null,
                true,
                matchedStudents.Select(student => student.IdEstudiante).ToList(),
                new[]
                {
                    new ImportacionIssueDto
                    {
                        Codigo = "student_homonym_manual_resolution",
                        Severidad = "info",
                        Mensaje = "Este nombre corresponde a más de un estudiante del curso. Debe seleccionar manualmente a cuál pertenece la fila.",
                    },
                });
        }

        private static void ApplyDuplicateConflicts(List<ImportacionRevisionRowDto> rows)
        {
            foreach (var row in rows)
            {
                row.Issues = row.Issues
                    .Where(issue => issue.Codigo is not ("student_duplicate_pdf_consistent" or "student_duplicate_pdf_conflict"))
                    .ToList();
            }

            var duplicatedRows = rows
                .Where(row => row.EstudianteAsociadoId.HasValue)
                .GroupBy(row => row.EstudianteAsociadoId!.Value)
                .Where(group => group.Count() > 1)
                .ToList();

            foreach (var duplicateGroup in duplicatedRows)
            {
                var hadConsistentDuplicate = false;
                var cellGroups = duplicateGroup
                    .SelectMany(row => row.Cells.Select(cell => new { Row = row, Cell = cell }))
                    .GroupBy(item => item.Cell.SlotKey);

                foreach (var cellGroup in cellGroups)
                {
                    var hasImportedValueInGroup = cellGroup.Any(item => HasPdfValue(item.Cell));
                    if (!hasImportedValueInGroup)
                    {
                        continue;
                    }

                    var values = cellGroup
                        .Select(item => GetPdfDuplicateValue(item.Cell))
                        .Distinct()
                        .ToList();

                    if (values.Count > 1)
                    {
                        foreach (var item in cellGroup)
                        {
                            item.Cell.Estado = "blocking";
                            item.Cell.Resolucion = "pending";
                            item.Cell.Mensaje = "El mismo estudiante aparece repetido en el PDF con valores distintos para esta evaluación.";
                        }

                        foreach (var row in duplicateGroup)
                        {
                            row.Issues.Add(new ImportacionIssueDto
                            {
                                Codigo = "student_duplicate_pdf_conflict",
                                Severidad = "blocking",
                                Mensaje = "El mismo estudiante aparece repetido en el PDF con notas distintas para la misma evaluación.",
                            });
                        }
                    }
                    else if (cellGroup.Count() > 1 && values.Count == 1 && values[0] != null)
                    {
                        hadConsistentDuplicate = true;
                    }
                }

                if (hadConsistentDuplicate)
                {
                    foreach (var row in duplicateGroup)
                    {
                        row.Issues.Add(new ImportacionIssueDto
                        {
                            Codigo = "student_duplicate_pdf_consistent",
                            Severidad = "info",
                            Mensaje = "El mismo estudiante aparece repetido en el PDF con el mismo valor. Se consolidará automáticamente al confirmar.",
                        });
                    }
                }
            }
        }

        private static void PromoteUnrecoverableRowBlockers(
            IReadOnlyList<ImportacionRevisionRowDto> rows,
            List<ImportacionIssueDto> bloqueos)
        {
            var addedKeys = bloqueos
                .Select(issue => $"{issue.Codigo}|{issue.SlotKey}|{issue.Mensaje}")
                .ToHashSet(StringComparer.Ordinal);

            void AddBlocker(string code, string message, string? slotKey = null)
            {
                var key = $"{code}|{slotKey}|{message}";
                if (!addedKeys.Add(key))
                {
                    return;
                }

                bloqueos.Add(new ImportacionIssueDto
                {
                    Codigo = code,
                    Severidad = "blocking",
                    Mensaje = message,
                    SlotKey = slotKey,
                });
            }

            foreach (var row in rows)
            {
                foreach (var issue in row.Issues.Where(issue => issue.Severidad == "blocking"))
                {
                    switch (issue.Codigo)
                    {
                        case "student_not_found":
                            AddBlocker(
                                issue.Codigo,
                                $"El estudiante \"{row.EstudiantePdf}\" no existe en la nómina activa del curso. Revise el PDF o la nómina y vuelva a analizar.");
                            break;

                        case "student_homonym_mismatch":
                            AddBlocker(
                                issue.Codigo,
                                $"El nombre \"{row.EstudiantePdf}\" aparece repetido en el PDF, pero la cantidad de homónimos reales del curso no coincide. Revise el PDF o la nómina y vuelva a analizar.");
                            break;
                    }
                }

                foreach (var cell in row.Cells.Where(cell => !string.IsNullOrWhiteSpace(cell.ValorImportadoRaw) && cell.ValorImportado == null))
                {
                    AddBlocker(
                        "nota_invalida",
                        $"El valor \"{cell.ValorImportadoRaw}\" no se pudo interpretar como nota válida en {BuildDisplaySlotLabel(cell)} para \"{row.EstudiantePdf}\". Revise el PDF y vuelva a analizar.",
                        cell.SlotKey);
                }
            }

            var duplicateConflicts = rows
                .Where(row => row.Issues.Any(issue => issue.Codigo == "student_duplicate_pdf_conflict"))
                .SelectMany(row => row.Cells
                    .Where(cell => cell.Estado == "blocking"
                        && cell.Mensaje?.Contains("repetido", StringComparison.OrdinalIgnoreCase) == true)
                    .Select(cell => new
                    {
                        StudentKey = row.EstudianteAsociadoId?.ToString("D") ?? row.EstudiantePdf,
                        row.EstudiantePdf,
                        Cell = cell,
                    }))
                .GroupBy(item => $"{item.StudentKey}|{item.Cell.SlotKey}");

            foreach (var group in duplicateConflicts)
            {
                var sample = group.First();
                AddBlocker(
                    "student_duplicate_pdf_conflict",
                    $"El PDF trae valores contradictorios para \"{sample.EstudiantePdf}\" en {BuildDisplaySlotLabel(sample.Cell)}. Corrija el PDF y vuelva a analizar.",
                    sample.Cell.SlotKey);
            }
        }

        private static string BuildDisplaySlotLabel(ImportacionRevisionCellDto cell)
            => $"E{cell.EvaluacionNumero} > {cell.TipoCalificacion}";

        private static bool HasPdfValue(ImportacionRevisionCellDto cell)
        {
            return !string.IsNullOrWhiteSpace(cell.ValorImportadoRaw) || cell.ValorImportado != null;
        }

        private static int? GetPdfDuplicateValue(ImportacionRevisionCellDto cell)
        {
            return HasPdfValue(cell) ? cell.ValorImportado : null;
        }

        private static void RecomputeRowStates(List<ImportacionRevisionRowDto> rows)
        {
            foreach (var row in rows)
            {
                if (!row.EstudianteAsociadoId.HasValue)
                {
                    var hasBlockingIssue = row.Issues.Any(issue => issue.Severidad == "blocking");
                    row.Estado = hasBlockingIssue ? "blocking" : row.RequiereAsociacionManual ? "review" : "blocking";
                    row.Mensaje = hasBlockingIssue
                        ? ""
                        : row.RequiereAsociacionManual
                            ? "Debe seleccionar a cuál de los homónimos del curso corresponde esta fila."
                            : "No se pudo asociar esta fila a un estudiante del curso.";
                    continue;
                }

                if (row.Issues.Any(issue => issue.Severidad == "blocking") || row.Cells.Any(cell => cell.Estado == "blocking"))
                {
                    row.Estado = "blocking";
                    row.Mensaje = "";
                    continue;
                }

                if (row.Cells.Any(cell => cell.Resolucion == "pending" || cell.Estado == "review"))
                {
                    row.Estado = "review";
                    row.Mensaje = "Esta fila necesita revisión.";
                    continue;
                }

                row.Estado = "clean";
                row.Mensaje = null;
            }
        }

        private static bool HasPendingRows(IEnumerable<ImportacionRevisionRowDto> rows)
        {
            return rows.Any(row => row.Estado != "clean");
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
                NotasNuevas = noteCells.Count(item => item.Cell.ValorImportado != null && item.Cell.ValorDb == null),
                NotasYaExistentes = noteCells.Count(item => item.Cell.ValorDb != null),
                ConflictosDeNotas = noteCells.Count(item => item.Cell.Estado != "clean" && (item.Cell.ValorImportado != null || item.Cell.ValorDb != null)),
                NotasInvalidas = noteCells.Count(item => !string.IsNullOrWhiteSpace(item.Cell.ValorImportadoRaw) && item.Cell.ValorImportado == null),
                PendientesDeRevision = noteCells.Count(item => item.Cell.Resolucion == "pending") + rows.Count(row => row.Estado == "blocking"),
            };
        }

        private static ImportacionConfirmacionResumenDto BuildConfirmacionResumen(IReadOnlyList<ImportacionRevisionRowDto> rows)
        {
            var noteCells = rows
                .Where(row => row.EstudianteAsociadoId.HasValue)
                .SelectMany(row => row.Cells)
                .ToList();

            return new ImportacionConfirmacionResumenDto
            {
                EstudiantesValidados = rows.Count(row => row.EstudianteAsociadoId.HasValue),
                NotasNuevas = noteCells.Count(cell => NormalizeResolution(cell.Resolucion) == "use_imported" && cell.ValorDb == null && cell.ValorImportado != null),
                NotasExistentesMantenidas = noteCells.Count(cell =>
                    NormalizeResolution(cell.Resolucion) == "keep_db"
                    && cell.ValorDb != null
                    && (cell.ValorImportado == null || cell.ValorImportado != cell.ValorDb)),
                NotasReemplazadas = noteCells.Count(cell => NormalizeResolution(cell.Resolucion) == "use_imported" && cell.ValorDb != null && cell.ValorImportado != null && cell.ValorImportado != cell.ValorDb),
                NotasQuitadas = noteCells.Count(cell => NormalizeResolution(cell.Resolucion) == "clear_db" && cell.ValorDb != null),
            };
        }

        private async Task<Dictionary<(Guid StudentId, string SlotKey), ActiveCalificacionSnapshot>> LoadCalificacionesVigentesByEcAsync(Guid idEC, CancellationToken ct)
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
                calificacion => new ActiveCalificacionSnapshot(calificacion.IdCalificacion, calificacion.Puntaje));
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
                        .Select(a => new ArchivoReadModel(a.IdArchivoIE, a.TipoCalificacion, a.Estado))
                        .ToList()))
                .ToListAsync(ct);
        }

        private static Dictionary<string, SlotDefinition> BuildSlotMap(IEnumerable<InstanciaReadModel> instancias)
        {
            var map = Enumerable.Range(1, 8)
                .SelectMany(nro => new[]
                {
                    new SlotDefinition(CalificacionesDomainHelper.BuildSlotKey(nro, TipoCalificacion.NotaOriginal), nro, "N", false, null, false),
                    new SlotDefinition(CalificacionesDomainHelper.BuildSlotKey(nro, TipoCalificacion.Recuperatorio1), nro, "R1", false, null, false),
                    new SlotDefinition(CalificacionesDomainHelper.BuildSlotKey(nro, TipoCalificacion.Recuperatorio2), nro, "R2", false, null, false),
                })
                .ToDictionary(slot => slot.SlotKey);

            foreach (var instancia in instancias)
            {
                foreach (var archivo in instancia.Archivos)
                {
                    var slotKey = CalificacionesDomainHelper.BuildSlotKey(instancia.Nro, archivo.TipoCalificacion);
                    map[slotKey] = new SlotDefinition(
                        slotKey,
                        instancia.Nro,
                        CalificacionesDomainHelper.ToTipoCalificacionCode(archivo.TipoCalificacion),
                        true,
                        instancia.IdIE,
                        archivo.Estado == EstadoInstanciaEvaluativa.Evaluada);
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
                    AdmiteCargaNotas = slot.AdmiteCargaNotas,
                })
                .ToList();
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
                    Mensaje = $"El espacio curricular del PDF no coincide con el espacio donde está realizando la importación ({espacio.NombreMateria}).",
                });
            }

            if (!normalizedText.Contains(espacio.AnioLectivo.ToString()))
            {
                blockers.Add(new ImportacionIssueDto
                {
                    Codigo = "anio_lectivo_no_coincide",
                    Severidad = "blocking",
                    Mensaje = $"El ciclo lectivo del PDF no coincide con el de este curso ({espacio.AnioLectivo}).",
                });
            }

            var courseVariants = BuildCourseVariants(espacio);
            if (!courseVariants.Any(normalizedText.Contains))
            {
                blockers.Add(new ImportacionIssueDto
                {
                    Codigo = "curso_no_coincide",
                    Severidad = "blocking",
                    Mensaje = $"El curso y la división del PDF no coinciden con este espacio ({BuildExpectedCourseLabel(espacio)}).",
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
                    Mensaje = "El archivo no corresponde al listado de calificaciones exportado desde CiDi.",
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
                    Mensaje = "No se pudo identificar correctamente el curso, la división, el espacio curricular o el ciclo lectivo en el PDF.",
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
                    Mensaje = "No se pudo reconocer la tabla de calificaciones del archivo.",
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
                invalidMessage = $"El valor '{rawValue}' no se pudo interpretar como una nota válida.";
                return null;
            }

            if (grade is < 1 or > 10)
            {
                invalidMessage = $"La nota '{rawValue}' está fuera del rango permitido. Solo se aceptan valores del 1 al 10.";
                return null;
            }

            return grade;
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
                throw new UnauthorizedAccessException("No tiene permisos para importar calificaciones en este espacio curricular.");
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

        private static string NormalizeResolution(string? resolution)
            => resolution?.Trim().ToLowerInvariant() ?? string.Empty;

        private static void ValidateInstancias(List<InstanciaReadModel> instancias)
        {
            if (instancias.Count > 8)
            {
                throw new InvalidOperationException("Este espacio tiene más de 8 evaluaciones registradas para el ciclo lectivo.");
            }

            if (instancias.Any(i => i.Nro is < 1 or > 8))
            {
                throw new InvalidOperationException("Se encontraron evaluaciones con una numeración fuera del rango permitido (1 a 8).");
            }

            if (instancias.GroupBy(i => i.Nro).Any(group => group.Count() > 1))
            {
                throw new InvalidOperationException("Se encontraron evaluaciones duplicadas con el mismo número.");
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
        private sealed record ArchivoReadModel(Guid IdArchivoIE, TipoCalificacion TipoCalificacion, EstadoInstanciaEvaluativa Estado);
        private sealed record SlotDefinition(string SlotKey, int EvaluacionNumero, string TipoCalificacionCode, bool HasStructure, Guid? IdIE, bool AdmiteCargaNotas);
        private sealed record ActiveCalificacionSnapshot(Guid IdCalificacion, int? Puntaje);
        private sealed record StudentResolution(Guid? SelectedStudentId, bool RequiresManualAssociation, IReadOnlyList<Guid> CandidateIds, IReadOnlyList<ImportacionIssueDto> Issues);
        private sealed record ApplyPlan(IReadOnlyCollection<CalificacionApplyChange> Cambios, IReadOnlyCollection<CalificacionConflictoConservado> ConflictosConservados);
    }
}
