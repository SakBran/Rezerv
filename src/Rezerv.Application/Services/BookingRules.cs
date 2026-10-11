using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Rezerv.Application.Exceptions;
using Rezerv.Domain.Entities;
using Rezerv.Domain.Enums;

namespace Rezerv.Application.Services
{
    public static class BookingRules
    {
        public static void EnsureCanBook(
            TimetableSchedule schedule,
            UserPackage wallet,
            int userId,
            List<Booking> userActiveBookings,
            int bookedCount,
            DateTime now)
        {
            EnsureEligible(schedule, wallet, userId, userActiveBookings, now);

            if (bookedCount >= schedule.Capacity)
            {
                throw BusinessRuleException.Conflict("SCHEDULE_FULL", "This class is full");
            }
        }

        public static void EnsureEligible(
            TimetableSchedule schedule,
            UserPackage wallet,
            int userId,
            List<Booking> userActiveBookings,
            DateTime now)
        {
            if (schedule.HasStarted(now))
            {
                throw BusinessRuleException.BadRequest("SCHEDULE_ALREADY_STARTED", "This class has already started");
            }

            if (wallet.UserId != userId)
            {
                throw BusinessRuleException.BadRequest("PACKAGE_NOT_OWNED", "This package does not belong to the user");
            }

            if (wallet.IsExpired(now))
            {
                throw BusinessRuleException.BadRequest("PACKAGE_EXPIRED", "This package has expired");
            }

            if (wallet.BusinessId != schedule.BusinessId)
            {
                throw BusinessRuleException.BadRequest("BUSINESS_MISMATCH", "This package cannot be used for this business");
            }

            if (wallet.RemainingCredits < 1)
            {
                throw BusinessRuleException.BadRequest("INSUFFICIENT_CREDITS", "No credits remaining");
            }

            foreach (Booking booking in userActiveBookings)
            {
                if (booking.ScheduleId == schedule.Id)
                {
                    throw BusinessRuleException.Conflict("ALREADY_BOOKED", "User already booked this class");
                }

                if (booking.Schedule.OverlapsWith(schedule))
                {
                    throw BusinessRuleException.BadRequest("OVERLAPPING_BOOKING", "User has another class at this time");
                }
            }
        }

        public static bool IsRefundable(TimetableSchedule schedule, DateTime now)
        {
            return now < schedule.StartTime.AddHours(-4);
        }
    }
}
