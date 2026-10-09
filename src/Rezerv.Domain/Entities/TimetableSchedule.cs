namespace Rezerv.Domain.Entities;

public class TimetableSchedule
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public Business Business { get; set; } = null!;
    public string ClassName { get; set; } = "";
    public string InstructorName { get; set; } = "";
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int Capacity { get; set; }
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public ICollection<WaitlistEntry> Waitlist { get; set; } = new List<WaitlistEntry>();

    public bool OverlapsWith(TimetableSchedule other)
        => StartTime < other.EndTime && other.StartTime < EndTime;

    public bool HasStarted(DateTime now) => now >= StartTime;
    public bool HasEnded(DateTime now) => now >= EndTime;
}
