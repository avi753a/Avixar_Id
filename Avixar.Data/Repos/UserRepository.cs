using Avixar.Entity;
using Avixar.Infrastructure;
using BCrypt.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Data;

namespace Avixar.Data
{
    public class UserRepository : IUserRepository
    {
        private readonly string _connString;
        private readonly string _encKey;
        private readonly string _blindKey;
        private readonly ILogger<UserRepository> _logger;
        private readonly MongoDbService? _mongoDbService;

        public UserRepository(IConfiguration config, ILogger<UserRepository> logger, MongoDbService? mongoDbService = null)
        {
            _connString = config.GetDefaultConnectionString();
            _encKey = config.GetEncryptionKey();
            _blindKey = config.GetBlindIndexKey();
            _logger = logger;
            _mongoDbService = mongoDbService;
        }

        #region sp_sociallogin Logic
        public async Task<Guid> LoginWithSocialAsync(string provider, string subjectId, string email, string displayName, string? pictureUrl)
        {
            try
            {
                string emailHash = "";
                _logger.LogInformation("Social login attempt - Provider: {Provider}, Email: {Email}", provider, email);

                using (var conn = new NpgsqlConnection(_connString))
                {
                    await conn.OpenAsync();
                    await SetDBEncryptionKeyVariables(conn);

                    // Check if provider link already exists
                    string checkProviderSql = @"
                        SELECT ""UserId"" FROM ""user_providers""
                        WHERE ""Provider"" = @provider::auth_provider
                          AND ""ProviderSubjectId"" = @subjectId";

                    using (var checkProviderCmd = new NpgsqlCommand(checkProviderSql, conn))
                    {
                        checkProviderCmd.Parameters.AddWithValue("provider", provider.ToUpper());
                        checkProviderCmd.Parameters.AddWithValue("subjectId", subjectId);

                        var existingUserId = await checkProviderCmd.ExecuteScalarAsync();
                        if (existingUserId != null)
                        {
                            var userId = (Guid)existingUserId;
                            _logger.LogInformation("Social login successful - Existing provider link found, UserId: {UserId}", userId);
                            return userId;
                        }
                    }

                    // Check if user exists by email
                    Guid? existingUserIdByEmail = null;
                    if (!string.IsNullOrWhiteSpace(email))
                    {
                        emailHash = await ComputeEmailHashAsync(conn, email);

                        string checkEmailSql = @"
                            SELECT ""Id"" FROM ""user_secrets""
                            WHERE ""Email_Hash"" = @emailHash";

                        using (var checkEmailCmd = new NpgsqlCommand(checkEmailSql, conn))
                        {
                            checkEmailCmd.Parameters.AddWithValue("emailHash", emailHash);
                            var result = await checkEmailCmd.ExecuteScalarAsync();

                            if (result != null)
                            {
                                existingUserIdByEmail = (Guid)result;
                            }
                        }
                    }

                    // If user exists by email, link the provider
                    if (existingUserIdByEmail.HasValue)
                    {
                        string linkProviderSql = @"
                            INSERT INTO ""user_providers"" (""UserId"", ""Provider"", ""ProviderSubjectId"")
                            VALUES (@userId, @provider::auth_provider, @subjectId)";

                        using (var linkProviderCmd = new NpgsqlCommand(linkProviderSql, conn))
                        {
                            linkProviderCmd.Parameters.AddWithValue("userId", existingUserIdByEmail.Value);
                            linkProviderCmd.Parameters.AddWithValue("provider", provider.ToUpper());
                            linkProviderCmd.Parameters.AddWithValue("subjectId", subjectId);

                            await linkProviderCmd.ExecuteNonQueryAsync();
                        }

                        _logger.LogInformation("Social login successful - Linked provider to existing user, UserId: {UserId}", existingUserIdByEmail.Value);
                        return existingUserIdByEmail.Value;
                    }

                    // Create new user
                    Guid newUserId;
                    byte[]? emailEnc = null;

                    if (!string.IsNullOrWhiteSpace(email))
                    {
                        emailEnc = await EncryptEmailAsync(conn, email);
                        // emailHash is already computed above
                    }

                    string insertUserSql = @"
                        INSERT INTO ""users"" (""DisplayName"", ""ProfilePictureUrl"")
                        VALUES (@displayName, @pictureUrl)
                        RETURNING ""Id""";

                    using (var insertUserCmd = new NpgsqlCommand(insertUserSql, conn))
                    {
                        insertUserCmd.Parameters.AddWithValue("displayName", displayName ?? "User");
                        insertUserCmd.Parameters.AddWithValue("pictureUrl", (object?)pictureUrl ?? DBNull.Value);

                        var result = await insertUserCmd.ExecuteScalarAsync();
                        newUserId = (Guid)result!;
                    }

                    // Insert user secrets
                    string insertSecretsSql = @"
                        INSERT INTO ""user_secrets"" (""Id"", ""Email_Enc"", ""Email_Hash"")
                        VALUES (@id, @emailEnc, @emailHash)";

                    using (var insertSecretsCmd = new NpgsqlCommand(insertSecretsSql, conn))
                    {
                        insertSecretsCmd.Parameters.AddWithValue("id", newUserId);
                        insertSecretsCmd.Parameters.AddWithValue("emailEnc", (object?)emailEnc ?? DBNull.Value);
                        insertSecretsCmd.Parameters.AddWithValue("emailHash", (object?)emailHash ?? DBNull.Value);

                        await insertSecretsCmd.ExecuteNonQueryAsync();
                    }

                    // Link provider
                    string insertProviderSql = @"
                        INSERT INTO ""user_providers"" (""UserId"", ""Provider"", ""ProviderSubjectId"")
                        VALUES (@userId, @provider::auth_provider, @subjectId)";

                    using (var insertProviderCmd = new NpgsqlCommand(insertProviderSql, conn))
                    {
                        insertProviderCmd.Parameters.AddWithValue("userId", newUserId);
                        insertProviderCmd.Parameters.AddWithValue("provider", provider.ToUpper());
                        insertProviderCmd.Parameters.AddWithValue("subjectId", subjectId);

                        await insertProviderCmd.ExecuteNonQueryAsync();
                    }

                    _logger.LogInformation("Social login successful - New user created, UserId: {UserId}", newUserId);
                    return newUserId;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during social login - Provider: {Provider}, Email: {Email}", provider, email);
                throw;
            }
        }
        #endregion

        public async Task<UserCredentials?> LoginLocalAsync(string email)
        {
            try
            {
                _logger.LogInformation("Local login attempt for email: {Email}", email);

                using (var conn = new NpgsqlConnection(_connString))
                {
                    await conn.OpenAsync();
                    await SetDBEncryptionKeyVariables(conn);

                    // Compute Hash for Lookup
                    string emailHash = await ComputeEmailHashAsync(conn, email);

                    // Inline Query: Joins users, secrets, and settings
                    string sql = @"
                        SELECT u.""Id"", 
                               pgp_sym_decrypt(s.""Email_Enc"", current_setting('app.enc_key', true)) as ""Email"", 
                               s.""PasswordHash"", 
                               u.""DisplayName"", 
                               u.first_name as ""FirstName"", 
                               u.last_name as ""LastName"", 
                               u.""ProfilePictureUrl""
                        FROM users u
                        INNER JOIN user_secrets s ON u.""Id"" = s.""Id""
                        WHERE s.""Email_Hash"" = @email";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("email", emailHash); // Pass the Hash

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                var userId = reader.GetGuid(reader.GetOrdinal("Id"));
                                _logger.LogInformation("Local login successful - UserId: {UserId}", userId);

                                return new UserCredentials
                                {
                                    UserId = userId,
                                    DisplayName = reader.IsDBNull(reader.GetOrdinal("DisplayName")) ? "" : reader.GetString(reader.GetOrdinal("DisplayName")),
                                    PasswordHash = reader.IsDBNull(reader.GetOrdinal("PasswordHash")) ? "" : reader.GetString(reader.GetOrdinal("PasswordHash")),
                                    Email = reader.IsDBNull(reader.GetOrdinal("Email")) ? "" : reader.GetString(reader.GetOrdinal("Email")),
                                    ProfilePictureUrl = reader.IsDBNull(reader.GetOrdinal("ProfilePictureUrl")) ? null : reader.GetString(reader.GetOrdinal("ProfilePictureUrl"))
                                };
                            }
                        }
                    }

                    _logger.LogWarning("Local login failed - User not found for email: {Email}", email);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during local login for email: {Email}", email);
                throw;
            }
        }

        #region sp_registeruser Logic
        public async Task<Guid> RegisterLocalAsync(string email, string password, string displayName)
        {
            try
            {
                _logger.LogInformation("Registration attempt for email: {Email}", email);

                if (string.IsNullOrWhiteSpace(email))
                {
                    throw new Exception("Email is required.");
                }

                using (var conn = new NpgsqlConnection(_connString))
                {
                    await conn.OpenAsync();
                    await SetDBEncryptionKeyVariables(conn);

                    // Hash the email to check for duplicates
                    string emailHash = await ComputeEmailHashAsync(conn, email);

                    // Check if user already exists
                    string checkSql = @"SELECT 1 FROM ""user_secrets"" WHERE ""Email_Hash"" = @emailHash";
                    using (var checkCmd = new NpgsqlCommand(checkSql, conn))
                    {
                        checkCmd.Parameters.AddWithValue("emailHash", emailHash);
                        var exists = await checkCmd.ExecuteScalarAsync();

                        if (exists != null)
                        {
                            _logger.LogWarning("Registration failed - User already exists: {Email}", email);
                            throw new Exception("USER_EXISTS: An account with this email already exists.");
                        }
                    }

                    // Encrypt email
                    byte[] emailEnc = await EncryptEmailAsync(conn, email);

                    // Hash password
                    string passwordHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 11);

                    // Insert into users table
                    Guid newUserId;
                    string insertUserSql = @"
                        INSERT INTO ""users"" (""DisplayName"", ""LastLoginAt"")
                        VALUES (@displayName, @lastLoginAt)
                        RETURNING ""Id""";

                    using (var insertUserCmd = new NpgsqlCommand(insertUserSql, conn))
                    {
                        insertUserCmd.Parameters.AddWithValue("displayName", displayName);
                        insertUserCmd.Parameters.AddWithValue("lastLoginAt", DateTime.UtcNow);

                        var result = await insertUserCmd.ExecuteScalarAsync();
                        newUserId = (Guid)result!;
                    }

                    // Insert into user_secrets table
                    string insertSecretsSql = @"
                        INSERT INTO ""user_secrets"" 
                        (""Id"", ""PasswordHash"", ""Email_Enc"", ""Email_Hash"")
                        VALUES (@id, @passwordHash, @emailEnc, @emailHash)";

                    using (var insertSecretsCmd = new NpgsqlCommand(insertSecretsSql, conn))
                    {
                        insertSecretsCmd.Parameters.AddWithValue("id", newUserId);
                        insertSecretsCmd.Parameters.AddWithValue("passwordHash", passwordHash);
                        insertSecretsCmd.Parameters.AddWithValue("emailEnc", emailEnc);
                        insertSecretsCmd.Parameters.AddWithValue("emailHash", emailHash);

                        await insertSecretsCmd.ExecuteNonQueryAsync();
                    }

                    _logger.LogInformation("Registration successful - UserId: {UserId}", newUserId);
                    return newUserId;
                }
            }
            catch (Exception ex) when (!ex.Message.StartsWith("USER_EXISTS"))
            {
                _logger.LogError(ex, "Error during registration for email: {Email}", email);
                throw;
            }
        }
        #endregion

