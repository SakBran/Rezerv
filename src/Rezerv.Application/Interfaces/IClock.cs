using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Rezerv.Application.Interfaces
{
    public interface IClock
    {
        DateTime UtcNow { get; }
    }
}