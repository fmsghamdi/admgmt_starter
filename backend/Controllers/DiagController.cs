using Microsoft.AspNetCore.Mvc;
using admgmt_backend.Services;
using Microsoft.Extensions.Options;

namespace admgmt_backend.Controllers
{
    [ApiController]
    [Route("api/diag")]
    public class DiagController : ControllerBase
    {
        private readonly IServiceProvider _sp;
        private readonly AdOptions _opt;

        public DiagController(IServiceProvider sp, IOptions<AdOptions> opt)
        {
            _sp = sp;
            _opt = opt.Value;
        }

        [HttpGet("ping")]
        public IActionResult Ping()
            => Ok(new { ok = true, env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") });

        [HttpGet("config")]
        public IActionResult Config()
            => Ok(new {
                _opt.BaseDN,
                _opt.DomainController,
                _opt.ServiceUserUPN,
                _opt.SearchTimeoutSeconds,
                _opt.UseLdaps
            });

        [HttpGet("roots")]
        public async Task<IActionResult> Roots([FromServices] IADService? ad = null)
        {
            // نطلب الخدمة هنا فقط (حقن متأخر)
            ad ??= _sp.GetService<IADService>();
            if (ad == null) return Problem("IADService not available from DI");

            try
            {
                var items = await ad.GetOUsAsync(null, 1, 5);
                return Ok(items);
            }
            catch (Exception ex)
            {
                return Problem(ex.ToString());
            }
        }
    }
}
