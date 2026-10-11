using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Rezerv.Application.DTO;
using Rezerv.Application.Exceptions;
using Rezerv.Application.Services;
using Rezerv.Domain.Enums;
using Rezerv.Infrastructure.Persistence;
using Xunit;

namespace Rezerv.Tests
{
    public class ConcurrencyTests
    {
        [Fact]
        public async Task TenUsers_FiveSlots_ExactlyFiveBooked()
        {
            DateTime now = new DateTime(2026, 10, 20, 8, 0, 0, DateTimeKind.Utc);
            DbContextOptions<AppDbContext> options = TestDb.NewOptions();

            using (AppDbContext seedDb = TestDb.Create(options))
            {
                TestDb.SeedBasic(seedDb, now, 5, 10);
            }

            InProcessLockService lockService = new InProcessLockService();
            NoopCacheService cache = new NoopCacheService();
            FakeClock clock = new FakeClock { UtcNow = now };

            List<Task<string>> tasks = new List<Task<string>>();

            for (int i = 1; i <= 10; i++)
            {
                int userId = i;
                tasks.Add(Task.Run(async () =>
                {
                    using (AppDbContext db = TestDb.Create(options))
                    {
                        WaitlistService waitlist = new WaitlistService(db, lockService, cache, clock);
                        BookingService booking = new BookingService(db, lockService, cache, clock, waitlist);

                        try
                        {
                            await booking.BookAsync(new BookingRequest { UserId = userId, ScheduleId = 1, UserPackageId = userId });
                            return "OK";
                        }
                        catch (BusinessRuleException ex)
                        {
                            return ex.Code;
                        }
                    }
                }));
            }

            string[] results = await Task.WhenAll(tasks);

            using (AppDbContext checkDb = TestDb.Create(options))
            {
                int booked = await checkDb.Bookings.CountAsync(b => b.Status == BookingStatus.Booked);
                int creditsSpent = await checkDb.UserPackages.SumAsync(w => 5 - w.RemainingCredits);

                Assert.Equal(5, booked);
                Assert.Equal(5, creditsSpent);
            }

            Assert.Equal(5, results.Count(r => r == "OK"));
            Assert.Equal(5, results.Count(r => r == "SCHEDULE_FULL"));
        }
    }
}
