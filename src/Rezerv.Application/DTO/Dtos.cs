using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Rezerv.Application.DTO
{
    public class PackageDto
    {
        public int PackageId { get; set; }
        public int BusinessId { get; set; }
        public string BusinessName { get; set; } = "";
        public string Name { get; set; } = "";
        public int TotalCredits { get; set; }
        public int ValidityDays { get; set; }
        public decimal Price { get; set; }
    }

    public class PurchaseRequest
    {
        public int UserId { get; set; }
        public int PackageId { get; set; }
    }

    public class PurchaseResponse
    {
        public int UserPackageId { get; set; }
        public int RemainingCredits { get; set; }
        public DateTime ExpiresAt { get; set; }
    }

    public class TimetableScheduleDto
    {
        public int ScheduleId { get; set; }
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