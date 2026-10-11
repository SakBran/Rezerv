using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Rezerv.Application.Interfaces;
using Rezerv.Domain.Entities;
using Rezerv.Infrastructure.Persistence;

namespace Rezerv.Tests
{
    public class FakeClock : IClock
    {
        public DateTime UtcNow { get; set; }
    }

    public class NoopCacheService : ICacheService
    {
        public Task<T?> GetAsync<T>(string key) => Task.FromResult<T?>(default);
        public Task SetAsync<T>(string key, T value, TimeSpan ttl) => Task.CompletedTask;
        public Task RemoveAsync(string key) => Task.CompletedTask;
    }

    public class InProcessLockService : IDistributedLockService
    {
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks = new ConcurrentDictionary<string, SemaphoreSlim>();

        public async Task<string?> TryAcquireAsync(string key, int waitSeconds, int ttlSeconds)
        {
            SemaphoreSlim semaphore = Locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
            bool acquired = await semaphore.WaitAsync(TimeSpan.FromSeconds(waitSeconds));
            return acquired ? Guid.NewGuid().ToString("N") : null;
        }

        public Task ReleaseAsync(string key, string token)
        {
            Locks[key].Release();
            return Task.CompletedTask;
        }
    }

    public static class TestDb
    {
        public static DbContextOptions<AppDbContext> NewOptions()
        {
            return new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
        }

        public static AppDbContext Create(DbContextOptions<AppDbContext> options)
        {
            return new AppDbContext(options);
        }

        public static void SeedBasic(AppDbContext db, DateTime now, int capacity, int userCount)
        {
            Business business = new Business { Id = 1, Name = "Studio" };
            Package package = new Package { Id = 1, BusinessId = 1, Name = "Pack", TotalCredits = 5, ValidityDays = 30, Price = 50 };
            TimetableSchedule schedule = new TimetableSchedule
            {
                Id = 1,
                BusinessId = 1,
                ClassName = "Yoga",
                InstructorName = "Aung Ko",
                StartTime = now.AddHours(10),
                EndTime = now.AddHours(11),
                Capacity = capacity
            };

            db.Businesses.Add(business);
            db.Packages.Add(package);
            db.Schedules.Add(schedule);

            for (int i = 1; i <= userCount; i++)
            {
                db.Users.Add(new User { Id = i, Name = "User" + i, Email = "u" + i + "@test.com" });
                db.UserPackages.Add(new UserPackage
                {
                    Id = i,
                    UserId = i,
                    PackageId = 1,
                    BusinessId = 1,
                    TotalCredits = 5,
                    RemainingCredits = 5,
                    PurchasedAt = now.AddDays(-1),
                    ExpiresAt = now.AddDays(29)
                });
            }

            db.SaveChanges();
        }
    }
}
