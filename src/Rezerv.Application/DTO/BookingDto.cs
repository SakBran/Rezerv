using System;

namespace Rezerv.Application.DTO
{
    public class BookingDto
    {
        public int BookingId { get; set; }
        public int ScheduleId { get; set; }
        public string ClassName { get; set; } = "";
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Status { get; set; } = "";
        public DateTime BookedAt { get; set; }
        public DateTime? CancelledAt { get; set; }
        public bool CreditRefunded { get; set; }
    }
}
