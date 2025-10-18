using admgmt_backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace admgmt_backend.Controllers
{
    [ApiController]
    [Route("api/policy")]
    public class PolicyController : ControllerBase
    {
        private readonly IPasswordPolicyService _pol;
        public PolicyController(IPasswordPolicyService pol) { _pol = pol; }

        [HttpGet("password")]
        public IActionResult GetPasswordPolicy() => Ok(_pol.GetPolicy());
    }
}
