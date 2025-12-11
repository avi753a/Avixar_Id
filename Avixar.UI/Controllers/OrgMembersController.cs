using Avixar.Domain;
using Avixar.Entity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Avixar.UI
{
    /// <summary>
    /// Organization Members Management Controller
    /// Handles member invitations, role updates, and removals
    /// </summary>
    [ApiController]
    [Route("api/orgs/{orgId}/members")]
    [Authorize]
    public class OrgMembersController : ControllerBase
    {
        private readonly IOrganizationMemberService _memberService;
        private readonly ILogger<OrgMembersController> _logger;

        public OrgMembersController(IOrganizationMemberService memberService, ILogger<OrgMembersController> logger)
        {
            _memberService = memberService;
            _logger = logger;
        }

        /// <summary>
        /// GET /api/orgs/{orgId}/members - List organization members
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetMembers(Guid orgId)
        {
            try
            {
                var userId = GetUserId();
                if (userId == Guid.Empty)
                    return Unauthorized(new { message = "User not authenticated" });

                _logger.LogInformation("Getting members for org {OrgId} by user {UserId}", orgId, userId);

                var result = await _memberService.GetOrgMembersAsync(orgId, userId);

                if (!result.Status)
                    return BadRequest(new { message = result.Message });

                return Ok(result.Data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting members for org {OrgId}", orgId);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// POST /api/orgs/{orgId}/members/invite - Invite member to organization
        /// Requires OWNER or ADMIN role
        /// </summary>
        [HttpPost("invite")]
        public async Task<IActionResult> InviteMember(Guid orgId, [FromBody] InviteMemberDto dto)
        {
            try
            {
                var userId = GetUserId();
                if (userId == Guid.Empty)
                    return Unauthorized(new { message = "User not authenticated" });

                _logger.LogInformation("Inviting member {Email} to org {OrgId} by user {UserId}", 
                    dto.Email, orgId, userId);

                var result = await _memberService.InviteMemberAsync(orgId, userId, dto);

                if (!result.Status)
                    return BadRequest(new { message = result.Message });

                return Ok(new { success = result.Data, message = result.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inviting member to org {OrgId}", orgId);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// PUT /api/orgs/{orgId}/members/{userId}/roles - Update member role
        /// Requires OWNER or ADMIN role
        /// </summary>
        [HttpPut("{userId}/roles")]
        public async Task<IActionResult> UpdateMemberRole(Guid orgId, Guid userId, [FromBody] UpdateMemberRoleDto dto)
        {
            try
            {
                var requestingUserId = GetUserId();
                if (requestingUserId == Guid.Empty)
                    return Unauthorized(new { message = "User not authenticated" });

                _logger.LogInformation("Updating role for member {TargetUserId} in org {OrgId} by user {RequestingUserId}", 
                    userId, orgId, requestingUserId);

                var result = await _memberService.UpdateMemberRoleAsync(orgId, requestingUserId, userId, dto.RoleId);

                if (!result.Status)
                    return BadRequest(new { message = result.Message });

                return Ok(new { success = result.Data, message = result.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating role for member {UserId} in org {OrgId}", userId, orgId);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// DELETE /api/orgs/{orgId}/members/{userId} - Remove member from organization
        /// Requires OWNER or ADMIN role
        /// Cannot remove OWNER
        /// </summary>
        [HttpDelete("{userId}")]
        public async Task<IActionResult> RemoveMember(Guid orgId, Guid userId)
        {
            try
            {
                var requestingUserId = GetUserId();
                if (requestingUserId == Guid.Empty)
                    return Unauthorized(new { message = "User not authenticated" });

                _logger.LogInformation("Removing member {TargetUserId} from org {OrgId} by user {RequestingUserId}", 
                    userId, orgId, requestingUserId);

                var result = await _memberService.RemoveMemberAsync(orgId, requestingUserId, userId);

                if (!result.Status)
                    return BadRequest(new { message = result.Message });

                return Ok(new { success = result.Data, message = result.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing member {UserId} from org {OrgId}", userId, orgId);
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
