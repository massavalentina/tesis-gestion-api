using QRCoder;
using TesisGestorApi.Interfaces;

namespace TesisGestorApi.Services
{
    public class QrCredentialVisualService : IQrCredentialVisualService
    {
        public byte[] BuildQrPng(Guid code, int pixelsPerModule = 20)
        {
            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(code.ToString(), QRCodeGenerator.ECCLevel.Q);
            var qr = new PngByteQRCode(data);
            return qr.GetGraphic(pixelsPerModule);
        }
    }
}
