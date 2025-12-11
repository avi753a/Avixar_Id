using Avixar.Entity;

namespace Avixar.Data
{
    /// <summary>
    /// Repository interface for OAuth2 data access (authorization codes and refresh tokens)
    /// Uses Redis for temporary storage
    /// </summary>
    public interface IOAuthRepository
    {
        // Authorization Codes
        Task SaveAuthorizationCodeAsync(AuthorizationCode code);
        Task<AuthorizationCode?> GetAuthorizationCodeAsync(string code);
        Task DeleteAuthorizationCodeAsync(string code);
        
        // Refresh Tokens
        Task SaveRefreshTokenAsync(RefreshToken token);
        Task<RefreshToken?> GetRefreshTokenAsync(string token);
        Task RevokeRefreshTokenAsync(string token);
    }
}
