using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

namespace projectapi.Middleware
{
    public class JwtValidationMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        private readonly RequestDelegate _next = next;
        private readonly string _secretKey = configuration["JwtSettings:Secret"] ?? "";

        public async Task InvokeAsync(HttpContext context)
        {
            // Bỏ qua API login
            if (context.Request.Path.StartsWithSegments("/api/login") || context.Request.Path.StartsWithSegments("/swagger/index.html") )
            {
                await _next(context); // Bỏ qua middleware này
                return;
            }

            var token = context.Request.Headers.Authorization.ToString()?.Replace("Bearer ", "").Trim();

            if (string.IsNullOrEmpty(token))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Token is missing or invalid");
                return;
            }

            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.UTF8.GetBytes(_secretKey);

                // Kiểm tra tính hợp lệ của JWT token
                var parameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ClockSkew = TimeSpan.Zero // Không cho phép chênh lệch thời gian giữa client và server
                };

                // Validate token và giải mã thông tin
                var principal = tokenHandler.ValidateToken(token, parameters, out var validatedToken);

                // Kiểm tra nếu token đã hết hạn
                if (validatedToken is JwtSecurityToken jwtToken)
                {
                    var exp = jwtToken.Claims.FirstOrDefault(c => c.Type == "exp")?.Value;
                    if (exp != null && DateTimeOffset.FromUnixTimeSeconds(long.Parse(exp)) < DateTimeOffset.UtcNow)
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        await context.Response.WriteAsync("Token has expired");
                        return;
                    }
                }

                // Nếu token hợp lệ, tiếp tục xử lý request
                context.User = principal;
                await _next(context);
            }
            catch (Exception)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Invalid token");
            }
        }
    }
}
