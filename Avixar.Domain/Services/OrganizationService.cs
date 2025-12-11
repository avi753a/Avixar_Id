using Avixar.Data;
using Avixar.Entity;
using Avixar.Entity;
using Avixar.Entity;
using Avixar.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Avixar.Domain
{
    /// <summary>
    /// Service implementation for organization management
    /// </summary>
    public class OrganizationService : IOrganizationService
    {
        private readonly IOrganizationRepository _orgRepo;
        private readonly IOrganizationMemberRepository _memberRepo;
        private readonly TokenService _tokenService;
        private readonly ILogger<OrganizationService> _logger;

        public OrganizationService(
            IOrganizationRepository orgRepo,
            IOrganizationMemberRepository memberRepo,
            TokenService tokenService,
            ILogger<OrganizationService> logger)
        {
            _orgRepo = orgRepo;
            _memberRepo = memberRepo;
            _tokenService = tokenService;
            _logger = logger;
        }

        public async Task<BaseReturn<List<Organization>>> GetUserOrganizationsAsync(Guid userId)
        {
            try
            {
                _logger.LogInformation("Getting organizations for user {UserId}", userId);
                var orgs = await _orgRepo.GetOrganizationsByUserIdAsync(userId);
                
                return new BaseReturn<List<Organization>>
                {
                    Status = true,
                    Data = orgs,
                    Message = $"Found {orgs.Count} organizations"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting organizations for user {UserId}", userId);
                return new BaseReturn<List<Organization>>
                {
                    Status = false,
                    Message = "Failed to retrieve organizations"
                };
            }
        }

        public async Task<BaseReturn<List<OrgContextDto>>> GetOrganizationContextsAsync(Guid userId)
        {
            try
            {
                _logger.LogInformation("Getting organization contexts for user {UserId}", userId);
                var contexts = await _memberRepo.GetUserContextsAsync(userId);
                
                return new BaseReturn<List<OrgContextDto>>
                {
                    Status = true,
                    Data = contexts,
                    Message = $"Found {contexts.Count} contexts"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting contexts for user {UserId}", userId);
                return new BaseReturn<List<OrgContextDto>>
                {
                    Status = false,
                    Message = "Failed to retrieve contexts"
                };
            }
        }

        public async Task<BaseReturn<Guid>> CreateOrganizationAsync(Guid userId, CreateOrgDto dto)
        {
            try
            {
                _logger.LogInformation("Creating organization {Name} for user {UserId}", dto.Name, userId);

                // Create organization
                var org = new Organization
                {
                    Name = dto.Name,
                    Slug = dto.Slug ?? dto.Name.ToLower().Replace(" ", "-"),
                    IsPersonal = false,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                var orgId = await _orgRepo.CreateOrganizationAsync(org);

                // Add creator as OWNER (role ID 1)
                await _memberRepo.AddMemberToOrgAsync(userId, orgId, 1);

                _logger.LogInformation("Created organization {OrgId} with user {UserId} as OWNER", orgId, userId);

                return new BaseReturn<Guid>
                {
                    Status = true,
                    Data = orgId,
                    Message = "Organization created successfully"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating organization for user {UserId}", userId);
                return new BaseReturn<Guid>
                {
                    Status = false,
                    Message = "Failed to create organization"
                };
            }
        }

        public async Task<BaseReturn<string>> SwitchContextAsync(Guid userId, Guid orgId)
        {
            try
            {
                _logger.LogInformation("Switching context for user {UserId} to org {OrgId}", userId, orgId);

                // Get user's role in the organization
                var roleId = await _memberRepo.GetMemberRoleAsync(userId, orgId);
                
                if (roleId == null)
                {
                    return new BaseReturn<string>
                    {
                        Status = false,
                        Message = "User is not a member of this organization"
                    };
                }

                // Get organization details
                var org = await _orgRepo.GetOrganizationByIdAsync(orgId);
                if (org == null)
                {
                    return new BaseReturn<string>
                    {
                        Status = false,
                        Message = "Organization not found"
                    };
                }

                // Get role name
                var contexts = await _memberRepo.GetUserContextsAsync(userId);
                var context = contexts.FirstOrDefault(c => c.OrgId == orgId);
                
                if (context == null)
                {
                    return new BaseReturn<string>
                    {
                        Status = false,
                        Message = "Context not found"
                    };
                }

                // Generate new JWT with org/role claims
                var token = _tokenService.GenerateJwtWithContext(
                    userId, 
                    "", // email will be fetched from user data
                    "", // displayName will be fetched from user data
                    orgId, 
                    roleId.Value, 
                    context.RoleName
                );

                _logger.LogInformation("Context switched successfully for user {UserId}", userId);

                return new BaseReturn<string>
                {
                    Status = true,
                    Data = token,
                    Message = "Context switched successfully"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error switching context for user {UserId}", userId);
                return new BaseReturn<string>
                {
                    Status = false,
                    Message = "Failed to switch context"
                };
            }
        }
    }
}
