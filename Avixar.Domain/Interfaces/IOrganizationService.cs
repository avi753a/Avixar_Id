using Avixar.Entity;
using Avixar.Entity;
using Avixar.Entity;

namespace Avixar.Domain
{
    /// <summary>
    /// Service interface for organization management
    /// </summary>
    public interface IOrganizationService
    {
        Task<BaseReturn<List<Organization>>> GetUserOrganizationsAsync(Guid userId);
        Task<BaseReturn<List<OrgContextDto>>> GetOrganizationContextsAsync(Guid userId);
        Task<BaseReturn<Guid>> CreateOrganizationAsync(Guid userId, CreateOrgDto dto);
        Task<BaseReturn<string>> SwitchContextAsync(Guid userId, Guid orgId);
    }
}
