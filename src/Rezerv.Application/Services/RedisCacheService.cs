using System.Text.Json;
using Rezerv.Application.Interfaces;
using StackExchange.Redis;

namespace Rezerv.Application.Services
{
    public class RedisCacheService : ICacheService
    {
        private readonly IConnectionMultiplexer _mux;
        private IDatabase Db => _mux.GetDatabase();

        public RedisCacheService(IConnectionMultiplexer mux)
        {
            _mux = mux;
        }

        public async Task<T?> GetAsync<T>(string key)
        {
            try
            {
                var v = await Db.StringGetAsync(key);
                return v.HasValue ? JsonSerializer.Deserialize<T>(v.ToString()) : default;
            }
            catch { return default; }   // Redis down → DB ကို သွား (fail-open)
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan ttl)
        {
            try { await Db.StringSetAsync(key, JsonSerializer.Serialize(value), ttl); } catch { }
        }

        public async Task RemoveAsync(string key)
        {
            try { await Db.KeyDeleteAsync(key); } catch { }
        }
    }
}