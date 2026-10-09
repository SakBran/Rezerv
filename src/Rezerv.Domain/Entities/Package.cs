namespace Rezerv.Domain.Entities;

public class Package
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public Business Business { get; set; } = null!;
    public string Name { get; set; } = "";
    public int TotalCredits { get; set; }
    public int ValidityDays { get; set; }
    public decimal Price { get; set; }
}
