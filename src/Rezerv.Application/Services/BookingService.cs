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
    public class BookingService
    {
        private const int LockWaitSeconds = 5;
        private const int LockTtlSeconds = 10;

        private readonly IApplicationDbContext _dbContext;
        private readonly IDistributedLockService _lockService;
        private readonly ICacheService _cacheService;
        private readonly IClock _clock;
        private readonly WaitlistService _waitlistService;

        public BookingService(
            IApplicationDbContext dbContext,
            IDistributedLockService lockService,
            ICacheService cacheService,
            IClock clock,
            WaitlistService waitlistService)
        {
            _dbContext = dbContext;
            _lockService = lockService;
            _cacheService = cacheService;
            _clock = clock;
            _waitlistService = waitlistService;
        }

        public async Task<BookingResponse> BookAsync(BookingRequest request)
        {
            string lockKey = CacheKeys.ScheduleLock(request.ScheduleId);
            string? token = await _lockService.TryAcquireAsync(lockKey, LockWaitSeconds, LockTtlSeconds);

            if (token == null)
            {
                throw new BusinessRuleException(503, "LOCK_TIMEOUT", "Schedule is busy, please retry");
            }

            try
            {
                return await BookInsideLockAsync(request);
            }
            finally
            {
                await _lockService.ReleaseAsync(lockKey, token);
            }
        }

        private async Task<BookingResponse> BookInsideLockAsync(BookingRequest request)
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

            List<Booking> userActiveBookings = await _dbContext.Bookings
                .Include(b => b.Schedule)
                .Where(b => b.UserId == request.UserId && b.Status == BookingStatus.Booked)
                .ToListAsync();

            int bookedCount = await _dbContext.Bookings
                .CountAsync(b => b.ScheduleId == schedule.Id && b.Status == BookingStatus.Booked);

            BookingRules.EnsureCanBook(schedule, wallet, request.UserId, userActiveBookings, bookedCount, now);

            Booking booking = new Booking
            {
                UserId = request.UserId,
                ScheduleId = schedule.Id,
                UserPackageId = wallet.Id,
                Status = BookingStatus.Booked,
                BookedAt = now
            };

            wallet.DeductCredit();
            _dbContext.Bookings.Add(booking);

            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                throw BusinessRuleException.Conflict("CONCURRENCY_CONFLICT", "Package was modified by another request, please retry");
            }

            await _cacheService.RemoveAsync(CacheKeys.Timetable);

            return new BookingResponse
            {
                BookingId = booking.Id,
                ScheduleId = schedule.Id,
                ClassName = schedule.ClassName,
                StartTime = schedule.StartTime,
                RemainingCredits = wallet.RemainingCredits,
                Status = booking.Status.ToString()
            };
        }

        public async Task<CancelResponse> CancelAsync(CancelRequest request)
        {
            Booking? booking = await _dbContext.Bookings
                .Include(b => b.Schedule)
                .Include(b => b.UserPackage)
                .FirstOrDefaultAsync(b => b.Id == request.BookingId);

            if (booking == null)
            {
                throw BusinessRuleException.NotFound("BOOKING_NOT_FOUND", "Booking not found");
            }

            if (booking.UserId != request.UserId)
            {
                throw BusinessRuleException.BadRequest("BOOKING_NOT_OWNED", "This booking does not belong to the user");
            }

            string lockKey = CacheKeys.ScheduleLock(booking.ScheduleId);
            string? token = await _lockService.TryAcquireAsync(lockKey, LockWaitSeconds, LockTtlSeconds);

            if (token == null)
            {
                throw new BusinessRuleException(503, "LOCK_TIMEOUT", "Schedule is busy, please retry");
            }

            try
            {
                return await CancelInsideLockAsync(booking);
            }
            finally
            {
                await _lockService.ReleaseAsync(lockKey, token);
            }
        }

        private async Task<CancelResponse> CancelInsideLockAsync(Booking booking)
        {
            DateTime now = _clock.UtcNow;

            if (booking.Status != BookingStatus.Booked)
            {
                throw BusinessRuleException.Conflict("BOOKING_NOT_ACTIVE", "Booking is already cancelled");
            }

            if (booking.Schedule.HasEnded(now))
            {
                throw BusinessRuleException.BadRequest("SCHEDULE_ALREADY_ENDED", "Cannot cancel a class that has ended");
            }

            bool refundable = BookingRules.IsRefundable(booking.Schedule, now);

            booking.Status = BookingStatus.Cancelled;
            booking.CancelledAt = now;
            booking.CreditRefunded = refundable;

            if (refundable)
            {
                booking.UserPackage.RefundCredit();
            }

            int? promotedUserId = null;

            if (!booking.Schedule.HasStarted(now))
            {
                promotedUserId = await _waitlistService.PromoteNextAsync(booking.Schedule, now);
            }

            await _dbContext.SaveChangesAsync();
            await _cacheService.RemoveAsync(CacheKeys.Timetable);

            return new CancelResponse
            {
                BookingId = booking.Id,
                Status = booking.Status.ToString(),
                CreditRefunded = refundable,
                RemainingCredits = booking.UserPackage.RemainingCredits,
                PromotedUserId = promotedUserId
            };
        }

        public async Task<List<BookingDto>> ListByUserAsync(int userId)
        {
            List<BookingDto> result = await _dbContext.Bookings
                .Where(b => b.UserId == userId)
                .OrderBy(b => b.Schedule.StartTime)
                .Select(b => new BookingDto
                {
                    BookingId = b.Id,
                    ScheduleId = b.ScheduleId,
                    ClassName = b.Schedule.ClassName,
                    StartTime = b.Schedule.StartTime,
                    EndTime = b.Schedule.EndTime,
                    Status = b.Status.ToString(),
                    BookedAt = b.BookedAt,
                    CancelledAt = b.CancelledAt,
                    CreditRefunded = b.CreditRefunded
                })
                .ToListAsync();

            return result;
        }
    }
}
