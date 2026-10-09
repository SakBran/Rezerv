using System;

namespace Rezerv.Application.DTO
{
    public class PurchaseResponse
    {
        public int UserPackageId { get; set; }
        public int RemainingCredits { get; set; }
        public DateTime ExpiresAt { get; set; }
    }
}
