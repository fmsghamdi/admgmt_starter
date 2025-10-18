using admgmt_backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
// using Microsoft.AspNetCore.Authorization;

namespace admgmt_backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    // [Authorize]
    public class OUsController : ControllerBase
    {
        private readonly IADService _ad;
        private readonly ILogger<OUsController> _logger;

        public OUsController(IADService ad, ILogger<OUsController> logger)
        {
            _ad = ad;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] string? baseDn, [FromQuery] int take = 500, [FromQuery] int skip = 0)
            => Ok(await _ad.GetOUsAsync(baseDn, take, skip));

        [HttpGet("children")]
        public async Task<IActionResult> GetChildren([FromQuery] string? parentDn = null)
        {
            try
            {
                _logger.LogInformation("Loading OUs for parent: {Parent}", parentDn ?? "(root)");
                var ous = await _ad.GetChildOUsAsync(parentDn);
                _logger.LogInformation("Loaded {Count} OUs.", ous.Count);
                return Ok(ous);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading OUs: {Message}", ex.Message);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("objects")]
        public async Task<IActionResult> GetObjects([FromQuery] string ouDn, [FromQuery] int take = 200, [FromQuery] int skip = 0, [FromQuery] string? search = null)
        {
            try
            {
                var list = await _ad.GetOuObjectsAsync(ouDn, take, skip, search);
                return Ok(list);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading OU objects: {Message}", ex.Message);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("object")]
        public async Task<IActionResult> GetObject([FromQuery] string dn)
        {
            try
            {
                var obj = await _ad.GetObjectDetailsAsync(dn);
                return obj == null ? NotFound() : Ok(obj);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading object details: {Message}", ex.Message);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        public record CreateDto(string ParentDn, string Name, string? Description);
        [HttpPost] public async Task<IActionResult> Create([FromBody] CreateDto dto)
            => Ok(new { success = await _ad.CreateOUAsync(dto.ParentDn, dto.Name, dto.Description) });

        public record RenameDto(string Dn, string NewName);
        [HttpPut("rename")] public async Task<IActionResult> Rename([FromBody] RenameDto dto)
            => Ok(new { success = await _ad.RenameOUAsync(dto.Dn, dto.NewName) });

        [HttpDelete] public async Task<IActionResult> Delete([FromQuery] string dn)
            => Ok(new { success = await _ad.DeleteOUAsync(dn) });

        public record MoveDto(string ObjectDn, string TargetOuDn);
        [HttpPost("move-object")] public async Task<IActionResult> MoveObject([FromBody] MoveDto dto)
            => Ok(new { success = await _ad.MoveObjectAsync(dto.ObjectDn, dto.TargetOuDn) });

        public record MoveUserSamDto(string SamAccountName, string TargetOuDn);
        [HttpPost("move-user-by-sam")] public async Task<IActionResult> MoveUserBySam([FromBody] MoveUserSamDto dto)
            => Ok(new { success = await _ad.MoveUserBySamAsync(dto.SamAccountName, dto.TargetOuDn) });
    }
}
