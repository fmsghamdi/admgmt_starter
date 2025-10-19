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

        // GET api/users
        [HttpGet]
        public async Task<IActionResult> GetUsers([FromQuery] string? q, [FromQuery] string? ouDn,
            [FromQuery] UserStatusFilter status = UserStatusFilter.Any,
            [FromQuery] int take = 100, [FromQuery] int skip = 0,
            [FromQuery] string? sortBy = "displayName", [FromQuery] bool desc = false)
        {
            var opts = new UsersQueryOptions
            {
                Q = q,
                OuDn = ouDn,
                Status = status,
                Take = take,
                Skip = skip,
                SortBy = sortBy,
                Desc = desc
            };

            var (items, total) = await _ad.GetUsersAdvancedAsync(opts);
            return Ok(new { items, total });
        }
        
        [HttpGet("ping")]
public IActionResult Ping()
{
    return Ok(new { ok = true, serverTimeUtc = DateTime.UtcNow });
}


        // GET api/users/details?sam=...&dn=...
        [HttpGet("details")]
        public async Task<IActionResult> GetDetails([FromQuery] string? sam, [FromQuery] string? dn)
        {
            var vm = await _ad.GetUserDetailsAsync(sam, dn);
            if (vm == null) return NotFound();
            return Ok(vm);
        }
    }
}
