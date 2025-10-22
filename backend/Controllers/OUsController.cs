using Microsoft.AspNetCore.Mvc;
using admgmt_backend.Services;

namespace admgmt_backend.Controllers
{
    [ApiController]
    [Route("api/ous")]
    public class OUsController : ControllerBase
    {
        private readonly IADService _ad;

        public OUsController(IADService ad)
        {
            _ad = ad;
        }

        // GET api/ous/root
        [HttpGet("root")]
        public async Task<IActionResult> GetRoot([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            var items = await _ad.GetOUsAsync(null, page, pageSize);
            return Ok(items);
        }

        // GET api/ous/children?parentDn=...
        [HttpGet("children")]
        public async Task<IActionResult> GetChildren([FromQuery] string? parentDn)
        {
            var items = await _ad.GetChildOUsAsync(parentDn);
            return Ok(items);
        }

        // GET api/ous/objects?ouDn=...&page=1&pageSize=50
        [HttpGet("objects")]
        public async Task<IActionResult> GetObjects([FromQuery] string ouDn, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            var (items, total) = await _ad.GetOuObjectsAsync(ouDn, page, pageSize);
            return Ok(new { items, total });
        }

        // POST api/ous   body: { parentDn, name }
        public record CreateOuReq(string ParentDn, string Name);

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateOuReq body)
        {
            await _ad.CreateOUAsync(body.ParentDn, body.Name);
            return Ok(new { success = true });
        }

        // DELETE api/ous?dn=...
        [HttpDelete]
        public async Task<IActionResult> Delete([FromQuery] string dn)
        {
            await _ad.DeleteOUAsync(dn);
            return Ok(new { success = true });
        }

        // PUT api/ous/rename   body: { dn, newName }
        public record RenameOuReq(string Dn, string NewName);

        [HttpPut("rename")]
        public async Task<IActionResult> Rename([FromBody] RenameOuReq body)
        {
            await _ad.RenameOUAsync(body.Dn, body.NewName);
            return Ok(new { success = true });
        }

        // PUT api/ous/move-object   body: { objectDn, targetOuDn }
        public record MoveObjectReq(string ObjectDn, string TargetOuDn);

        [HttpPut("move-object")]
        public async Task<IActionResult> MoveObject([FromBody] MoveObjectReq body)
        {
            await _ad.MoveObjectAsync(body.ObjectDn, body.TargetOuDn);
            return Ok(new { success = true });
        }

        // PUT api/ous/move-user-by-sam   body: { sam, targetOuDn }
        public record MoveUserBySamReq(string Sam, string TargetOuDn);

        [HttpPut("move-user-by-sam")]
        public async Task<IActionResult> MoveUserBySam([FromBody] MoveUserBySamReq body)
        {
            await _ad.MoveUserBySamAsync(body.Sam, body.TargetOuDn);
            return Ok(new { success = true });
        }
    }
}
