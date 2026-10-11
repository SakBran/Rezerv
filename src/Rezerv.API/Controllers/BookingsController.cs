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
    public class BookingsController : ControllerBase
    {
        private readonly BookingService _bookingService;

        public BookingsController(BookingService bookingService)
        {
            _bookingService = bookingService;
        }

        [HttpPost]
        public async Task<IActionResult> Book(BookingRequest request)
        {
            return Created("", await _bookingService.BookAsync(request));
        }

        [HttpPost("cancel")]
        public async Task<IActionResult> Cancel(CancelRequest request)
        {
            return Ok(await _bookingService.CancelAsync(request));
        }

        [HttpGet]
        public async Task<IActionResult> List([FromQuery] int userId)
        {
            return Ok(await _bookingService.ListByUserAsync(userId));
        }
    }
}
