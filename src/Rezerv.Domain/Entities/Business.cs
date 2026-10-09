namespace Rezerv.Domain.Entities;

public class Business
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public ICollection<Package> Packages { get; set; } = new List<Package>();
    public ICollection<TimetableSchedule> Schedules { get; set; } = new List<TimetableSchedule>();
}
