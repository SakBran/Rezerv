using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Rezerv.Application.Services;

namespace Rezerv.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TimetableController : ControllerBase
    {
        private readonly TimetableService _timetableService;
        public TimetableController(TimetableService timetableService)
        {
            _timetableService = timetableService;
        }
        [HttpGet]
        public async Task<IActionResult> List([FromQuery] int? businessId, [FromQuery] DateOnly? date)
        {
            return Ok(await _timetableService.ListAsync(businessId, date));
        }
    }
}