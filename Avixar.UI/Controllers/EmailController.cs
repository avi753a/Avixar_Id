using Avixar.Infrastructure;
using Avixar.Entity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Avixar.UI
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class EmailController : ControllerBase
    {
        private readonly EmailService _emailService;
        private readonly ILogger<EmailController> _logger;

        public EmailController(EmailService emailService, ILogger<EmailController> logger)
        {
            _emailService = emailService;
            _logger = logger;
        }

        /// <summary>
        /// Send a simple email with subject and body
        /// </summary>
        [HttpPost("send")]
        public async Task<IActionResult> SendEmail([FromBody] SendEmailRequestDto request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.To) || string.IsNullOrEmpty(request.Subject) || string.IsNullOrEmpty(request.Body))
                {
                    return BadRequest(new { message = "To, Subject, and Body are required" });
                }

                _logger.LogInformation("Email send request from {User} to {To}", User.Identity?.Name, request.To);

                await _emailService.SendEmailAsync(request.To, request.Subject, request.Body);
                return Ok(new { message = "Email sent successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending email");
                return StatusCode(500, new { message = "An error occurred while sending email" });
            }
        }
    }
}
