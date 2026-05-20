namespace TesisGestorApi.Helpers
{
    public static class EmailTemplateHelper
    {
        public const string SubjectPrefix = "GR-01-TC-2025-Carcano-Massa-Herrera Moyano";

        /// <summary>
        /// Envuelve el cuerpo HTML en el template institucional del Colegio Luis Manuel Robles.
        /// logoSrc puede ser una URL externa o un cid: para imágenes inline (e.g. "cid:institution-logo").
        /// Si es null o vacío, el bloque del logo se omite.
        /// </summary>
        public static string Build(string cuerpo, string? logoSrc = null)
        {
            var logoBlock = string.IsNullOrWhiteSpace(logoSrc)
                ? string.Empty
                : $@"          <!-- Logo -->
          <tr>
            <td align='center' style='padding:32px 40px 20px;'>
              <img src='{logoSrc}' alt='Colegio Luis Manuel Robles' width='80'
                   style='display:block;' />
            </td>
          </tr>

          <!-- Línea superior -->
          <tr>
            <td style='padding:0 40px;'>
              <hr style='border:0;border-top:1px solid #e0e0e0;margin:0;' />
            </td>
          </tr>";

            return $@"<!DOCTYPE html>
<html lang='es'>
<head>
  <meta charset='UTF-8'>
  <meta name='viewport' content='width=device-width,initial-scale=1'>
</head>
<body style='margin:0;padding:24px 0;background:#f2f2f2;font-family:Arial,""Helvetica Neue"",sans-serif;'>
  <table cellpadding='0' cellspacing='0' border='0' width='100%'>
    <tr>
      <td align='center'>
        <table cellpadding='0' cellspacing='0' border='0' width='560' style='max-width:560px;background:#ffffff;border-radius:4px;overflow:hidden;'>

{logoBlock}

          <!-- Cuerpo -->
          <tr>
            <td style='padding:28px 40px;font-size:14px;line-height:1.75;color:#333333;'>
              {cuerpo}
              <p style='margin:24px 0 0;'>
                Atentamente,<br>
                Colegio Luis Manuel Robles
              </p>
            </td>
          </tr>

          <!-- Línea inferior -->
          <tr>
            <td style='padding:0 40px;'>
              <hr style='border:0;border-top:1px solid #e0e0e0;margin:0;' />
            </td>
          </tr>

          <!-- Footer -->
          <tr>
            <td align='center' style='padding:16px 40px 28px;font-size:11px;color:#999999;line-height:1.6;'>
              Secretar&iacute;a de la instituci&oacute;n Colegio Luis Manuel Robles &mdash; Padre Luis Monti<br>
              1859, X5004ENI C&oacute;rdoba &middot; 03514517213 &middot; colegiorobles.edu.ar<br>
              Desde &copy; PaletApp 2026
            </td>
          </tr>

        </table>
      </td>
    </tr>
  </table>
</body>
</html>";
        }

        /// <summary>
        /// Intenta cargar el logo institucional desde el sistema de archivos.
        /// Retorna los bytes y el content-type, o (null, "") si no se encontró.
        /// </summary>
        public static (byte[]? Bytes, string ContentType) LoadLogoBytes()
        {
            var candidates = new[]
            {
                (Path.Combine(AppContext.BaseDirectory, "wwwroot", "logo.jpg"), "image/jpeg"),
                (Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "logo.jpg"), "image/jpeg"),
                (Path.Combine(AppContext.BaseDirectory, "utils", "robles.png"), "image/png"),
                (Path.Combine(Directory.GetCurrentDirectory(), "utils", "robles.png"), "image/png"),
            };

            foreach (var (path, contentType) in candidates)
            {
                try
                {
                    if (File.Exists(path))
                        return (File.ReadAllBytes(path), contentType);
                }
                catch { }
            }

            return (null, string.Empty);
        }
    }
}
