namespace Rezerv.Application.DTO
{
    public class CancelResponse
    {
        public int BookingId { get; set; }
        public string Status { get; set; } = "";
        public bool CreditRefunded { get; set; }
        public int RemainingCredits { get; set; }
        public int? PromotedUserId { get; set; }
    }
}
