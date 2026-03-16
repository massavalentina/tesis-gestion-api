namespace TesisGestorApi.DTOs
{
    public class QrCredentialDeliveryStudentsPageDto
    {
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }
        public List<QrCredentialDeliveryStudentRowDto> Items { get; set; } = new();
    }
}
