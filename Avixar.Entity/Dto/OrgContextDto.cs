namespace Avixar.Entity
{
    /// <summary>
    /// DTO representing an organization context for the context switcher
    /// Contains organization and role information for a user
    /// </summary>
    public class OrgContextDto
    {
        public Guid OrgId { get; set; }
        public string OrgName { get; set; } = string.Empty;
        public int RoleId { get; set; }
        public string RoleName { get; set; } = string.Empty;
    }
}
