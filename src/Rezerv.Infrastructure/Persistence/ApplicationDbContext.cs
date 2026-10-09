using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Rezerv.Application.Interfaces;
using Rezerv.Domain.Entities;

namespace Rezerv.Infrastructure.Persistence
{
    public class AppDbContext : DbContext, IApplicationDbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Business> Businesses => Set<Business>();
        public DbSet<User> Users => Set<User>();
        public DbSet<Package> Packages => Set<Package>();
        public DbSet<UserPackage> UserPackages => Set<UserPackage>();
        public DbSet<TimetableSchedule> Schedules => Set<TimetableSchedule>();
        public DbSet<Booking> Bookings => Set<Booking>();
        public DbSet<WaitlistEntry> WaitlistEntries => Set<WaitlistEntry>();

        protected override void OnModelCreating(ModelBuilder b)
        {
            b.Entity<UserPackage>(e =>
            {
                e.Property(x => x.RemainingCredits).IsConcurrencyToken();   // layer 3
                e.HasIndex(x => new { x.UserId, x.BusinessId });
            });

            b.Entity<Booking>(e =>
            {
                e.HasIndex(x => new { x.ScheduleId, x.Status });
                e.HasIndex(x => new { x.UserId, x.Status });
                e.HasOne(x => x.UserPackage).WithMany().OnDelete(DeleteBehavior.Restrict);
            });

            b.Entity<WaitlistEntry>(e =>
            {
                e.HasIndex(x => new { x.ScheduleId, x.Status, x.JoinedAt });
                e.HasOne(x => x.UserPackage).WithMany().OnDelete(DeleteBehavior.Restrict);
            });

            b.Entity<Package>().Property(x => x.Price).HasPrecision(10, 2);
        }
    }
}