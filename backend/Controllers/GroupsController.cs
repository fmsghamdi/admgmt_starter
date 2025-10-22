using Microsoft.AspNetCore.Mvc;
using admgmt_backend.Services;

namespace admgmt_backend.Controllers
{
    [ApiController]
    [Route("api/groups")]
    public class GroupsController : ControllerBase
    {
        private readonly IADService _ad;

        public GroupsController(IADService ad)
        {
            _ad = ad;
        }

        // GET api/groups?search=&page=1&pageSize=50
        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            var (items, total) = await _ad.GetGroupsAsync(search, page, pageSize);
            return Ok(new { items, total });
        }

        // GET api/groups/{groupSamOrDn}/members
        [HttpGet("{groupSamOrDn}/members")]
        public async Task<IActionResult> GetMembers(string groupSamOrDn)
        {
            var members = await _ad.GetGroupMembersAsync(groupSamOrDn);
            return Ok(members);
        }

        // POST api/groups/{groupSamOrDn}/add-user?userSam=...
        [HttpPost("{groupSamOrDn}/add-user")]
        public async Task<IActionResult> AddUser(string groupSamOrDn, [FromQuery] string userSam)
        {
            await _ad.AddUserToGroupAsync(userSam, groupSamOrDn);
            return Ok(new { success = true });
        }

        // DELETE api/groups/{groupSamOrDn}/remove-user?userSam=...
        [HttpDelete("{groupSamOrDn}/remove-user")]
        public async Task<IActionResult> RemoveUser(string groupSamOrDn, [FromQuery] string userSam)
        {
            await _ad.RemoveUserFromGroupAsync(userSam, groupSamOrDn);
            return Ok(new { success = true });
        }
    }
}
