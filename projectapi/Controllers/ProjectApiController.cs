using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace projectapi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProjectApiController(IOptions<JwtSettings> jwtOptions, IOptions<AccountToInitSystem> accountOptions) : ControllerBase
    {

        private readonly JwtSettings _jwtSettings = jwtOptions.Value;
        private readonly AccountToInitSystem _accountSettings = accountOptions.Value;

        [Route("login")]
        [HttpPost]
        [AllowAnonymous]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            if (request.Username == _accountSettings.Username && request.Password == _accountSettings.Password)
            {
                var token = GenerateJwtToken();
                return Ok(new { Code = 1, Data = token });
            }

            return Ok(new { Code = 2, Data = "Invalid username or password" });
        }

        private string GenerateJwtToken()
        {
            var claims = new[]
            {
            new Claim(JwtRegisteredClaimNames.Sub, _accountSettings.Username ?? ""),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Key ?? ""));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _jwtSettings.Issuer,
                audience: _jwtSettings.Audience,
                claims: claims,
                expires: DateTime.Now.AddMinutes(_jwtSettings.ExpirationMinutes),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }

    public class LoginRequest
    {
        public string? Username { get; set; }
        public string? Password { get; set; }
    }

    public class JwtSettings
    {
        public string? Key { get; set; }
        public string? Issuer { get; set; }
        public string? Audience { get; set; }
        public int ExpirationMinutes { get; set; }
        public string? Secret { get; set; }
    }

    public class AccountToInitSystem
    {
        public string? Username { get; set; }
        public string? Password { get; set; }
    }


    public class Class
    {
        public Guid Id { get; set; }
        public string? FullName { get; set; }
    }
    public class Student
    {
        public Guid Id { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public Guid ClassId { get; set; }
        public short Gender { get; set; }
        public DateTime? DayOfBirth { get; set; }
        public string? Avatar { get; set; }
    }
}


