using admgmt_backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.DirectoryServices;

namespace admgmt_backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GroupsController : ControllerBase
    {
        private readonly IADService _ad;
        private readonly IConfiguration _cfg;
        private readonly ILogger<GroupsController> _logger;

        public GroupsController(IADService ad, IConfiguration cfg, ILogger<GroupsController> logger)
        {
            _ad = ad;
            _cfg = cfg;
            _logger = logger;
        }

        // GET /api/groups?search=&take=&skip=
        [HttpGet]
        public async Task<IActionResult> List([FromQuery] string? search = null, [FromQuery] int take = 100, [FromQuery] int skip = 0)
        {
            var items = await _ad.GetGroupsAsync(search, take, skip);
            return Ok(new { total = items.Count, items });
        }

        public record AddRemoveDto(string groupName, string samAccountName);

        // POST /api/groups/add-user
        [HttpPost("add-user")]
        public async Task<IActionResult> AddUser([FromBody] AddRemoveDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.groupName) || string.IsNullOrWhiteSpace(dto.samAccountName))
                return BadRequest();

            var ok = await _ad.AddUserToGroupAsync(dto.samAccountName, dto.groupName);
            return Ok(new { success = ok });
        }

        // POST /api/groups/remove-user
        [HttpPost("remove-user")]
        public async Task<IActionResult> RemoveUser([FromBody] AddRemoveDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.groupName) || string.IsNullOrWhiteSpace(dto.samAccountName))
                return BadRequest();

            var ok = await _ad.RemoveUserFromGroupAsync(dto.samAccountName, dto.groupName);
            return Ok(new { success = ok });
        }

        // GET /api/groups/members?name=GroupName
        // يعيد أعضاء المجموعة (Sam/DisplayName/Type) لاستخدامه في العدّ والفلاتر
        [HttpGet("members")]
        public IActionResult Members([FromQuery] string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return BadRequest(new { error = "Missing group name" });

            try
            {
                var upn = _cfg["AD:ServiceUserUPN"];
                var pass = _cfg["AD:ServicePassword"];

                // استخدم defaultNamingContext إن لم يحدد BaseDN
                string baseDn;
                using (var root = new DirectoryEntry("LDAP://RootDSE"))
                {
                    baseDn = root.Properties["defaultNamingContext"]?.Value?.ToString() ?? (_cfg["AD:DefaultContainer"] ?? "");
                }

                // ابحث عن المجموعة بالـ sAMAccountName أو CN
                using var baseDe = new DirectoryEntry("LDAP://" + baseDn, upn, pass);
                using var ds = new DirectorySearcher(baseDe)
                {
                    Filter = $"(|(sAMAccountName={Escape(name)})(cn={Escape(name)}))",
                    SearchScope = SearchScope.Subtree,
                    PageSize = 1000
                };
                ds.PropertiesToLoad.Add("member");
                ds.PropertiesToLoad.Add("cn");
                ds.PropertiesToLoad.Add("sAMAccountName");

                var found = ds.FindOne();
                if (found == null)
                    return Ok(Array.Empty<object>());

                var members = new List<object>();
                if (found.Properties.Contains("member"))
                {
                    foreach (var v in found.Properties["member"])
                    {
                        var dn = v?.ToString();
                        if (string.IsNullOrWhiteSpace(dn)) continue;

                        try
                        {
                            using var entry = new DirectoryEntry("LDAP://" + dn, upn, pass);
                            var cls = entry.Properties["objectClass"]?.Value;
                            var oc = cls is System.Array arr && arr.Length > 0 ? arr.GetValue(arr.Length - 1)?.ToString() : entry.SchemaClassName;

                            string type = oc?.ToLower() switch
                            {
                                "user" => "user",
                                "group" => "group",
                                "computer" => "computer",
                                _ => "unknown"
                            };

                            string sam = entry.Properties["sAMAccountName"]?.Value?.ToString() ?? "";
                            string disp = entry.Properties["displayName"]?.Value?.ToString()
                                          ?? entry.Properties["cn"]?.Value?.ToString()
                                          ?? sam;

                            members.Add(new { samAccountName = sam, displayName = disp, type });
                        }
                        catch (Exception exItem)
                        {
                            _logger.LogWarning(exItem, "Failed to read member {dn}", dn);
                        }
                    }
                }

                return Ok(members);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load group members for {name}", name);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        private static string Escape(string v)
            => v.Replace("\\", "\\5c").Replace("(", "\\28").Replace(")", "\\29").Replace("*", "\\2a");
    }
}
