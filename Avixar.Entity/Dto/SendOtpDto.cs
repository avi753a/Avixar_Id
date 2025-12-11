namespace Avixar.Entity
{
    /// <summary>
    /// DTO for sending OTP codes
    /// Purpose can be "Verification" or "TwoFactor"
    /// </summary>
    public class SendOtpDto
    {
        public string Email { get; set; } = string.Empty;
        public string Purpose { get; set; } = "Verification"; // Verification or TwoFactor
    }
}
