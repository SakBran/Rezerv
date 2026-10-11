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
    public class WaitlistTests
    {
        private readonly DateTime _now = new DateTime(2026, 10, 20, 8, 0, 0, DateTimeKind.Utc);

        private (BookingService booking, WaitlistService waitlist, AppDbContext db, FakeClock clock) Build()
        {
            DbContextOptions<AppDbContext> options = TestDb.NewOptions();
            AppDbContext db = TestDb.Create(options);
            TestDb.SeedBasic(db, _now, 1, 4);

            FakeClock clock = new FakeClock { UtcNow = _now };
            InProcessLockService lockService = new InProcessLockService();
            NoopCacheService cache = new NoopCacheService();
            WaitlistService waitlist = new WaitlistService(db, lockService, cache, clock);
            BookingService booking = new BookingService(db, lockService, cache, clock, waitlist);

            return (booking, waitlist, db, clock);
        }

        [Fact]
        public async Task Join_WhenNotFull_Fails()
        {
            var (_, waitlist, _, _) = Build();

            BusinessRuleException ex = await Assert.ThrowsAsync<BusinessRuleException>(
                () => waitlist.JoinAsync(new WaitlistRequest { UserId = 1, ScheduleId = 1, UserPackageId = 1 }));

            Assert.Equal("SCHEDULE_NOT_FULL", ex.Code);
        }

        [Fact]
        public async Task Join_DoesNotDeductCredit()
        {
            var (booking, waitlist, db, _) = Build();
            await booking.BookAsync(new BookingRequest { UserId = 1, ScheduleId = 1, UserPackageId = 1 });

            await waitlist.JoinAsync(new WaitlistRequest { UserId = 2, ScheduleId = 1, UserPackageId = 2 });

            UserPackage? wallet = await db.UserPackages.FindAsync(2);
            Assert.Equal(5, wallet!.RemainingCredits);
        }

        [Fact]
        public async Task Cancel_PromotesFirstWaiter_Fifo()
        {
            var (booking, waitlist, db, clock) = Build();
            BookingResponse booked = await booking.BookAsync(new BookingRequest { UserId = 1, ScheduleId = 1, UserPackageId = 1 });

            await waitlist.JoinAsync(new WaitlistRequest { UserId = 2, ScheduleId = 1, UserPackageId = 2 });
            clock.UtcNow = _now.AddMinutes(1);
            await waitlist.JoinAsync(new WaitlistRequest { UserId = 3, ScheduleId = 1, UserPackageId = 3 });

            CancelResponse cancelled = await booking.CancelAsync(new CancelRequest { UserId = 1, BookingId = booked.BookingId });

            Assert.Equal(2, cancelled.PromotedUserId);
            UserPackage? wallet = await db.UserPackages.FindAsync(2);
            Assert.Equal(4, wallet!.RemainingCredits);
            Assert.Equal(1, await db.Bookings.CountAsync(b => b.UserId == 2 && b.Status == BookingStatus.Booked));
        }

        [Fact]
        public async Task Cancel_SkipsIneligibleWaiter()
        {
            var (booking, waitlist, db, clock) = Build();
            BookingResponse booked = await booking.BookAsync(new BookingRequest { UserId = 1, ScheduleId = 1, UserPackageId = 1 });

            await waitlist.JoinAsync(new WaitlistRequest { UserId = 2, ScheduleId = 1, UserPackageId = 2 });
            clock.UtcNow = _now.AddMinutes(1);
            await waitlist.JoinAsync(new WaitlistRequest { UserId = 3, ScheduleId = 1, UserPackageId = 3 });

            UserPackage? wallet2 = await db.UserPackages.FindAsync(2);
            wallet2!.RemainingCredits = 0;
            await db.SaveChangesAsync();

            CancelResponse cancelled = await booking.CancelAsync(new CancelRequest { UserId = 1, BookingId = booked.BookingId });

            Assert.Equal(3, cancelled.PromotedUserId);
            WaitlistEntry skipped = await db.WaitlistEntries.FirstAsync(w => w.UserId == 2);
            Assert.Equal(WaitlistStatus.Skipped, skipped.Status);
        }

        [Fact]
        public async Task ExpireEnded_MarksExpired_NoDeduction()
        {
            var (booking, waitlist, db, clock) = Build();
            await booking.BookAsync(new BookingRequest { UserId = 1, ScheduleId = 1, UserPackageId = 1 });
            await waitlist.JoinAsync(new WaitlistRequest { UserId = 2, ScheduleId = 1, UserPackageId = 2 });

            clock.UtcNow = _now.AddHours(12);
            int count = await waitlist.ExpireEndedAsync();

            Assert.Equal(1, count);
            UserPackage? wallet = await db.UserPackages.FindAsync(2);
            Assert.Equal(5, wallet!.RemainingCredits);
        }
    }
}
