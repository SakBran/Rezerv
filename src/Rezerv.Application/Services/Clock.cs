using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Rezerv.Application.Interfaces;

namespace Rezerv.Application.Services
{
    public class Clock : IClock
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }
}