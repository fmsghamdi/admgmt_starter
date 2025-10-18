using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using admgmt_backend.Services;

namespace admgmt_backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IConfiguration _cfg;
        private readonly IAuthService _auth;

        public AuthController(IConfiguration cfg, IAuthService auth)
        {
            _cfg = cfg;
            _auth = auth;
        }

        public record LoginDto(string Username, string Password);

        /// <summary>
        /// توثيق فعلي ضد AD: إذا نجح ValidateCredentials نصدر JWT يحتوي Claims من AD.
        /// </summary>
        [HttpPost("token")]
        public async Task<IActionResult> Token([FromBody] LoginDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Password))
                return BadRequest(new { error = "Username and password are required." });

            var identity = await _auth.ValidateUserAsync(dto.Username, dto.Password);
            if (identity == null)
                return Unauthorized(new { error = "Invalid credentials." });

            var key = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(_cfg["Auth:JwtKey"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            // صلاحية التوكين
            var expiresHours = 8;
            if (int.TryParse(_cfg["Auth:TokenHours"], out var h) && h > 0 && h <= 24) expiresHours = h;

            var token = new JwtSecurityToken(
                claims: identity.Claims,
                expires: DateTime.UtcNow.AddHours(expiresHours),
                signingCredentials: creds
            );

            var jwt = new JwtSecurityTokenHandler().WriteToken(token);
            return Ok(new { token = jwt });
        }
    }
}
