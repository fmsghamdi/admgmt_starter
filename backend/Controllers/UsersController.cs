using admgmt_backend.Services;
using admgmt_backend.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace admgmt_backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly IADService _ad;

        public UsersController(IADService ad)
        {
            _ad = ad;
        }

        // GET /api/users  (بحث متقدّم)
        [HttpGet]
        public async Task<IActionResult> GetUsers([FromQuery] UsersQueryOptions options)
        {
            var result = await _ad.GetUsersAdvancedAsync(options);
            return Ok(result);
        }

        // GET /api/users/details?sam=...&dn=...
        [HttpGet("details")]
        public async Task<IActionResult> GetDetails([FromQuery] string? sam, [FromQuery] string? dn)
        {
            var vm = await _ad.GetUserDetailsAsync(sam, dn);
            if (vm == null) return NotFound();
            return Ok(vm);
        }
    }
}
