using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Rezerv.Application.Common;
using Rezerv.Application.DTO;
using Rezerv.Application.Exceptions;
using Rezerv.Application.Interfaces;
using Rezerv.Domain.Entities;
using Rezerv.Domain.Enums;

namespace Rezerv.Application.Services
{
    public class WaitlistService
    {
        private const int LockWaitSeconds = 5;
        private const int LockTtlSeconds = 10;

        private readonly IApplicationDbContext _dbContext;
        private readonly IDistributedLockService _lockService;
        private readonly ICacheService _cacheService;
        private readonly IClock _clock;

        public WaitlistService(
            IApplicationDbContext dbContext,
            IDistributedLockService lockService,
            ICacheService cacheService,
            IClock clock)
        {
            _dbContext = dbContext;
            _lockService = lockService;
            _cacheService = cacheService;
            _clock = clock;
        }

        public async Task<WaitlistResponse> JoinAsync(WaitlistRequest request)
        {
            string lockKey = CacheKeys.ScheduleLock(request.ScheduleId);
            string? token = await _lockService.TryAcquireAsync(lockKey, LockWaitSeconds, LockTtlSeconds);

            if (token == null)
            {
                throw new BusinessRuleException(503, "LOCK_TIMEOUT", "Schedule is busy, please retry");
            }

            try
            {
                return await JoinInsideLockAsync(request);
            }
            finally
            {
                await _lockService.ReleaseAsync(lockKey, token);
            }
        }

        private async Task<WaitlistResponse> JoinInsideLockAsync(WaitlistRequest request)
        {
            DateTime now = _clock.UtcNow;

            TimetableSchedule? schedule = await _dbContext.Schedules.FirstOrDefaultAsync(s => s.Id == request.ScheduleId);
            if (schedule == null)
            {
                throw BusinessRuleException.NotFound("SCHEDULE_NOT_FOUND", "Schedule not found");
            }

            UserPackage? wallet = await _dbContext.UserPackages.FirstOrDefaultAsync(w => w.Id == request.UserPackageId);
            if (wallet == null)
            {
                throw BusinessRuleException.NotFound("PACKAGE_NOT_FOUND", "Package not found");
            }

            int bookedCount = await _dbContext.Bookings
                .CountAsync(b => b.ScheduleId == schedule.Id && b.Status == BookingStatus.Booked);

            if (bookedCount < schedule.Capacity)
            {
                throw BusinessRuleException.Conflict("SCHEDULE_NOT_FULL", "Class still has free slots, book directly");
            }

            List<Booking> userActiveBookings = await _dbContext.Bookings
                .Include(b => b.Schedule)
                .Where(b => b.UserId == request.UserId && b.Status == BookingStatus.Booked)
                .ToListAsync();

            BookingRules.EnsureEligible(schedule, wallet, request.UserId, userActiveBookings, now);

            bool alreadyWaiting = await _dbContext.WaitlistEntries
                .AnyAsync(w => w.ScheduleId == schedule.Id && w.UserId == request.UserId && w.Status == WaitlistStatus.Waiting);

            if (alreadyWaiting)
            {
                throw BusinessRuleException.Conflict("ALREADY_WAITLISTED", "User is already on the waitlist");
            }

            WaitlistEntry entry = new WaitlistEntry
            {
                UserId = request.UserId,
                ScheduleId = schedule.Id,
                UserPackageId = wallet.Id,
                Status = WaitlistStatus.Waiting,
                JoinedAt = now
            };

            _dbContext.WaitlistEntries.Add(entry);
            await _dbContext.SaveChangesAsync();

            int position = await _dbContext.WaitlistEntries
                .CountAsync(w => w.ScheduleId == schedule.Id && w.Status == WaitlistStatus.Waiting && w.JoinedAt <= entry.JoinedAt);

            return new WaitlistResponse
            {
                WaitlistEntryId = entry.Id,
                ScheduleId = schedule.Id,
                Position = position,
                JoinedAt = entry.JoinedAt
            };
        }

        public async Task<int?> PromoteNextAsync(TimetableSchedule schedule, DateTime now)
        {
            List<WaitlistEntry> waiting = await _dbContext.WaitlistEntries
                .Include(w => w.UserPackage)
                .Where(w => w.ScheduleId == schedule.Id && w.Status == WaitlistStatus.Waiting)
                .OrderBy(w => w.JoinedAt)
                .ThenBy(w => w.Id)
                .ToListAsync();

            foreach (WaitlistEntry entry in waiting)
            {
                List<Booking> userActiveBookings = await _dbContext.Bookings
                    .Include(b => b.Schedule)
                    .Where(b => b.UserId == entry.UserId && b.Status == BookingStatus.Booked)
                    .ToListAsync();

                try
                {
                    BookingRules.EnsureEligible(schedule, entry.UserPackage, entry.UserId, userActiveBookings, now);
                }
                catch (BusinessRuleException ex)
                {
                    entry.Status = WaitlistStatus.Skipped;
                    entry.ResolvedAt = now;
                    entry.Note = ex.Code;
                    continue;
                }

                Booking booking = new Booking
                {
                    UserId = entry.UserId,
                    ScheduleId = schedule.Id,
                    UserPackageId = entry.UserPackageId,
                    Status = BookingStatus.Booked,
                    BookedAt = now
                };

                entry.UserPackage.DeductCredit();
                entry.Status = WaitlistStatus.Promoted;
                entry.ResolvedAt = now;
                _dbContext.Bookings.Add(booking);

                return entry.UserId;
            }

            return null;
        }

        public async Task<List<WaitlistEntryDto>> ListAsync(int scheduleId)
        {
            List<WaitlistEntry> entries = await _dbContext.WaitlistEntries
                .Include(w => w.User)
                .Where(w => w.ScheduleId == scheduleId)
                .OrderBy(w => w.JoinedAt)
                .ThenBy(w => w.Id)
                .ToListAsync();

            List<WaitlistEntryDto> result = new List<WaitlistEntryDto>();
            int position = 0;

            foreach (WaitlistEntry entry in entries)
            {
                int? entryPosition = null;

                if (entry.Status == WaitlistStatus.Waiting)
                {
                    position++;
                    entryPosition = position;
                }

                result.Add(new WaitlistEntryDto
                {
                    WaitlistEntryId = entry.Id,
                    UserId = entry.UserId,
                    UserName = entry.User.Name,
                    Status = entry.Status.ToString(),
                    Position = entryPosition,
                    JoinedAt = entry.JoinedAt,
                    ResolvedAt = entry.ResolvedAt,
                    Note = entry.Note
                });
            }

            return result;
        }

        public async Task<int> ExpireEndedAsync()
        {
            DateTime now = _clock.UtcNow;

            List<WaitlistEntry> expired = await _dbContext.WaitlistEntries
                .Where(w => w.Status == WaitlistStatus.Waiting && w.Schedule.EndTime < now)
                .ToListAsync();

            foreach (WaitlistEntry entry in expired)
            {
                entry.Status = WaitlistStatus.Expired;
                entry.ResolvedAt = now;
                entry.Note = "SCHEDULE_ENDED";
            }

            if (expired.Count > 0)
            {
                await _dbContext.SaveChangesAsync();
                await _cacheService.RemoveAsync(CacheKeys.Timetable);
            }

            return expired.Count;
        }
    }
}
