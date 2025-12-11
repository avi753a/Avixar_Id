using Avixar.Domain;
using Avixar.Entity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Avixar.UI
{
    /// <summary>
    /// Organization Management Controller
    /// Handles organization CRUD and context switching
    /// </summary>
    [ApiController]
    [Route("api/orgs")]
    [Authorize]
    public class OrgController : ControllerBase
    {
        private readonly IOrganizationService _orgService;
        private readonly ILogger<OrgController> _logger;

        public OrgController(IOrganizationService orgService, ILogger<OrgController> logger)
        {
            _orgService = orgService;
            _logger = logger;
        }

        /// <summary>
        /// GET /api/orgs - List user's organizations
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetOrganizations()
        {
            try
            {
                var userId = GetUserId();
                if (userId == Guid.Empty)
                    return Unauthorized(new { message = "User not authenticated" });

                _logger.LogInformation("Getting organizations for user {UserId}", userId);

                var result = await _orgService.GetUserOrganizationsAsync(userId);

                if (!result.Status)
                    return BadRequest(new { message = result.Message });

                return Ok(result.Data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting organizations");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// GET /api/orgs/contexts - Get available contexts for context switcher
        /// </summary>
        [HttpGet("contexts")]
        public async Task<IActionResult> GetContexts()
        {
            try
            {
                var userId = GetUserId();
                if (userId == Guid.Empty)
                    return Unauthorized(new { message = "User not authenticated" });

                _logger.LogInformation("Getting contexts for user {UserId}", userId);

                var result = await _orgService.GetOrganizationContextsAsync(userId);

                if (!result.Status)
                    return BadRequest(new { message = result.Message });

                return Ok(result.Data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting contexts");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// POST /api/orgs - Create new organization (creator becomes OWNER)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateOrganization([FromBody] CreateOrgDto dto)
        {
            try
            {
                var userId = GetUserId();
                if (userId == Guid.Empty)
                    return Unauthorized(new { message = "User not authenticated" });

                _logger.LogInformation("Creating organization {Name} for user {UserId}", dto.Name, userId);

                var result = await _orgService.CreateOrganizationAsync(userId, dto);

                if (!result.Status)
                    return BadRequest(new { message = result.Message });

                return Ok(new { orgId = result.Data, message = result.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating organization");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// POST /api/orgs/switch-context - Switch to a specific org/role context (issues new JWT)
        /// </summary>
        [HttpPost("switch-context")]
        public async Task<IActionResult> SwitchContext([FromBody] SwitchContextDto dto)
        {
            try
            {
                var userId = GetUserId();
                if (userId == Guid.Empty)
                    return Unauthorized(new { message = "User not authenticated" });

                _logger.LogInformation("Switching context for user {UserId} to org {OrgId}", userId, dto.OrgId);

                var result = await _orgService.SwitchContextAsync(userId, dto.OrgId);

                if (!result.Status)
                    return BadRequest(new { message = result.Message });

                return Ok(new { access_token = result.Data, message = result.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error switching context");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        private Guid GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
        }
    }
}
