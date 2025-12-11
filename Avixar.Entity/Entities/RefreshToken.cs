namespace Avixar.Entity
{
    /// <summary>
    /// Represents an OAuth2 refresh token
    /// Stored in Redis with 30-day expiration
    /// </summary>
    public class RefreshToken
    {
        public string Token { get; set; } = string.Empty;
        public Guid UserId { get; set; }
        public string ClientId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool IsRevoked { get; set; }
    }
}
