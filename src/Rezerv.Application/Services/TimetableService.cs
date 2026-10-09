using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Rezerv.Application.Common;
using Rezerv.Application.DTO;
using Rezerv.Application.Interfaces;
using Rezerv.Domain.Enums;

namespace Rezerv.Application.Services
{
    public class TimetableService
    {
        private static readonly TimeSpan TimetableCacheDuration = TimeSpan.FromSeconds(30);

        private readonly IApplicationDbContext _dbContext;
        private readonly ICacheService _cacheService;

        public TimetableService(IApplicationDbContext dbContext, ICacheService cacheService)
        {
            _dbContext = dbContext;
            _cacheService = cacheService;
        }

        public async Task<List<TimetableScheduleDto>> ListAsync(int? businessId, DateOnly? date)
        {
            List<TimetableScheduleDto> allSchedules = await GetAllSchedulesFromCacheOrDatabaseAsync();

            List<TimetableScheduleDto> filteredSchedules = new List<TimetableScheduleDto>();

            foreach (TimetableScheduleDto schedule in allSchedules)
            {
                if (businessId.HasValue)
                {
                    if (schedule.BusinessId != businessId.Value)
                    {
                        continue;
                    }
                }

                if (date.HasValue)
                {
                    DateOnly scheduleDate = DateOnly.FromDateTime(schedule.StartTime);

                    if (scheduleDate != date.Value)
                    {
                        continue;
                    }
                }

                filteredSchedules.Add(schedule);
            }

            return filteredSchedules;
        }

        private async Task<List<TimetableScheduleDto>> GetAllSchedulesFromCacheOrDatabaseAsync()
        {
            List<TimetableScheduleDto>? cachedSchedules =
                await _cacheService.GetAsync<List<TimetableScheduleDto>>(CacheKeys.Timetable);

            if (cachedSchedules != null)
            {
                return cachedSchedules;
            }

            List<TimetableScheduleDto> schedulesFromDatabase = await LoadAllSchedulesFromDatabaseAsync();

            await _cacheService.SetAsync(CacheKeys.Timetable, schedulesFromDatabase, TimetableCacheDuration);

            return schedulesFromDatabase;
        }

        private async Task<List<TimetableScheduleDto>> LoadAllSchedulesFromDatabaseAsync()
        {
            List<TimetableScheduleDto> schedules = await _dbContext.Schedules
                .OrderBy(schedule => schedule.StartTime)
                .Select(schedule => new TimetableScheduleDto
                {
                    ScheduleId = schedule.Id,
                    BusinessId = schedule.BusinessId,
                    BusinessName = schedule.Business.Name,
                    ClassName = schedule.ClassName,
                    InstructorName = schedule.InstructorName,
                    StartTime = schedule.StartTime,
                    EndTime = schedule.EndTime,
                    AttendanceCount = schedule.Bookings.Count(booking => booking.Status == BookingStatus.Booked),
                    AvailableSlots = schedule.Capacity
                        - schedule.Bookings.Count(booking => booking.Status == BookingStatus.Booked),
                    IsFull = schedule.Bookings.Count(booking => booking.Status == BookingStatus.Booked)
                        >= schedule.Capacity
                })
                .ToListAsync();

            return schedules;
        }
    }
}
