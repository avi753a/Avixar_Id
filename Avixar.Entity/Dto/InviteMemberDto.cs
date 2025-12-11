namespace Avixar.Entity
{
    /// <summary>
    /// DTO for inviting a member to an organization
    /// </summary>
    public class InviteMemberDto
    {
        public string Email { get; set; } = string.Empty;
        public int RoleId { get; set; }
    }
}
