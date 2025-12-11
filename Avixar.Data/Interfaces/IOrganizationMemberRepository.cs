using Avixar.Entity;
using Avixar.Entity;

namespace Avixar.Data
{
    /// <summary>
    /// Repository interface for organization member data access
    /// </summary>
    public interface IOrganizationMemberRepository
    {
        Task<List<OrganizationMember>> GetMembersByOrgIdAsync(Guid orgId);
        Task<List<OrgContextDto>> GetUserContextsAsync(Guid userId);
        Task<bool> AddMemberToOrgAsync(Guid userId, Guid orgId, int roleId);
        Task<bool> UpdateMemberRoleAsync(Guid userId, Guid orgId, int roleId);
        Task<bool> RemoveMemberFromOrgAsync(Guid userId, Guid orgId);
        Task<int?> GetMemberRoleAsync(Guid userId, Guid orgId);
    }
}
