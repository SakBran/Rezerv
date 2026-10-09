using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Rezerv.Application.Common
{
    public class CacheKeys
    {
        public const string Timetable = "timetable:all";
        public static string ScheduleLock(int scheduleId) => $"lock:schedule:{scheduleId}";
    }
}