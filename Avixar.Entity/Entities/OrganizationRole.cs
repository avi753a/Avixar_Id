namespace Avixar.Entity
{
    /// <summary>
    /// Represents a role within an organization
    /// Maps to the 'org_roles' table in PostgreSQL
    /// Common roles: OWNER (1), ADMIN (2), MANAGER (3), USER (4)
    /// </summary>
    public class OrganizationRole
    {
        public int Id { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public string? Description { get; set; }
    }
}
