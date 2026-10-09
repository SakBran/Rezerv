using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Rezerv.Application.DTO;
using Rezerv.Application.Exceptions;
using Rezerv.Application.Interfaces;
using Rezerv.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Rezerv.Application.Services
{
    public class PackageService
    {
        private readonly IApplicationDbContext _db;
        private readonly IClock _clock;
        public PackageService(IApplicationDbContext db, IClock clock)
        {
            _db = db;
            _clock = clock;
        }
        public async Task<List<PackageDto>> ListAsync(int? businessId)
        {
            var q = _db.Packages.Include(p => p.Business).AsQueryable();
            if (businessId is not null)
            {
                q = q.Where(p => p.BusinessId == businessId);
            }
            return await q.Select(p => new PackageDto
            {
                PackageId = p.Id,
                BusinessId = p.BusinessId,
                BusinessName = p.Business.Name,
                Name = p.Name,
                TotalCredits = p.TotalCredits,
                ValidityDays = p.ValidityDays,
                Price = p.Price
            }).ToListAsync();
        }

        public async Task<PurchaseResponse> PurchaseAsync(PurchaseRequest req)
        {
            var user = await _db.Users.FindAsync(req.UserId);
            if (user == null)
            {
                throw BusinessRuleException.NotFound("USER_NOT_FOUND", "User not found");
            }

            var pkg = await _db.Packages.FindAsync(req.PackageId) ?? throw BusinessRuleException.NotFound("PACKAGE_NOT_FOUND", "Package not found");

            var wallet = new UserPackage
            {
                UserId = req.UserId,
                PackageId = pkg.Id,
                BusinessId = pkg.BusinessId,
                TotalCredits = pkg.TotalCredits,
                RemainingCredits = pkg.TotalCredits,
                PurchasedAt = _clock.UtcNow,
                ExpiresAt = _clock.UtcNow.AddDays(pkg.ValidityDays)
            };
            _db.UserPackages.Add(wallet);
            await _db.SaveChangesAsync();
            return new PurchaseResponse { UserPackageId = wallet.Id, RemainingCredits = wallet.RemainingCredits, ExpiresAt = wallet.ExpiresAt };
        }
    }
}