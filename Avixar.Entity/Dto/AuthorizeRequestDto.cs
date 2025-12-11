namespace Avixar.Entity
{
    /// <summary>
    /// DTO for OAuth2 authorization requests
    /// Used in the /connect/authorize endpoint
    /// </summary>
    public class AuthorizeRequestDto
    {
        public string ClientId { get; set; } = string.Empty;
        public string RedirectUri { get; set; } = string.Empty;
        public string ResponseType { get; set; } = "code";
        public string Scope { get; set; } = "openid profile";
        public string? State { get; set; }
    }
}
