namespace Avixar.Entity
{
    /// <summary>
    /// DTO for verifying OTP codes
    /// Purpose can be "Verification" or "TwoFactor"
    /// </summary>
    public class VerifyOtpDto
    {
        public string Email { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string Purpose { get; set; } = "Verification"; // Verification or TwoFactor
    }
}
