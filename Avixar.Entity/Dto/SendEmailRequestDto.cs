namespace Avixar.Entity
{
    /// <summary>
    /// Request DTO for sending email
    /// </summary>
    public class SendEmailRequestDto
    {
        public string To { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
    }
}
