using Avixar.Entity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Avixar.Domain
{
    public class TokenService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<TokenService> _logger;

        public TokenService(IConfiguration configuration, ILogger<TokenService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public string GenerateJwtToken(string userId, string email, string displayName)
        {
            try
            {
                _logger.LogInformation("Generating JWT token for user: {UserId}", userId);
                
                var jwtSecret = _configuration["Jwt:Secret"] ?? "YourSuperSecretKeyForJWTTokenGeneration123456789";
                var jwtIssuer = _configuration["Jwt:Issuer"] ?? "AvixarIdentityProvider";
                var jwtAudience = _configuration["Jwt:Audience"] ?? "AvixarClients";
                var expiryMinutes = int.Parse(_configuration["Jwt:ExpiryMinutes"] ?? "60");

                var claims = new List<Claim>
                {
                    new Claim(JwtRegisteredClaimNames.Sub, userId),
                    new Claim(JwtRegisteredClaimNames.Email, email),
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                    new Claim("displayName", displayName),
                    new Claim(ClaimTypes.NameIdentifier, userId)
                };

                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
                var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

                var token = new JwtSecurityToken(
                    issuer: jwtIssuer,
                    audience: jwtAudience,
                    claims: claims,
                    expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
                    signingCredentials: creds
                );

                var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
                _logger.LogInformation("Successfully generated JWT token for user: {UserId}", userId);
                return tokenString;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating JWT token for user: {UserId}", userId);
                throw;
            }
        }

        public string GenerateJwtWithContext(Guid userId, string email, string displayName, Guid? orgId, int? roleId, string? roleName)
        {
            try
            {
                _logger.LogInformation("Generating JWT token with context for user: {UserId}, Org: {OrgId}, Role: {RoleId}", 
                    userId, orgId, roleId);
                
                var jwtSecret = _configuration["Jwt:Secret"] ?? "YourSuperSecretKeyForJWTTokenGeneration123456789";
                var jwtIssuer = _configuration["Jwt:Issuer"] ?? "AvixarIdentityProvider";
                var jwtAudience = _configuration["Jwt:Audience"] ?? "AvixarClients";
                var expiryMinutes = int.Parse(_configuration["Jwt:ExpiryMinutes"] ?? "60");

                var claims = new List<Claim>
                {
                    new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                    new Claim(JwtRegisteredClaimNames.Email, email),
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                    new Claim("displayName", displayName),
                    new Claim(ClaimTypes.NameIdentifier, userId.ToString())
                };

                // Add organization and role claims if provided
                if (orgId.HasValue)
                {
                    claims.Add(new Claim("org_id", orgId.Value.ToString()));
                }
                if (roleId.HasValue)
                {
                    claims.Add(new Claim("role_id", roleId.Value.ToString()));
                }
                if (!string.IsNullOrEmpty(roleName))
                {
                    claims.Add(new Claim("role_name", roleName));
                }

                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
                var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

                var token = new JwtSecurityToken(
                    issuer: jwtIssuer,
                    audience: jwtAudience,
                    claims: claims,
                    expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
                    signingCredentials: creds
                );

                var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
                _logger.LogInformation("Successfully generated JWT token with context for user: {UserId}", userId);
                return tokenString;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating JWT token with context for user: {UserId}", userId);
                throw;
            }
        }
    }
}
