using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Rezerv.Application.Interfaces;
using StackExchange.Redis;

namespace Rezerv.Infrastructure.Redis
{
    public class RedisDistributedLockService : IDistributedLockService
    {
        private readonly IConnectionMultiplexer _mux;

        public RedisDistributedLockService(IConnectionMultiplexer mux)
        {
            _mux = mux;
        }

        public async Task<string?> TryAcquireAsync(string key, int waitSeconds, int ttlSeconds)
        {
            var db = _mux.GetDatabase();
            var token = Guid.NewGuid().ToString("N");
            var deadline = DateTime.UtcNow.AddSeconds(waitSeconds);

            while (DateTime.UtcNow < deadline)
            {
                if (await db.LockTakeAsync(key, token, TimeSpan.FromSeconds(ttlSeconds)))
                    return token;
                await Task.Delay(50);
            }
            return null;
        }

        public async Task ReleaseAsync(string key, string token)
        {
            await _mux.GetDatabase().LockReleaseAsync(key, token);
        }
    }
}