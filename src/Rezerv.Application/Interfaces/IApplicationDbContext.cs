using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Rezerv.Domain.Entities;


namespace Rezerv.Application.Interfaces
{
    public interface IApplicationDbContext
    {
        DbSet<Business> Businesses { get; }
        DbSet<User> Users { get; }
        DbSet<Package> Packages { get; }
        DbSet<UserPackage> UserPackages { get; }
        DbSet<TimetableSchedule> Schedules { get; }
        DbSet<Booking> Bookings { get; }
        DbSet<WaitlistEntry> WaitlistEntries { get; }
        Task<int> SaveChangesAsync(CancellationToken ct = default);
    }
}