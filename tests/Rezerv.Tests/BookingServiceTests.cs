using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Rezerv.Application.DTO;
using Rezerv.Application.Exceptions;
using Rezerv.Application.Services;
using Rezerv.Domain.Entities;
using Rezerv.Domain.Enums;
using Rezerv.Infrastructure.Persistence;
using Xunit;

namespace Rezerv.Tests
{
    public class BookingServiceTests
    {
        private readonly DateTime _now = new DateTime(2026, 10, 20, 8, 0, 0, DateTimeKind.Utc);

        private (BookingService booking, AppDbContext db, FakeClock clock) Build(int capacity = 5, int users = 3)
        {
            DbContextOptions<AppDbContext> options = TestDb.NewOptions();
            AppDbContext db = TestDb.Create(options);
            TestDb.SeedBasic(db, _now, capacity, users);

            FakeClock clock = new FakeClock { UtcNow = _now };
            InProcessLockService lockService = new InProcessLockService();
            NoopCacheService cache = new NoopCacheService();
            WaitlistService waitlist = new WaitlistService(db, lockService, cache, clock);
            BookingService booking = new BookingService(db, lockService, cache, clock, waitlist);

            return (booking, db, clock);
        }

        [Fact]
        public async Task Book_DeductsOneCredit()
        {
            var (service, db, _) = Build();

            BookingResponse response = await service.BookAsync(new BookingRequest { UserId = 1, ScheduleId = 1, UserPackageId = 1 });

            Assert.Equal(4, response.RemainingCredits);
            Assert.Equal(1, await db.Bookings.CountAsync(b => b.Status == BookingStatus.Booked));
        }

        [Fact]
        public async Task Book_ExpiredPackage_Fails()
        {
            var (service, db, _) = Build();
            UserPackage? wallet = await db.UserPackages.FindAsync(1);
            wallet!.ExpiresAt = _now.AddDays(-1);
            await db.SaveChangesAsync();

            BusinessRuleException ex = await Assert.ThrowsAsync<BusinessRuleException>(
                () => service.BookAsync(new BookingRequest { UserId = 1, ScheduleId = 1, UserPackageId = 1 }));

            Assert.Equal("PACKAGE_EXPIRED", ex.Code);
        }

        [Fact]
        public async Task Book_ZeroCredits_Fails()
        {
            var (service, db, _) = Build();
            UserPackage? wallet = await db.UserPackages.FindAsync(1);
            wallet!.RemainingCredits = 0;
            await db.SaveChangesAsync();

            BusinessRuleException ex = await Assert.ThrowsAsync<BusinessRuleException>(
                () => service.BookAsync(new BookingRequest { UserId = 1, ScheduleId = 1, UserPackageId = 1 }));

            Assert.Equal("INSUFFICIENT_CREDITS", ex.Code);
        }

        [Fact]
        public async Task Book_WrongBusiness_Fails()
        {
            var (service, db, _) = Build();
            db.Businesses.Add(new Business { Id = 2, Name = "Other" });
            UserPackage? wallet = await db.UserPackages.FindAsync(1);
            wallet!.BusinessId = 2;
            await db.SaveChangesAsync();

            BusinessRuleException ex = await Assert.ThrowsAsync<BusinessRuleException>(
                () => service.BookAsync(new BookingRequest { UserId = 1, ScheduleId = 1, UserPackageId = 1 }));

            Assert.Equal("BUSINESS_MISMATCH", ex.Code);
        }

        [Fact]
        public async Task Book_FullSchedule_Fails()
        {
            var (service, _, _) = Build(1, 2);
            await service.BookAsync(new BookingRequest { UserId = 1, ScheduleId = 1, UserPackageId = 1 });

            BusinessRuleException ex = await Assert.ThrowsAsync<BusinessRuleException>(
                () => service.BookAsync(new BookingRequest { UserId = 2, ScheduleId = 1, UserPackageId = 2 }));

            Assert.Equal("SCHEDULE_FULL", ex.Code);
        }

        [Fact]
        public async Task Book_OverlappingSchedule_Fails()
        {
            var (service, db, _) = Build();
            db.Schedules.Add(new TimetableSchedule
            {
                Id = 2,
                BusinessId = 1,
                ClassName = "HIIT",
                InstructorName = "X",
                StartTime = _now.AddHours(10).AddMinutes(30),
                EndTime = _now.AddHours(11).AddMinutes(30),
                Capacity = 5
            });
            await db.SaveChangesAsync();

            await service.BookAsync(new BookingRequest { UserId = 1, ScheduleId = 1, UserPackageId = 1 });

            BusinessRuleException ex = await Assert.ThrowsAsync<BusinessRuleException>(
                () => service.BookAsync(new BookingRequest { UserId = 1, ScheduleId = 2, UserPackageId = 1 }));

            Assert.Equal("OVERLAPPING_BOOKING", ex.Code);
        }

        [Fact]
        public async Task Book_BackToBackSchedule_Succeeds()
        {
            var (service, db, _) = Build();
            db.Schedules.Add(new TimetableSchedule
            {
                Id = 2,
                BusinessId = 1,
                ClassName = "Next",
                InstructorName = "X",
                StartTime = _now.AddHours(11),
                EndTime = _now.AddHours(12),
                Capacity = 5
            });
            await db.SaveChangesAsync();

            await service.BookAsync(new BookingRequest { UserId = 1, ScheduleId = 1, UserPackageId = 1 });
            BookingResponse second = await service.BookAsync(new BookingRequest { UserId = 1, ScheduleId = 2, UserPackageId = 1 });

            Assert.Equal(3, second.RemainingCredits);
        }

        [Fact]
        public async Task Cancel_MoreThan4HoursBefore_Refunds()
        {
            var (service, _, clock) = Build();
            BookingResponse booked = await service.BookAsync(new BookingRequest { UserId = 1, ScheduleId = 1, UserPackageId = 1 });

            clock.UtcNow = _now.AddHours(10).AddHours(-4).AddSeconds(-1);
            CancelResponse cancelled = await service.CancelAsync(new CancelRequest { UserId = 1, BookingId = booked.BookingId });

            Assert.True(cancelled.CreditRefunded);
            Assert.Equal(5, cancelled.RemainingCredits);
        }

        [Fact]
        public async Task Cancel_ExactlyAt4Hours_NoRefund()
        {
            var (service, _, clock) = Build();
            BookingResponse booked = await service.BookAsync(new BookingRequest { UserId = 1, ScheduleId = 1, UserPackageId = 1 });

            clock.UtcNow = _now.AddHours(10).AddHours(-4);
            CancelResponse cancelled = await service.CancelAsync(new CancelRequest { UserId = 1, BookingId = booked.BookingId });

            Assert.False(cancelled.CreditRefunded);
            Assert.Equal(4, cancelled.RemainingCredits);
        }
    }
}
