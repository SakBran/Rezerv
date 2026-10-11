using System;

namespace Rezerv.Application.DTO
{
    public class WaitlistEntryDto
    {
        public int WaitlistEntryId { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; } = "";
        public string Status { get; set; } = "";
        public int? Position { get; set; }
        public DateTime JoinedAt { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public string? Note { get; set; }
    }
}
