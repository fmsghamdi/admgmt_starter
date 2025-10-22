using Microsoft.AspNetCore.Mvc;
using admgmt_backend.Services;

namespace admgmt_backend.Controllers
{
    [ApiController]
    [Route("api/users")]
    public class UsersController : ControllerBase
    {
        private readonly IADService _ad;

        public UsersController(IADService ad)
        {
            _ad = ad;
        }

        // GET api/users?search=&page=1&pageSize=50
        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            var (items, total) = await _ad.GetUsersAsync(search, page, pageSize);
            return Ok(new { items, total });
        }

        // GET api/users/advanced?Search=&OuDistinguishedName=&Page=&PageSize=&Enabled=&Locked=
        [HttpGet("advanced")]
        public async Task<IActionResult> GetAdvanced(
            [FromQuery] string? Search,
            [FromQuery] string? OuDistinguishedName,
            [FromQuery] int Page = 1,
            [FromQuery] int PageSize = 50,
            [FromQuery] bool? Enabled = null,
            [FromQuery] bool? Locked = null)
        {
            var options = new admgmt_backend.Services.UsersQueryOptions
            {
                Search = Search,
                OuDistinguishedName = OuDistinguishedName,
                Page = Page,
                PageSize = PageSize,
                Enabled = Enabled,
                Locked = Locked
            };

            var (items, total) = await _ad.GetUsersAdvancedAsync(options);
            return Ok(new { items, total });
        }

        // GET api/users/{samOrUpn}
        [HttpGet("{samOrUpn}")]
        public async Task<IActionResult> GetById(string samOrUpn)
        {
            var user = await _ad.GetUserAsync(samOrUpn);
            return Ok(user);
        }

        // GET api/users/details?sam=&upn=
        [HttpGet("details")]
        public async Task<IActionResult> GetDetails([FromQuery] string? sam, [FromQuery] string? upn)
        {
            var details = await _ad.GetUserDetailsAsync(sam, upn);
            return Ok(details);
        }

        // PUT api/users/{sam}/enable?enabled=true
        [HttpPut("{sam}/enable")]
        public async Task<IActionResult> SetEnabled(string sam, [FromQuery] bool enabled)
        {
            await _ad.SetUserEnabledAsync(sam, enabled);
            return Ok(new { success = true });
        }

        // POST api/users/{sam}/reset-password
        public record ResetPasswordReq(string NewPassword, bool Unlock, bool MustChange);

        [HttpPost("{sam}/reset-password")]
        public async Task<IActionResult> ResetPassword(string sam, [FromBody] ResetPasswordReq body)
        {
            if (body.Unlock || body.MustChange)
            {
                await _ad.ResetPasswordWithOptionsAsync(sam, body.NewPassword, body.Unlock, body.MustChange);
            }
            else
            {
                await _ad.ResetPasswordAsync(sam, body.NewPassword);
            }

            return Ok(new { success = true });
        }

        // POST api/users/{sam}/unlock
        [HttpPost("{sam}/unlock")]
        public async Task<IActionResult> Unlock(string sam)
        {
            await _ad.UnlockUserAsync(sam);
            return Ok(new { success = true });
        }
    }
}
