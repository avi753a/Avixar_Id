using Avixar.Entity;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Avixar.Data
{
    /// <summary>
    /// Repository implementation for organization data access
    /// Uses Dapper with PostgreSQL
    /// </summary>
    public class OrganizationRepository : IOrganizationRepository
    {
        private readonly string _connectionString;
        private readonly ILogger<OrganizationRepository> _logger;

        public OrganizationRepository(IConfiguration configuration, ILogger<OrganizationRepository> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") 
                ?? "Host=localhost;Port=5432;Database=avidevdb;Username=appuser;Password=Temp@123";
            _logger = logger;
        }

        public async Task<List<Organization>> GetOrganizationsByUserIdAsync(Guid userId)
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                var sql = @"
                    SELECT o.""Id"", o.""Name"", o.""Slug"", o.""IsPersonal"", o.""CreatedAt"", o.""UpdatedAt""
                    FROM orgs o
                    INNER JOIN org_users ou ON o.""Id"" = ou.""OrgId""
                    WHERE ou.""UserId"" = @UserId
                    ORDER BY o.""CreatedAt"" DESC";

                var orgs = await conn.QueryAsync<Organization>(sql, new { UserId = userId });
                return orgs.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting organizations for user {UserId}", userId);
                throw;
            }
        }

        public async Task<Organization?> GetOrganizationByIdAsync(Guid orgId)
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                var sql = @"
                    SELECT ""Id"", ""Name"", ""Slug"", ""IsPersonal"", ""CreatedAt"", ""UpdatedAt""
                    FROM orgs
                    WHERE ""Id"" = @OrgId";

                return await conn.QueryFirstOrDefaultAsync<Organization>(sql, new { OrgId = orgId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting organization {OrgId}", orgId);
                throw;
            }
        }

        public async Task<Guid> CreateOrganizationAsync(Organization org)
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                var sql = @"
                    INSERT INTO orgs (""Name"", ""Slug"", ""IsPersonal"", ""CreatedAt"", ""UpdatedAt"")
                    VALUES (@Name, @Slug, @IsPersonal, NOW(), NOW())
                    RETURNING ""Id""";

                var orgId = await conn.ExecuteScalarAsync<Guid>(sql, new
                {
                    org.Name,
                    org.Slug,
                    org.IsPersonal
                });

                return orgId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating organization {Name}", org.Name);
                throw;
            }
        }

        public async Task<bool> UpdateOrganizationAsync(Organization org)
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                var sql = @"
                    UPDATE orgs
                    SET ""Name"" = @Name, ""Slug"" = @Slug, ""UpdatedAt"" = NOW()
                    WHERE ""Id"" = @Id";

                var rows = await conn.ExecuteAsync(sql, new
                {
                    org.Id,
                    org.Name,
                    org.Slug
                });

                return rows > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating organization {OrgId}", org.Id);
                throw;
            }
        }

        public async Task<bool> DeleteOrganizationAsync(Guid orgId)
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                var sql = @"DELETE FROM orgs WHERE ""Id"" = @OrgId";

                var rows = await conn.ExecuteAsync(sql, new { OrgId = orgId });
                return rows > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting organization {OrgId}", orgId);
                throw;
            }
        }
    }
}
