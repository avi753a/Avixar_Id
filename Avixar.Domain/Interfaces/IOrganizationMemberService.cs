using Avixar.Entity;

namespace Avixar.Domain
{
    /// <summary>
    /// Service interface for organization member management
    /// </summary>
    public interface IOrganizationMemberService
    {
        Task<BaseReturn<List<OrganizationMember>>> GetOrgMembersAsync(Guid orgId, Guid requestingUserId);
        Task<BaseReturn<bool>> InviteMemberAsync(Guid orgId, Guid requestingUserId, InviteMemberDto dto);
        Task<BaseReturn<bool>> UpdateMemberRoleAsync(Guid orgId, Guid requestingUserId, Guid targetUserId, int newRoleId);
        Task<BaseReturn<bool>> RemoveMemberAsync(Guid orgId, Guid requestingUserId, Guid targetUserId);
        Task InvalidateMemberCacheAsync(Guid orgId);
    }
}
