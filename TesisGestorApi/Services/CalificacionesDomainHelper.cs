using TesisGestorApi.Entities;

namespace TesisGestorApi.Services
{
    internal static class CalificacionesDomainHelper
    {
        internal static string ToTipoCalificacionCode(TipoCalificacion tipoCalificacion)
        {
            return tipoCalificacion switch
            {
                TipoCalificacion.NotaOriginal => "N",
                TipoCalificacion.Recuperatorio1 => "R1",
                TipoCalificacion.Recuperatorio2 => "R2",
                _ => tipoCalificacion.ToString(),
            };
        }

        internal static bool TryParseTipoCalificacion(string rawValue, out TipoCalificacion tipoCalificacion)
        {
            tipoCalificacion = default;
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return false;
            }

            switch (rawValue.Trim().ToUpperInvariant())
            {
                case "N":
                case "NOTAORIGINAL":
                    tipoCalificacion = TipoCalificacion.NotaOriginal;
                    return true;
                case "R1":
                case "RECUPERATORIO1":
                    tipoCalificacion = TipoCalificacion.Recuperatorio1;
                    return true;
                case "R2":
                case "RECUPERATORIO2":
                    tipoCalificacion = TipoCalificacion.Recuperatorio2;
                    return true;
                default:
                    return false;
            }
        }

        internal static string NormalizeText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var normalized = value.Normalize(System.Text.NormalizationForm.FormD);
            var chars = normalized
                .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
                .Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ')
                .ToArray();

            return string.Join(' ', new string(chars).Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }

        internal static string BuildSlotKey(int evaluacionNumero, TipoCalificacion tipoCalificacion)
            => $"E{evaluacionNumero}_{ToTipoCalificacionCode(tipoCalificacion)}";

        internal static bool TryParseSlotKey(string slotKey, out int evaluacionNumero, out TipoCalificacion tipoCalificacion)
        {
            evaluacionNumero = 0;
            tipoCalificacion = default;

            if (string.IsNullOrWhiteSpace(slotKey))
            {
                return false;
            }

            var parts = slotKey.Trim().ToUpperInvariant().Split('_', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2 || !parts[0].StartsWith('E'))
            {
                return false;
            }

            if (!int.TryParse(parts[0][1..], out evaluacionNumero))
            {
                return false;
            }

            return TryParseTipoCalificacion(parts[1], out tipoCalificacion);
        }
    }
}
