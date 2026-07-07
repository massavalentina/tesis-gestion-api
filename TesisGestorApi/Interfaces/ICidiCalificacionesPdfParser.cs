namespace TesisGestorApi.Interfaces
{
    public interface ICidiCalificacionesPdfParser
    {
        CidiCalificacionesParseResult Parse(Stream pdfStream);
    }

    public sealed record CidiCalificacionesParseResult(
        string FullText,
        IReadOnlyList<CidiCalificacionesParsedRow> Rows);

    public sealed record CidiCalificacionesParsedRow(
        int Order,
        string StudentRaw,
        IReadOnlyDictionary<string, string?> Cells);
}