        public async Task<ApplicationUser?> GetUserAsync(Guid userId)
        {
            try
            {
                _logger.LogInformation("Getting user: {UserId}", userId);

                using (var conn = new NpgsqlConnection(_connString))
                {
                    await conn.OpenAsync();
                    await SetDBEncryptionKeyVariables(conn);

                    // Inline Query
                    string sql = @"
                        SELECT u.""Id"", 
                               pgp_sym_decrypt(s.""Email_Enc"", current_setting('app.enc_key', true)) as ""Email"", 
                               s.""PasswordHash"", 
                               u.""DisplayName"", 
                               u.first_name as ""FirstName"", 
                               u.last_name as ""LastName"", 
                               u.""ProfilePictureUrl"", 
                               st.two_factor_enabled as ""TwoFactorEnabled""
                        FROM users u
                        INNER JOIN user_secrets s ON u.""Id"" = s.""Id""
                        LEFT JOIN user_settings st ON u.""Id"" = st.user_id
                        WHERE u.""Id"" = @userId";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("userId", userId);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                var user = new ApplicationUser
                                {
                                    Id = reader.GetGuid(reader.GetOrdinal("Id")).ToString(),
                                    DisplayName = reader.IsDBNull(reader.GetOrdinal("DisplayName")) ? "" : reader.GetString(reader.GetOrdinal("DisplayName")),
                                    ProfilePictureUrl = reader.IsDBNull(reader.GetOrdinal("ProfilePictureUrl")) ? "" : reader.GetString(reader.GetOrdinal("ProfilePictureUrl")),
                                    FirstName = reader.IsDBNull(reader.GetOrdinal("FirstName")) ? null : reader.GetString(reader.GetOrdinal("FirstName")),
                                    LastName = reader.IsDBNull(reader.GetOrdinal("LastName")) ? null : reader.GetString(reader.GetOrdinal("LastName")),
                                    Email = reader.IsDBNull(reader.GetOrdinal("Email")) ? "" : reader.GetString(reader.GetOrdinal("Email")),
                                    UserName = reader.IsDBNull(reader.GetOrdinal("Email")) ? "" : reader.GetString(reader.GetOrdinal("Email")),
                                    TwoFactorEnabled = !reader.IsDBNull(reader.GetOrdinal("TwoFactorEnabled")) && reader.GetBoolean(reader.GetOrdinal("TwoFactorEnabled"))
                                };

                                _logger.LogInformation("User found: {UserId}", userId);
                                return user;
                            }
                        }
                    }
                }

