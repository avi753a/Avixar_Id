namespace Avixar.Entity
{
    /// <summary>
    /// DTO for OAuth2 token requests
    /// Supports authorization_code and refresh_token grant types
    /// </summary>
    public class TokenRequestDto
    {
        public string GrantType { get; set; } = string.Empty; // authorization_code or refresh_token
        public string? Code { get; set; } // For authorization_code grant
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public string? RedirectUri { get; set; } // For authorization_code grant
        public string? RefreshToken { get; set; } // For refresh_token grant
    }
}
