using Avixar.Data;
using Avixar.Entity;
using Avixar.Entity;
using Avixar.Entity;
using Avixar.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Avixar.Domain
{
    /// <summary>
    /// Service implementation for organization member management
    /// Includes permission checks and cache invalidation
    /// </summary>
    public class OrganizationMemberService : IOrganizationMemberService
    {
        private readonly IOrganizationMemberRepository _memberRepo;
        private readonly IUserRepository _userRepo;
        private readonly RedisCacheService _cacheService;
        private readonly EmailService _emailService;
        private readonly ILogger<OrganizationMemberService> _logger;

        // Role IDs: OWNER=1, ADMIN=2, MANAGER=3, USER=4
        private const int ROLE_OWNER = 1;
        private const int ROLE_ADMIN = 2;

        public OrganizationMemberService(
            IOrganizationMemberRepository memberRepo,
            IUserRepository userRepo,
            RedisCacheService cacheService,
            EmailService emailService,
            ILogger<OrganizationMemberService> logger)
        {
            _memberRepo = memberRepo;
            _userRepo = userRepo;
            _cacheService = cacheService;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task<BaseReturn<List<OrganizationMember>>> GetOrgMembersAsync(Guid orgId, Guid requestingUserId)
        {
            try
            {
                _logger.LogInformation("Getting members for org {OrgId} by user {UserId}", orgId, requestingUserId);

                // Check if user is a member of the organization
                var roleId = await _memberRepo.GetMemberRoleAsync(requestingUserId, orgId);
                if (roleId == null)
                {
                    return new BaseReturn<List<OrganizationMember>>
                    {
                        Status = false,
                        Message = "You are not a member of this organization"
                    };
                }

                // Get members (with caching)
                var cacheKey = $"org:members:{orgId}";
                var members = await _cacheService.GetAsync<List<OrganizationMember>>(cacheKey);

                if (members == null)
                {
                    members = await _memberRepo.GetMembersByOrgIdAsync(orgId);
                    await _cacheService.SetAsync(cacheKey, members, TimeSpan.FromMinutes(15));
                }

                return new BaseReturn<List<OrganizationMember>>
                {
                    Status = true,
                    Data = members,
                    Message = $"Found {members.Count} members"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting members for org {OrgId}", orgId);
                return new BaseReturn<List<OrganizationMember>>
                {
                    Status = false,
                    Message = "Failed to retrieve members"
                };
            }
        }

        public async Task<BaseReturn<bool>> InviteMemberAsync(Guid orgId, Guid requestingUserId, InviteMemberDto dto)
        {
            try
            {
                _logger.LogInformation("Inviting member {Email} to org {OrgId} by user {UserId}", 
                    dto.Email, orgId, requestingUserId);

                // Check if requesting user has permission (OWNER or ADMIN)
                var requestingRole = await _memberRepo.GetMemberRoleAsync(requestingUserId, orgId);
                if (requestingRole == null || (requestingRole != ROLE_OWNER && requestingRole != ROLE_ADMIN))
                {
                    return new BaseReturn<bool>
                    {
                        Status = false,
                        Message = "You don't have permission to invite members"
                    };
                }

                // Find user by email
                var user = await _userRepo.GetUserByEmailAsync(dto.Email);
                if (user == null)
                {
                    return new BaseReturn<bool>
                    {
                        Status = false,
                        Message = "User not found with this email"
                    };
                }

                var userId = Guid.Parse(user.Id);

                // Check if user is already a member
                var existingRole = await _memberRepo.GetMemberRoleAsync(userId, orgId);
                if (existingRole != null)
                {
                    return new BaseReturn<bool>
                    {
                        Status = false,
                        Message = "User is already a member of this organization"
                    };
                }

                // Add member
                var success = await _memberRepo.AddMemberToOrgAsync(userId, orgId, dto.RoleId);

                if (success)
                {
                    // Invalidate cache
                    await InvalidateMemberCacheAsync(orgId);

                    // Send invitation email (fire and forget)
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _emailService.SendEmailAsync(
                                dto.Email,
                                "Organization Invitation",
                                $"You have been invited to join an organization."
                            );
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to send invitation email");
                        }
                    });

                    _logger.LogInformation("Member {UserId} invited to org {OrgId}", userId, orgId);
                }

                return new BaseReturn<bool>
                {
                    Status = success,
                    Data = success,
                    Message = success ? "Member invited successfully" : "Failed to invite member"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inviting member to org {OrgId}", orgId);
                return new BaseReturn<bool>
                {
                    Status = false,
                    Message = "Failed to invite member"
                };
            }
        }

        public async Task<BaseReturn<bool>> UpdateMemberRoleAsync(Guid orgId, Guid requestingUserId, Guid targetUserId, int newRoleId)
        {
            try
            {
                _logger.LogInformation("Updating role for member {TargetUserId} in org {OrgId} by user {RequestingUserId}", 
                    targetUserId, orgId, requestingUserId);

                // Check if requesting user has permission (OWNER or ADMIN)
                var requestingRole = await _memberRepo.GetMemberRoleAsync(requestingUserId, orgId);
                if (requestingRole == null || (requestingRole != ROLE_OWNER && requestingRole != ROLE_ADMIN))
                {
                    return new BaseReturn<bool>
                    {
                        Status = false,
                        Message = "You don't have permission to update member roles"
                    };
                }

                // Prevent changing OWNER role (only OWNER can do that)
                var targetRole = await _memberRepo.GetMemberRoleAsync(targetUserId, orgId);
                if (targetRole == ROLE_OWNER && requestingRole != ROLE_OWNER)
                {
                    return new BaseReturn<bool>
                    {
                        Status = false,
                        Message = "Only OWNER can modify OWNER roles"
                    };
                }

                // Update role
                var success = await _memberRepo.UpdateMemberRoleAsync(targetUserId, orgId, newRoleId);

                if (success)
                {
                    // Invalidate cache
                    await InvalidateMemberCacheAsync(orgId);
                    _logger.LogInformation("Role updated for member {TargetUserId} in org {OrgId}", targetUserId, orgId);
                }

                return new BaseReturn<bool>
                {
                    Status = success,
                    Data = success,
                    Message = success ? "Role updated successfully" : "Failed to update role"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating role for member {TargetUserId} in org {OrgId}", targetUserId, orgId);
                return new BaseReturn<bool>
                {
                    Status = false,
                    Message = "Failed to update role"
                };
            }
        }

        public async Task<BaseReturn<bool>> RemoveMemberAsync(Guid orgId, Guid requestingUserId, Guid targetUserId)
        {
            try
            {
                _logger.LogInformation("Removing member {TargetUserId} from org {OrgId} by user {RequestingUserId}", 
                    targetUserId, orgId, requestingUserId);

                // Check if requesting user has permission (OWNER or ADMIN)
                var requestingRole = await _memberRepo.GetMemberRoleAsync(requestingUserId, orgId);
                if (requestingRole == null || (requestingRole != ROLE_OWNER && requestingRole != ROLE_ADMIN))
                {
                    return new BaseReturn<bool>
                    {
                        Status = false,
                        Message = "You don't have permission to remove members"
                    };
                }

                // Prevent removing OWNER
                var targetRole = await _memberRepo.GetMemberRoleAsync(targetUserId, orgId);
                if (targetRole == ROLE_OWNER)
                {
                    return new BaseReturn<bool>
                    {
                        Status = false,
                        Message = "Cannot remove OWNER from organization"
                    };
                }

                // Remove member
                var success = await _memberRepo.RemoveMemberFromOrgAsync(targetUserId, orgId);

                if (success)
                {
                    // Invalidate cache
                    await InvalidateMemberCacheAsync(orgId);
                    _logger.LogInformation("Member {TargetUserId} removed from org {OrgId}", targetUserId, orgId);
                }

                return new BaseReturn<bool>
                {
                    Status = success,
                    Data = success,
                    Message = success ? "Member removed successfully" : "Failed to remove member"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing member {TargetUserId} from org {OrgId}", targetUserId, orgId);
                return new BaseReturn<bool>
                {
                    Status = false,
                    Message = "Failed to remove member"
                };
            }
        }

        public async Task InvalidateMemberCacheAsync(Guid orgId)
        {
            try
            {
                var cacheKey = $"org:members:{orgId}";
                await _cacheService.RemoveAsync(cacheKey);
                _logger.LogInformation("Invalidated member cache for org {OrgId}", orgId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating cache for org {OrgId}", orgId);
                // Don't throw - cache invalidation failure shouldn't break operations
            }
        }
    }
}
