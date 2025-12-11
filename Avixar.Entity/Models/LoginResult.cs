namespace Avixar.Entity
{
    public class LoginResult
    {
        public Guid UserId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Token { get; set; }
        public string? ProfilePictureUrl { get; set; }
    }
}
