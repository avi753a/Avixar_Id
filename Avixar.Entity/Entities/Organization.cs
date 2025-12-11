namespace Avixar.Entity
{
    /// <summary>
    /// Represents an organization in the system
    /// Maps to the 'orgs' table in PostgreSQL
    /// </summary>
    public class Organization
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Slug { get; set; }
        public bool IsPersonal { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
