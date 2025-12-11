using Avixar.Entity;
using Avixar.Infrastructure;

using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Avixar.Data
{
    /// <summary>
    /// Repository implementation for OAuth2 data access
    /// Uses Redis via RedisCacheService for temporary storage
    /// </summary>
    public class OAuthRepository : IOAuthRepository
    {
        private readonly RedisCacheService _cacheService;
        private readonly ILogger<OAuthRepository> _logger;

        private const string AUTH_CODE_PREFIX = "authcode:";
        private const string REFRESH_TOKEN_PREFIX = "refresh:";
        private static readonly TimeSpan AUTH_CODE_EXPIRY = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan REFRESH_TOKEN_EXPIRY = TimeSpan.FromDays(30);

        public OAuthRepository(RedisCacheService cacheService, ILogger<OAuthRepository> logger)
        {
            _cacheService = cacheService;
            _logger = logger;
        }

        // Authorization Codes
        public async Task SaveAuthorizationCodeAsync(AuthorizationCode code)
        {
            try
            {
                var key = AUTH_CODE_PREFIX + code.Code;
                await _cacheService.SetAsync(key, code, AUTH_CODE_EXPIRY);
                _logger.LogInformation("Saved authorization code for user {UserId}", code.UserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving authorization code");
                throw;
            }
        }

        public async Task<AuthorizationCode?> GetAuthorizationCodeAsync(string code)
        {
            try
            {
                var key = AUTH_CODE_PREFIX + code;
                return await _cacheService.GetAsync<AuthorizationCode>(key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting authorization code");
                throw;
            }
        }

        public async Task DeleteAuthorizationCodeAsync(string code)
        {
            try
            {
                var key = AUTH_CODE_PREFIX + code;
                await _cacheService.DeleteAsync(key);
                _logger.LogInformation("Deleted authorization code");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting authorization code");
                throw;
            }
        }

        // Refresh Tokens
        public async Task SaveRefreshTokenAsync(RefreshToken token)
        {
            try
            {
                var key = REFRESH_TOKEN_PREFIX + token.Token;
                await _cacheService.SetAsync(key, token, REFRESH_TOKEN_EXPIRY);
                _logger.LogInformation("Saved refresh token for user {UserId}", token.UserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving refresh token");
                throw;
            }
        }

        public async Task<RefreshToken?> GetRefreshTokenAsync(string token)
        {
            try
            {
                var key = REFRESH_TOKEN_PREFIX + token;
                return await _cacheService.GetAsync<RefreshToken>(key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting refresh token");
                throw;
            }
        }

        public async Task RevokeRefreshTokenAsync(string token)
        {
            try
            {
                var key = REFRESH_TOKEN_PREFIX + token;
                var refreshToken = await _cacheService.GetAsync<RefreshToken>(key);
                
                if (refreshToken != null)
                {
                    refreshToken.IsRevoked = true;
                    await _cacheService.SetAsync(key, refreshToken, REFRESH_TOKEN_EXPIRY);
                    _logger.LogInformation("Revoked refresh token for user {UserId}", refreshToken.UserId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking refresh token");
                throw;
            }
        }
    }
}
