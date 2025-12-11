namespace Avixar.Entity
{
    /// <summary>
    /// DTO for creating a new organization
    /// </summary>
    public class CreateOrgDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Slug { get; set; }
    }
}
