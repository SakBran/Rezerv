using Rezerv.Domain.Enums;

namespace Rezerv.Domain.Entities;

public class Booking
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int ScheduleId { get; set; }
    public TimetableSchedule Schedule { get; set; } = null!;
    public int UserPackageId { get; set; }
    public UserPackage UserPackage { get; set; } = null!;
    public BookingStatus Status { get; set; } = BookingStatus.Booked;
    public DateTime BookedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public bool CreditRefunded { get; set; }
}
