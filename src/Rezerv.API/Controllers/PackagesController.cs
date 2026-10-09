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
    public class PackagesController : ControllerBase
    {
        private readonly PackageService _packageService;
        public PackagesController(PackageService packageService)
        {
            _packageService = packageService;
        }
        [HttpGet]
        public async Task<IActionResult> List([FromQuery] int? businessId)
        {
            return Ok(await _packageService.ListAsync(businessId));
        }

        [HttpPost("purchase")]
        public async Task<IActionResult> Purchase(PurchaseRequest req)
        {
            return Created("", await _packageService.PurchaseAsync(req));
        }
    }
}