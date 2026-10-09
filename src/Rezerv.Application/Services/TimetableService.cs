using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Rezerv.Application.DTO;
using Rezerv.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Rezerv.Application.Services
{
    public class TimetableService
    {
        private readonly Interfaces.IApplicationDbContext _db;
        public TimetableService(Interfaces.IApplicationDbContext db)
        {
            _db = db;
        }
        public async Task<List<TimetableScheduleDto>> ListAsync(int? businessId, DateOnly? date)
        {
            var q = _db.Schedules.AsQueryable();
            if (businessId is not null)
            {
                q = q.Where(s => s.BusinessId == businessId);
            }
            if (date is not null)
            {
                var from = date.Value.ToDateTime(TimeOnly.MinValue);
                q = q.Where(s => s.StartTime >= from && s.StartTime < from.AddDays(1));
            }

            return await q.OrderBy(s => s.StartTime).Select(s => new TimetableScheduleDto
            {
                ScheduleId = s.Id,
                ClassName = s.ClassName,
                InstructorName = s.InstructorName,
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                AttendanceCount = s.Bookings.Count(b => b.Status == BookingStatus.Booked),
                AvailableSlots = s.Capacity - s.Bookings.Count(b => b.Status == BookingStatus.Booked),
                IsFull = s.Bookings.Count(b => b.Status == BookingStatus.Booked) >= s.Capacity,
                BusinessName = s.Business.Name
            }).ToListAsync();
        }
    }
}