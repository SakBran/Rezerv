namespace Rezerv.Application.DTO
{
    public class BookingRequest
    {
        public int UserId { get; set; }
        public int ScheduleId { get; set; }
        public int UserPackageId { get; set; }
    }
}
