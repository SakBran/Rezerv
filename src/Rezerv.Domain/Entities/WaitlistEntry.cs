using Rezerv.Domain.Enums;

namespace Rezerv.Domain.Entities;

public class WaitlistEntry
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int ScheduleId { get; set; }
    public TimetableSchedule Schedule { get; set; } = null!;
    public int UserPackageId { get; set; }
    public UserPackage UserPackage { get; set; } = null!;
    public WaitlistStatus Status { get; set; } = WaitlistStatus.Waiting;
    public DateTime JoinedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? Note { get; set; }
}
