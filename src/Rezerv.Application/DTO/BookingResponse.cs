using System;

namespace Rezerv.Application.DTO
{
    public class BookingResponse
    {
        public int BookingId { get; set; }
        public int ScheduleId { get; set; }
        public string ClassName { get; set; } = "";
        public DateTime StartTime { get; set; }
        public int RemainingCredits { get; set; }
        public string Status { get; set; } = "";
    }
}
