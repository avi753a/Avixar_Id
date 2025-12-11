namespace Avixar.Entity
{
    /// <summary>
    /// Request DTO for validating OTP
    /// </summary>
    public class ValidateOtpRequestDto
    {
        public string Code { get; set; } = string.Empty;
        public string Purpose { get; set; } = string.Empty;
    }
}
