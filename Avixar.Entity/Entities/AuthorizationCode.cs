namespace Avixar.Entity
{
    /// <summary>
    /// Represents an OAuth2 authorization code
    /// Stored in Redis with 10-minute expiration
    /// </summary>
    public class AuthorizationCode
    {
        public string Code { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public Guid UserId { get; set; }
        public string RedirectUri { get; set; } = string.Empty;
        public string Scope { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool IsUsed { get; set; }
    }
}
