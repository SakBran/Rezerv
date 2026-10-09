using System;

namespace Rezerv.Application.DTO
{
    public class TimetableScheduleDto
    {
        public int ScheduleId { get; set; }
        public int BusinessId { get; set; }
        public string ClassName { get; set; } = "";
        public string InstructorName { get; set; } = "";
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int AttendanceCount { get; set; }
        public int AvailableSlots { get; set; }
        public bool IsFull { get; set; }
        public string BusinessName { get; set; } = "";
    }
}
