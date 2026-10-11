using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Rezerv.Application.DTO;
using Rezerv.Application.Services;

namespace Rezerv.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WaitlistController : ControllerBase
    {
        private readonly WaitlistService _waitlistService;

        public WaitlistController(WaitlistService waitlistService)
        {
            _waitlistService = waitlistService;
        }

        [HttpPost]
        public async Task<IActionResult> Join(WaitlistRequest request)
        {
            return Created("", await _waitlistService.JoinAsync(request));
        }

        [HttpGet("{scheduleId:int}")]
        public async Task<IActionResult> List(int scheduleId)
        {
            return Ok(await _waitlistService.ListAsync(scheduleId));
        }

        [HttpPost("expire-ended")]
        public async Task<IActionResult> ExpireEnded()
        {
            int count = await _waitlistService.ExpireEndedAsync();
            return Ok(new { expired = count });
        }
    }
}
