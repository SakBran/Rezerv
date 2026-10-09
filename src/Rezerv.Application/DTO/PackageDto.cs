namespace Rezerv.Application.DTO
{
    public class PackageDto
    {
        public int PackageId { get; set; }
        public int BusinessId { get; set; }
        public string BusinessName { get; set; } = "";
        public string Name { get; set; } = "";
        public int TotalCredits { get; set; }
        public int ValidityDays { get; set; }
        public decimal Price { get; set; }
    }
}
