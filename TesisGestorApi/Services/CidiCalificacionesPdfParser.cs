using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using TesisGestorApi.Interfaces;

namespace TesisGestorApi.Services
{
    public class CidiCalificacionesPdfParser : ICidiCalificacionesPdfParser
    {
        private const double LineTolerance = 2.5;
        private static readonly string[] RequiredFormatMarkers =
        {
            "listado de calificaciones",
            "espacio curricular",
            "curso",
            "division",
            "ciclo lectivo",
            "estudiantes",
            "eval 1",
            "eval 8",
        };

        public CidiCalificacionesParseResult Parse(Stream pdfStream)
        {
            using var document = PdfDocument.Open(pdfStream);
            var rows = new List<CidiCalificacionesParsedRow>();
            var fullTextPages = new List<string>();
            var rowOrder = 0;

            foreach (var page in document.GetPages())
            {
                fullTextPages.Add(page.Text);
                var pageRows = ParsePage(page, ref rowOrder);
                rows.AddRange(pageRows);
            }

            if (rows.Count == 0)
            {
                throw new InvalidOperationException("No se detectaron filas de estudiantes en el PDF de CiDi.");
            }

            var fullText = string.Join(Environment.NewLine, fullTextPages);
            ValidateExpectedCidiFormat(fullText);

            return new CidiCalificacionesParseResult(
                fullText,
                rows);
        }

        private static List<CidiCalificacionesParsedRow> ParsePage(Page page, ref int rowOrder)
        {
            var words = page.GetWords()
                .Where(word => !string.IsNullOrWhiteSpace(word.Text))
                .ToList();

            if (words.Count == 0)
            {
                return new List<CidiCalificacionesParsedRow>();
            }

            var lines = words
                .GroupBy(word => FindLineAnchor(words, word))
                .OrderByDescending(group => group.Key)
                .Select(group => group.OrderBy(w => w.BoundingBox.Left).ToList())
                .ToList();

            var subHeaderLine = lines
                .Select(line => new
                {
                    Line = line,
                    Tokens = line.Count(word => IsGradeSubHeader(word.Text)),
                })
                .OrderByDescending(item => item.Tokens)
                .FirstOrDefault(item => item.Tokens >= 12)
                ?.Line;

            if (subHeaderLine == null)
            {
                throw new InvalidOperationException("El PDF no contiene la estructura esperada de columnas Eval 1..8 con N/R1/R2.");
            }

            var gradeColumns = subHeaderLine
                .Where(word => IsGradeSubHeader(word.Text))
                .OrderBy(word => word.BoundingBox.Left)
                .Select((word, index) => new GradeColumn(
                    index,
                    word.BoundingBox.Left + ((word.BoundingBox.Right - word.BoundingBox.Left) / 2)))
                .ToList();

            if (gradeColumns.Count < 24)
            {
                throw new InvalidOperationException("El PDF no contiene las 24 subcolumnas esperadas para Eval 1..8.");
            }

            gradeColumns = gradeColumns.Take(24).ToList();
            var firstGradeColumnX = gradeColumns.Min(column => column.CenterX) - 8;
            var headerY = subHeaderLine.Average(word => word.BoundingBox.Bottom);

            var parsedRows = new List<CidiCalificacionesParsedRow>();
            foreach (var line in lines)
            {
                var lineY = line.Average(word => word.BoundingBox.Bottom);
                if (lineY >= headerY - LineTolerance)
                {
                    continue;
                }

                var studentWords = line
                    .Where(word => word.BoundingBox.Left < firstGradeColumnX)
                    .OrderBy(word => word.BoundingBox.Left)
                    .ToList();

                var gradeWords = line
                    .Where(word => word.BoundingBox.Left >= firstGradeColumnX)
                    .ToList();

                if (studentWords.Count == 0)
                {
                    continue;
                }

                var studentRaw = string.Join(' ', studentWords.Select(word => word.Text)).Trim();
                if (string.IsNullOrWhiteSpace(studentRaw) || LooksLikeNonStudentLine(studentRaw))
                {
                    continue;
                }

                var groupedByColumn = gradeWords
                    .GroupBy(word => FindClosestColumn(word, gradeColumns))
                    .ToDictionary(
                        group => group.Key,
                        group => string.Join(' ', group.OrderBy(word => word.BoundingBox.Left).Select(word => word.Text)).Trim());

                var cells = new Dictionary<string, string?>();
                for (var columnIndex = 0; columnIndex < 24; columnIndex++)
                {
                    var evaluacionNumero = (columnIndex / 3) + 1;
                    var tipo = (columnIndex % 3) switch
                    {
                        0 => "N",
                        1 => "R1",
                        _ => "R2",
                    };

                    var slotKey = $"E{evaluacionNumero}_{tipo}";
                    cells[slotKey] = groupedByColumn.TryGetValue(columnIndex, out var value) && !string.IsNullOrWhiteSpace(value)
                        ? value
                        : null;
                }

                rowOrder += 1;
                parsedRows.Add(new CidiCalificacionesParsedRow(rowOrder, studentRaw, cells));
            }

            return parsedRows;
        }

        private static void ValidateExpectedCidiFormat(string fullText)
        {
            var normalizedText = CalificacionesDomainHelper.NormalizeText(fullText);
            var missingMarkers = RequiredFormatMarkers
                .Where(marker => !normalizedText.Contains(CalificacionesDomainHelper.NormalizeText(marker), StringComparison.Ordinal))
                .ToList();

            if (missingMarkers.Count > 0)
            {
                throw new InvalidOperationException(
                    "El archivo no parece ser el listado de calificaciones exportado desde CiDi. Verificá que estés subiendo el PDF oficial.");
            }
        }

        private static bool IsGradeSubHeader(string value)
        {
            var normalized = value.Trim().ToUpperInvariant();
            return normalized is "N" or "R1" or "R2";
        }

        private static bool LooksLikeNonStudentLine(string studentRaw)
        {
            var normalized = studentRaw.Trim().ToUpperInvariant();
            return normalized.StartsWith("ESTUDIANTE")
                || normalized.StartsWith("APELLIDO")
                || normalized.StartsWith("CURSO")
                || normalized.StartsWith("ESPACIO")
                || normalized.StartsWith("PAGINA")
                || normalized.StartsWith("PÁGINA")
                || normalized.StartsWith("CICLO");
        }

        private static double FindLineAnchor(IReadOnlyList<Word> allWords, Word current)
        {
            var currentBottom = current.BoundingBox.Bottom;
            var match = allWords
                .Select(word => word.BoundingBox.Bottom)
                .Where(bottom => Math.Abs(bottom - currentBottom) <= LineTolerance)
                .OrderByDescending(bottom => bottom)
                .FirstOrDefault();

            return match == default ? currentBottom : match;
        }

        private static int FindClosestColumn(Word word, IReadOnlyList<GradeColumn> columns)
        {
            var centerX = word.BoundingBox.Left + ((word.BoundingBox.Right - word.BoundingBox.Left) / 2);
            return columns
                .OrderBy(column => Math.Abs(column.CenterX - centerX))
                .First()
                .Index;
        }

        private sealed record GradeColumn(int Index, double CenterX);
    }
}
