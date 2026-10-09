namespace Rezerv.Domain.Entities;

public class UserPackage
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int PackageId { get; set; }
    public Package Package { get; set; } = null!;
    public int BusinessId { get; set; }          // denormalized — business match check မြန်အောင်
    public int TotalCredits { get; set; }
    public int RemainingCredits { get; set; }    // ← EF concurrency token
    public DateTime PurchasedAt { get; set; }
    public DateTime ExpiresAt { get; set; }

    public bool IsExpired(DateTime now) => now >= ExpiresAt;

    public void DeductCredit()
    {
        if (RemainingCredits <= 0) throw new InvalidOperationException("No credits left");
        RemainingCredits--;
    }

    public void RefundCredit() => RemainingCredits++;
}
