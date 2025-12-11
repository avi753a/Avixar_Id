namespace Avixar.Entity
{
    /// <summary>
    /// Request DTO for sending OTP
    /// </summary>
    public class SendOtpRequestDto
    {
        public string Purpose { get; set; } = string.Empty; // TwoFactorAuth, EmailUpdate
        public string? Email { get; set; } // For email update, send to new email
        public int? ExpirySeconds { get; set; } // Custom expiry (1-31536000)
    }
}
