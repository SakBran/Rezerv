using System;

namespace Rezerv.Application.DTO
{
    public class WaitlistResponse
    {
        public int WaitlistEntryId { get; set; }
        public int ScheduleId { get; set; }
        public int Position { get; set; }
        public DateTime JoinedAt { get; set; }
    }
}
