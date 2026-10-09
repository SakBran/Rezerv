using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Rezerv.Domain.Entities;
using Rezerv.Domain.Enums;

namespace Rezerv.Infrastructure.Persistence
{
    public class DbSeeder
    {
        public static async Task SeedAsync(AppDbContext db)
        {
            if (await db.Businesses.AnyAsync()) return;

            var now = DateTime.UtcNow;
            var day = now.Date.AddDays(2);


            var fit = new Business { Name = "Rezerv Fitness" };
            var zen = new Business { Name = "Zen Yoga Studio" };
            db.Businesses.AddRange(fit, zen);


            var fit10 = new Package { Business = fit, Name = "10 Class Pack", TotalCredits = 10, ValidityDays = 30, Price = 100 };
            var fit5 = new Package { Business = fit, Name = "5 Class Pack", TotalCredits = 5, ValidityDays = 14, Price = 60 };
            var zen5 = new Package { Business = zen, Name = "5 Class Pack", TotalCredits = 5, ValidityDays = 30, Price = 70 };
            db.Packages.AddRange(fit10, fit5, zen5);


            var names = new[] { "Su Su", "Kyaw Kyaw", "Maw Maw", "Aung Aung", "Ma Ma", "Ko Ko", "Hla Hla", "Thura", "Ko Oo", "Hlwan Paing", "Kyat Pha", "Nay Win" };
            var users = names.Select(n => new User { Name = n, Email = $"{n.ToLower()}@test.com" }).ToList();
            db.Users.AddRange(users);
            User U(int i) => users[i - 1];


            UserPackage W(User u, Package p, int remaining, int expiresInDays) => new()
            {
                User = u,
                Package = p,
                BusinessId = 0,
                TotalCredits = p.TotalCredits,
                RemainingCredits = remaining,
                PurchasedAt = now.AddDays(-5),
                ExpiresAt = now.AddDays(expiresInDays)
            };
            var wallets = new List<UserPackage>
        {
            W(U(1), fit10, 10, 25),
            W(U(2), fit5,  0,  10),
            W(U(3), fit10, 8,  -1),
            W(U(4), zen5,  4,  25),
            W(U(5), fit10, 10, 25),
            W(U(6), fit10, 10, 25),
            W(U(7), fit10, 10, 25),
            W(U(8), fit10, 10, 25),
            W(U(9), fit10, 10, 25),
            W(U(10), fit10, 10, 25),
            W(U(11), fit10, 10, 25),
            W(U(12), fit10, 10, 25),
        };
            db.UserPackages.AddRange(wallets);


            TimetableSchedule S(Business b, string cls, string ins, DateTime start, int mins, int cap) => new()
            {
                Business = b,
                ClassName = cls,
                InstructorName = ins,
                StartTime = start,
                EndTime = start.AddMinutes(mins),
                Capacity = cap
            };
            var s = new List<TimetableSchedule>
        {
            S(fit, "Yoga Class",     "Aung Ko",       day.AddHours(10), 60, 15),
            S(fit, "HIIT Blast",     "Su Su Hlaing",  day.AddHours(10).AddMinutes(30), 45, 10),
            S(fit, "Spin Class",     "Kyaw Zin",      day.AddHours(18), 45, 2),
            S(fit, "Pilates",        "Thin Thin Aye", now.AddHours(2),  60, 5),
            S(fit, "Boxing Basics",  "Min Thu",       now.AddHours(6),  60, 5),
            S(fit, "Strength 101",   "Zaw Lin",       day.AddDays(1).AddHours(9), 60, 1),
            S(fit, "Morning Yoga",   "Aung Ko",       now.AddDays(-1),  60, 1),
            S(zen, "Hatha Yoga",     "May Thet",      day.AddHours(8),  60, 8),
            S(zen, "Meditation",     "May Thet",      day.AddHours(12), 30, 3),
            S(zen, "Vinyasa Flow",   "Hnin Wai",      day.AddDays(2).AddHours(17), 60, 12),
        };
            db.Schedules.AddRange(s);


            Booking B(User u, TimetableSchedule sch, UserPackage w) => new()
            {
                User = u,
                Schedule = sch,
                UserPackage = w,
                Status = BookingStatus.Booked,
                BookedAt = now.AddDays(-1)
            };
            var bookings = new List<Booking>
        {
            B(U(1), s[0], wallets[0]),  B(U(5), s[0], wallets[4]),
            B(U(11), s[1], wallets[10]), B(U(12), s[1], wallets[11]),
            B(U(6), s[2], wallets[5]),  B(U(7), s[2], wallets[6]),
            B(U(1), s[3], wallets[0]),
            B(U(1), s[4], wallets[0]),
            B(U(6), s[5], wallets[5]),
            B(U(1), s[6], wallets[0]),
            B(U(4), s[8], wallets[3]), B(U(1), s[8], wallets[0]), B(U(2), s[8], wallets[1]),
        };
            db.Bookings.AddRange(bookings);


            WaitlistEntry WL(User u, TimetableSchedule sch, UserPackage w, int minutesAgo) => new()
            {
                User = u,
                Schedule = sch,
                UserPackage = w,
                Status = WaitlistStatus.Waiting,
                JoinedAt = now.AddMinutes(-minutesAgo)
            };
            db.WaitlistEntries.AddRange(
                WL(U(8), s[2], wallets[7], 30),
                WL(U(9), s[2], wallets[8], 20),
                WL(U(2), s[5], wallets[1], 30),
                WL(U(10), s[5], wallets[9], 20),
                WL(U(8), s[6], wallets[7], 60)
            );

            await db.SaveChangesAsync();


            foreach (var w in wallets) w.BusinessId = w.Package.BusinessId;
            await db.SaveChangesAsync();

            Console.WriteLine($"Seeded {users.Count} users, 2 businesses, {s.Count} schedules, {bookings.Count} bookings, 5 waitlist entries");
        }
    }
}