using Avixar.Entity;
using Avixar.Entity;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Avixar.Data
{
    /// <summary>
    /// Repository implementation for organization member data access
    /// Uses Dapper with PostgreSQL
    /// </summary>
    public class OrganizationMemberRepository : IOrganizationMemberRepository
    {
        private readonly string _connectionString;
        private readonly ILogger<OrganizationMemberRepository> _logger;

        public OrganizationMemberRepository(IConfiguration configuration, ILogger<OrganizationMemberRepository> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") 
                ?? "Host=localhost;Port=5432;Database=avidevdb;Username=appuser;Password=Temp@123";
            _logger = logger;
        }

        public async Task<List<OrganizationMember>> GetMembersByOrgIdAsync(Guid orgId)
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                var sql = @"
                    SELECT 
                        ou.""UserId"", 
                        ou.""OrgId"", 
                        ou.""RoleId"", 
                        ou.""JoinedAt"",
                        u.""DisplayName"" AS UserDisplayName,
                        r.""RoleName""
                    FROM org_users ou
                    INNER JOIN users u ON ou.""UserId"" = u.""Id""
                    INNER JOIN org_roles r ON ou.""RoleId"" = r.""Id""
                    WHERE ou.""OrgId"" = @OrgId
                    ORDER BY ou.""JoinedAt"" ASC";

                var members = await conn.QueryAsync<OrganizationMember>(sql, new { OrgId = orgId });
                return members.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting members for organization {OrgId}", orgId);
                throw;
            }
        }

        public async Task<List<OrgContextDto>> GetUserContextsAsync(Guid userId)
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                var sql = @"
                    SELECT 
                        o.""Id"" AS OrgId,
                        o.""Name"" AS OrgName,
                        ou.""RoleId"",
                        r.""RoleName""
                    FROM org_users ou
                    INNER JOIN orgs o ON ou.""OrgId"" = o.""Id""
                    INNER JOIN org_roles r ON ou.""RoleId"" = r.""Id""
                    WHERE ou.""UserId"" = @UserId
                    ORDER BY o.""Name"" ASC";

                var contexts = await conn.QueryAsync<OrgContextDto>(sql, new { UserId = userId });
                return contexts.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting contexts for user {UserId}", userId);
                throw;
            }
        }

        public async Task<bool> AddMemberToOrgAsync(Guid userId, Guid orgId, int roleId)
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                var sql = @"
                    INSERT INTO org_users (""UserId"", ""OrgId"", ""RoleId"", ""JoinedAt"")
                    VALUES (@UserId, @OrgId, @RoleId, NOW())";

                var rows = await conn.ExecuteAsync(sql, new { UserId = userId, OrgId = orgId, RoleId = roleId });
                return rows > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding member {UserId} to organization {OrgId}", userId, orgId);
                throw;
            }
        }

        public async Task<bool> UpdateMemberRoleAsync(Guid userId, Guid orgId, int roleId)
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                var sql = @"
                    UPDATE org_users
                    SET ""RoleId"" = @RoleId
                    WHERE ""UserId"" = @UserId AND ""OrgId"" = @OrgId";

                var rows = await conn.ExecuteAsync(sql, new { UserId = userId, OrgId = orgId, RoleId = roleId });
                return rows > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating role for member {UserId} in organization {OrgId}", userId, orgId);
                throw;
            }
        }

        public async Task<bool> RemoveMemberFromOrgAsync(Guid userId, Guid orgId)
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                var sql = @"
                    DELETE FROM org_users
                    WHERE ""UserId"" = @UserId AND ""OrgId"" = @OrgId";

                var rows = await conn.ExecuteAsync(sql, new { UserId = userId, OrgId = orgId });
                return rows > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing member {UserId} from organization {OrgId}", userId, orgId);
                throw;
            }
        }

        public async Task<int?> GetMemberRoleAsync(Guid userId, Guid orgId)
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                var sql = @"
                    SELECT ""RoleId""
                    FROM org_users
                    WHERE ""UserId"" = @UserId AND ""OrgId"" = @OrgId";

                return await conn.QueryFirstOrDefaultAsync<int?>(sql, new { UserId = userId, OrgId = orgId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting role for member {UserId} in organization {OrgId}", userId, orgId);
                throw;
            }
        }
    }
}
