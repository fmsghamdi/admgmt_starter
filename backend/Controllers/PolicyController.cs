using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace admgmt_backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PolicyController : ControllerBase
    {
        private readonly IConfiguration _cfg;

        public PolicyController(IConfiguration cfg)
        {
            _cfg = cfg;
        }

        [HttpGet]
        public IActionResult Get()
        {
            // رجّع أي سياسة متوفرة من الإعدادات (إن وُجدت)
            var section = _cfg.GetSection("PasswordPolicy");
            if (!section.Exists())
                return Ok(new { minLength = 8, complexity = "Default", history = 5 });

            return Ok(new
            {
                minLength = section.GetValue<int?>("MinLength") ?? 8,
                complexity = section.GetValue<string>("Complexity") ?? "Default",
                history = section.GetValue<int?>("History") ?? 5
            });
        }
    }
}
