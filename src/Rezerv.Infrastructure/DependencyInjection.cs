using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Rezerv.Application.Interfaces;
using Rezerv.Application.Services;
using Rezerv.Infrastructure.Persistence;
using StackExchange.Redis;

namespace Rezerv.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
        {
            var cs = config.GetConnectionString("MySql")!;
            services.AddDbContext<AppDbContext>(o => o.UseMySql(cs, ServerVersion.AutoDetect(cs)));
            services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<AppDbContext>());

            services.AddSingleton<IClock, Clock>();
            services.AddScoped<PackageService>();
            services.AddScoped<TimetableService>();

            var redis = config.GetConnectionString("Redis")!;
            services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redis));
            services.AddSingleton<ICacheService, RedisCacheService>();
            services.AddSingleton<IDistributedLockService, RedisDistributedLockService>();
            return services;
        }
    }
}