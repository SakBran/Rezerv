using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Rezerv.Application.Interfaces
{
    public interface IDistributedLockService
    {
        Task<string?> TryAcquireAsync(string key, int waitSeconds, int ttlSeconds);
        Task ReleaseAsync(string key, string token);
    }
}