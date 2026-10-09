using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Rezerv.Application.Interfaces
{
    public interface ICacheService
    {
        Task<T?> GetAsync<T>(string key);
        Task SetAsync<T>(string key, T value, TimeSpan ttl);
        Task RemoveAsync(string key);
    }
}