namespace Avixar.Entity
{
    /// <summary>
    /// Represents a user's membership in an organization
    /// Maps to the 'org_users' table in PostgreSQL
    /// </summary>
    public class OrganizationMember
    {
        public Guid UserId { get; set; }
        public Guid OrgId { get; set; }
        public int RoleId { get; set; }
        public DateTime JoinedAt { get; set; }
        
        // Navigation properties (optional, for ORM use)
        public string? UserDisplayName { get; set; }
        public string? UserEmail { get; set; }
        public string? RoleName { get; set; }
    }
}
