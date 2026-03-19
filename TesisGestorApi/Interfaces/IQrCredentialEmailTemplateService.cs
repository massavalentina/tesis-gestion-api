namespace TesisGestorApi.Interfaces
{
    public interface IQrCredentialEmailTemplateService
    {
        string Build(
            string tutorNombre,
            string alumnoNombre,
            string alumnoDni,
            int anioLectivo,
            Guid codigoQr,
            DateTime fechaVigencia,
            string? mensajePersonalizado,
            string qrInlineContentId,
            string? logoInlineContentId = null);
    }
}
