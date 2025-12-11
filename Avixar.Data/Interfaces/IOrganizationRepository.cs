using Avixar.Entity;

namespace Avixar.Data
{
    /// <summary>
    /// Repository interface for organization data access
    /// </summary>
    public interface IOrganizationRepository
    {
        Task<List<Organization>> GetOrganizationsByUserIdAsync(Guid userId);
        Task<Organization?> GetOrganizationByIdAsync(Guid orgId);
        Task<Guid> CreateOrganizationAsync(Organization org);
        Task<bool> UpdateOrganizationAsync(Organization org);
        Task<bool> DeleteOrganizationAsync(Guid orgId);
    }
}