                _logger.LogWarning("User not found: {UserId}", userId);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user: {UserId}", userId);
                throw;
            }
        }

        public async Task<ApplicationUser?> GetUserByEmailAsync(string email)
        {
            try
            {
                _logger.LogInformation("Getting user by email");

                using (var conn = new NpgsqlConnection(_connString))
                {
                    await conn.OpenAsync();
                    await SetDBEncryptionKeyVariables(conn);

                    string emailHash = await ComputeEmailHashAsync(conn, email);

                    // Inline Query
                    string sql = @"
                        SELECT u.""Id"", 
                               pgp_sym_decrypt(s.""Email_Enc"", current_setting('app.enc_key', true)) as ""Email"", 
                               s.""PasswordHash"", 
                               u.""DisplayName"", 
                               u.first_name as ""FirstName"", 
                               u.last_name as ""LastName"", 
                               u.""ProfilePictureUrl"", 
                               st.two_factor_enabled as ""TwoFactorEnabled""
                        FROM users u
                        INNER JOIN user_secrets s ON u.""Id"" = s.""Id""
                        LEFT JOIN user_settings st ON u.""Id"" = st.user_id
                        WHERE s.""Email_Hash"" = @email";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("email", emailHash);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                var user = new ApplicationUser
                                {
                                    Id = reader.GetGuid(reader.GetOrdinal("Id")).ToString(),
                                    DisplayName = reader.IsDBNull(reader.GetOrdinal("DisplayName")) ? "" : reader.GetString(reader.GetOrdinal("DisplayName")),
                                    ProfilePictureUrl = reader.IsDBNull(reader.GetOrdinal("ProfilePictureUrl")) ? "" : reader.GetString(reader.GetOrdinal("ProfilePictureUrl")),
                                    FirstName = reader.IsDBNull(reader.GetOrdinal("FirstName")) ? null : reader.GetString(reader.GetOrdinal("FirstName")),
                                    LastName = reader.IsDBNull(reader.GetOrdinal("LastName")) ? null : reader.GetString(reader.GetOrdinal("LastName")),
                                    PasswordHash = reader.IsDBNull(reader.GetOrdinal("PasswordHash")) ? "" : reader.GetString(reader.GetOrdinal("PasswordHash")),
                                    Email = reader.IsDBNull(reader.GetOrdinal("Email")) ? "" : reader.GetString(reader.GetOrdinal("Email")),
                                    UserName = reader.IsDBNull(reader.GetOrdinal("Email")) ? "" : reader.GetString(reader.GetOrdinal("Email")),
                                    TwoFactorEnabled = !reader.IsDBNull(reader.GetOrdinal("TwoFactorEnabled")) && reader.GetBoolean(reader.GetOrdinal("TwoFactorEnabled"))
                                };

                                _logger.LogInformation("User found by email");
                                return user;
                            }
                        }
                    }
                }

                _logger.LogWarning("User not found by email");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user by email");
                throw;
            }
        }

        public async Task<bool> UpdateUserAsync(ApplicationUser user)
        {
            try
            {
                _logger.LogInformation("Updating user: {UserId}", user.Id);

                using var conn = new NpgsqlConnection(_connString);
                await conn.OpenAsync();

                // 1. Update Profile (users table) - Inline Query
                // Removed TwoFactorEnabled/Email from here as they are not passed in parameters
                string updateProfileSql = @"
                    UPDATE users
                    SET ""DisplayName"" = @DisplayName,
                        first_name = @FirstName,
                        last_name = @LastName,
                        ""ProfilePictureUrl"" = @ProfilePictureUrl
                    WHERE ""Id"" = @Id";

                using var cmd = new NpgsqlCommand(updateProfileSql, conn);
                cmd.Parameters.AddWithValue("Id", Guid.Parse(user.Id));
                cmd.Parameters.AddWithValue("FirstName", (object?)user.FirstName ?? DBNull.Value);
                cmd.Parameters.AddWithValue("LastName", (object?)user.LastName ?? DBNull.Value);
                cmd.Parameters.AddWithValue("DisplayName", (object?)user.DisplayName ?? DBNull.Value);
                cmd.Parameters.AddWithValue("ProfilePictureUrl", (object?)user.ProfilePictureUrl ?? DBNull.Value);

                var rows = await cmd.ExecuteNonQueryAsync();

                // 2. Update Password (user_secrets table) if provided - Inline Query
                if (!string.IsNullOrEmpty(user.PasswordHash))
                {
                    string updatePwdSql = @"
                        UPDATE user_secrets
                        SET ""PasswordHash"" = @PasswordHash
                        WHERE ""Id"" = @Id";

                    using var pwdCmd = new NpgsqlCommand(updatePwdSql, conn);
                    pwdCmd.Parameters.AddWithValue("Id", Guid.Parse(user.Id));
                    pwdCmd.Parameters.AddWithValue("PasswordHash", user.PasswordHash);
                    await pwdCmd.ExecuteNonQueryAsync();
                }

                var success = rows > 0;

                if (success)
                    _logger.LogInformation("Successfully updated user: {UserId}", user.Id);
                else
                    _logger.LogWarning("Failed to update user: {UserId}", user.Id);

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user: {UserId}", user.Id);
                throw;
            }
        }

        public async Task<List<UserAddress>> GetUserAddressesAsync(Guid userId)
        {
            try
            {
                _logger.LogInformation("Getting addresses for user: {UserId}", userId);

                var addresses = new List<UserAddress>();
                using var conn = new NpgsqlConnection(_connString);
                await conn.OpenAsync();

                // Inline Query: Maps DB columns to expected aliases
                string sql = @"
                    SELECT id as ""Id"", 
                           user_id as ""UserId"", 
                           label as ""AddressType"", 
                           address_line_1 as ""AddressLine1"", 
                           address_line_2 as ""AddressLine2"", 
                           city as ""City"", 
                           postal_code as ""PostalCode"", 
                           created_at as ""CreatedAt""
                    FROM user_addresses
                    WHERE user_id = @userId";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("userId", userId);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    addresses.Add(new UserAddress
                    {
                        Id = reader.GetGuid(reader.GetOrdinal("Id")),
                        UserId = reader.GetGuid(reader.GetOrdinal("UserId")),
                        Label = reader.IsDBNull(reader.GetOrdinal("AddressType")) ? null : reader.GetString(reader.GetOrdinal("AddressType")),
                        AddressLine1 = reader.GetString(reader.GetOrdinal("AddressLine1")),
                        AddressLine2 = reader.IsDBNull(reader.GetOrdinal("AddressLine2")) ? null : reader.GetString(reader.GetOrdinal("AddressLine2")),
                        City = reader.GetString(reader.GetOrdinal("City")),
                        PostalCode = reader.GetString(reader.GetOrdinal("PostalCode")),
                        CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt"))
                    });
                }

                _logger.LogInformation("Retrieved {Count} addresses for user: {UserId}", addresses.Count, userId);
                return addresses;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting addresses for user: {UserId}", userId);
                throw;
            }
        }

        public async Task<bool> AddUserAddressAsync(UserAddress address)
        {
            try
            {
                _logger.LogInformation("Adding address for user: {UserId}", address.UserId);

                using var conn = new NpgsqlConnection(_connString);
                await conn.OpenAsync();

                // Inline Query
                string sql = @"
                    INSERT INTO user_addresses (id, user_id, label, address_line_1, address_line_2, city, postal_code, created_at)
                    VALUES (@Id, @UserId, @AddressType, @AddressLine1, @AddressLine2, @City, @PostalCode, @CreatedAt)";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("Id", address.Id);
                cmd.Parameters.AddWithValue("UserId", address.UserId);
                cmd.Parameters.AddWithValue("AddressType", (object?)address.Label ?? DBNull.Value);
                cmd.Parameters.AddWithValue("AddressLine1", address.AddressLine1);
                cmd.Parameters.AddWithValue("AddressLine2", (object?)address.AddressLine2 ?? DBNull.Value);
                cmd.Parameters.AddWithValue("City", address.City);
                cmd.Parameters.AddWithValue("PostalCode", address.PostalCode);
                cmd.Parameters.AddWithValue("CreatedAt", address.CreatedAt == default ? DateTime.UtcNow : address.CreatedAt);

                var rows = await cmd.ExecuteNonQueryAsync();
                var success = rows > 0;

                if (success)
                    _logger.LogInformation("Successfully added address {AddressId} for user: {UserId}", address.Id, address.UserId);
                else
                    _logger.LogWarning("Failed to add address for user: {UserId}", address.UserId);

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding address for user: {UserId}", address.UserId);
                throw;
            }
        }

        public async Task<bool> UpdateUserAddressAsync(UserAddress address)
        {
            try
            {
                _logger.LogInformation("Updating address {AddressId} for user: {UserId}", address.Id, address.UserId);

                using var conn = new NpgsqlConnection(_connString);
                await conn.OpenAsync();

                // Inline Query
                string sql = @"
                    UPDATE user_addresses
                    SET label = @AddressType,
                        address_line_1 = @AddressLine1,
                        address_line_2 = @AddressLine2,
                        city = @City,
                        postal_code = @PostalCode
                    WHERE id = @Id AND user_id = @UserId";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("Id", address.Id);
                cmd.Parameters.AddWithValue("UserId", address.UserId);
                cmd.Parameters.AddWithValue("AddressType", (object?)address.Label ?? DBNull.Value);
                cmd.Parameters.AddWithValue("AddressLine1", address.AddressLine1);
                cmd.Parameters.AddWithValue("AddressLine2", (object?)address.AddressLine2 ?? DBNull.Value);
                cmd.Parameters.AddWithValue("City", address.City);
                cmd.Parameters.AddWithValue("PostalCode", address.PostalCode);

                var rows = await cmd.ExecuteNonQueryAsync();
                var success = rows > 0;

                if (success)
                    _logger.LogInformation("Successfully updated address {AddressId}", address.Id);
                else
                    _logger.LogWarning("Failed to update address {AddressId}", address.Id);

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating address {AddressId}", address.Id);
                throw;
            }
        }

        public async Task<bool> DeleteUserAddressAsync(Guid addressId, Guid userId)
        {
            try
            {
                _logger.LogInformation("Deleting address {AddressId} for user: {UserId}", addressId, userId);

                using var conn = new NpgsqlConnection(_connString);
                await conn.OpenAsync();

                // Inline Query
                string sql = @"
                    DELETE FROM user_addresses
                    WHERE id = @addressId AND user_id = @userId";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("addressId", addressId);
                cmd.Parameters.AddWithValue("userId", userId);

                var rows = await cmd.ExecuteNonQueryAsync();
                var success = rows > 0;

                if (success)
                    _logger.LogInformation("Successfully deleted address {AddressId}", addressId);
                else
                    _logger.LogWarning("Failed to delete address {AddressId}", addressId);

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting address {AddressId}", addressId);
                throw;
            }
        }

        #region User Settings Methods
        public async Task<UserSettings?> GetUserSettingsAsync(Guid userId)
        {
            try
            {
                _logger.LogInformation("Getting user settings for {UserId}", userId);

                using var conn = new NpgsqlConnection(_connString);
                await conn.OpenAsync();

                // Inline Query
                string sql = @"
                    SELECT user_id as ""UserId"", 
                           two_factor_enabled as ""TwoFactorEnabled"", 
                           email_verified as ""EmailVerified"", 
                           email_verified_at as ""EmailVerifiedAt"",
                           email_notifications as ""NotificationsEnabled"", 
                           created_at as ""CreatedAt"", 
                           updated_at as ""UpdatedAt""
                    FROM user_settings
                    WHERE user_id = @userId";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("userId", userId);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return new UserSettings
                    {
                        UserId = reader.GetGuid(reader.GetOrdinal("UserId")),
                        TwoFactorEnabled = reader.GetBoolean(reader.GetOrdinal("TwoFactorEnabled")),
                        EmailVerified = reader.GetBoolean(reader.GetOrdinal("EmailVerified")),
                        EmailVerifiedAt = reader.IsDBNull(reader.GetOrdinal("EmailVerifiedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("EmailVerifiedAt")),
                        EmailNotifications = reader.GetBoolean(reader.GetOrdinal("NotificationsEnabled")),
                        CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                        UpdatedAt = reader.GetDateTime(reader.GetOrdinal("UpdatedAt"))
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user settings for {UserId}", userId);
                throw;
            }
        }

        public async Task<bool> UpsertUserSettingsAsync(UserSettings settings)
        {
            try
            {
                _logger.LogInformation("Upserting user settings for {UserId}", settings.UserId);

                using var conn = new NpgsqlConnection(_connString);
                await conn.OpenAsync();

                // Inline Query
                string sql = @"
                    INSERT INTO user_settings (user_id, two_factor_enabled, email_verified, email_notifications, created_at, updated_at)
                    VALUES (@UserId, @TwoFactorEnabled, @EmailVerified, @NotificationsEnabled, @CreatedAt, @UpdatedAt)
                    ON CONFLICT (user_id)
                    DO UPDATE SET
                        two_factor_enabled = @TwoFactorEnabled,
                        email_verified = @EmailVerified,
                        email_notifications = @NotificationsEnabled,
                        updated_at = @UpdatedAt";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("UserId", settings.UserId);
                cmd.Parameters.AddWithValue("TwoFactorEnabled", settings.TwoFactorEnabled);
                cmd.Parameters.AddWithValue("EmailVerified", settings.EmailVerified);
                cmd.Parameters.AddWithValue("NotificationsEnabled", settings.EmailNotifications);
                cmd.Parameters.AddWithValue("CreatedAt", DateTime.UtcNow);
                cmd.Parameters.AddWithValue("UpdatedAt", DateTime.UtcNow);

                var rows = await cmd.ExecuteNonQueryAsync();
                return rows > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error upserting user settings for {UserId}", settings.UserId);
                throw;
            }
        }

        public async Task<bool> MarkEmailAsVerifiedAsync(Guid userId)
        {
            try
            {
                _logger.LogInformation("Marking email as verified for {UserId}", userId);

                using var conn = new NpgsqlConnection(_connString);
                await conn.OpenAsync();

                // Inline Query
                string sql = @"
                    INSERT INTO user_settings (user_id, email_verified, email_verified_at, updated_at)
                    VALUES (@Id, @EmailConfirmed, CASE WHEN @EmailConfirmed = true THEN NOW() ELSE NULL END, NOW())
                    ON CONFLICT (user_id) 
                    DO UPDATE SET 
                        email_verified = @EmailConfirmed,
                        email_verified_at = CASE WHEN @EmailConfirmed = true THEN NOW() ELSE NULL END,
                        updated_at = NOW()";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("Id", userId);
                cmd.Parameters.AddWithValue("EmailConfirmed", true);

                var rows = await cmd.ExecuteNonQueryAsync();
                return rows > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking email as verified for {UserId}", userId);
                throw;
            }
        }
        #endregion

        #region Helper Methods
        private async Task SetDBEncryptionKeyVariables(NpgsqlConnection conn)
        {
            try
            {
                using (var keyCmd = new NpgsqlCommand())
                {
                    keyCmd.Connection = conn;
                    keyCmd.CommandText = DataUtility.GetEncryptionKeys(_encKey, _blindKey);
                    await keyCmd.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to set encryption key variables.", ex);
            }
        }

        private async Task<string> ComputeEmailHashAsync(NpgsqlConnection conn, string email)
        {
            using (var cmd = new NpgsqlCommand(@"SELECT encode(hmac(@email, current_setting('app.blind_key'), 'sha256'), 'hex')", conn))
            {
                cmd.Parameters.AddWithValue("email", email);
                var result = await cmd.ExecuteScalarAsync();
                return (string)result!;
            }
        }

        private async Task<byte[]> EncryptEmailAsync(NpgsqlConnection conn, string email)
        {
            using (var cmd = new NpgsqlCommand(@"SELECT pgp_sym_encrypt(@email, current_setting('app.enc_key'))", conn))
            {
                cmd.Parameters.AddWithValue("email", email);
                var result = await cmd.ExecuteScalarAsync();
                return (byte[])result!;
            }
        }
        #endregion

        #region MongoDB - Login History & Security Events
        public async Task<List<LoginHistory>> GetLoginHistoryAsync(Guid userId, int limit = 50)
        {
            try
            {
                if (_mongoDbService == null)
                {
                    _logger.LogWarning("MongoDB service not available for login history");
                    return new List<LoginHistory>();
                }

                _logger.LogInformation("Getting login history for user {UserId}", userId);

                var filter = MongoDB.Driver.Builders<LoginHistory>.Filter.Eq("user_id", userId);
                var history = await _mongoDbService.FindAsync("login_history", filter, limit);

                return history.OrderByDescending(h => h.LoginTime).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting login history for user {UserId}", userId);
                throw;
            }
        }

        public async Task LogLoginAttemptAsync(LoginHistory log)
        {
            try
            {
                if (_mongoDbService == null)
                {
                    _logger.LogWarning("MongoDB service not available for login logging");
                    return;
                }

                log.LoginTime = DateTime.UtcNow;
                await _mongoDbService.InsertAsync("login_history", log);

                _logger.LogInformation("Logged login attempt for user {UserId}, Success: {Success}",
                    log.UserId, log.Success);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging login attempt for user {UserId}", log.UserId);
            }
        }

        public async Task LogSecurityEventAsync(Guid userId, string eventType, string details)
        {
            try
            {
                if (_mongoDbService == null)
                {
                    _logger.LogWarning("MongoDB service not available for security event logging");
                    return;
                }

                var securityEvent = new
                {
                    user_id = userId,
                    event_type = eventType,
                    details = details,
                    timestamp = DateTime.UtcNow
                };

                await _mongoDbService.InsertAsync("security_events", securityEvent);

                _logger.LogInformation("Logged security event for user {UserId}: {EventType}",
                    userId, eventType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging security event for user {UserId}", userId);
            }
        }
        #endregion
    }
}