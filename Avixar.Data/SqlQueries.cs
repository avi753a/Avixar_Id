namespace Avixar.Data
{
    /// <summary>
    /// SQL query constants for repository operations
    /// </summary>
    public static class SqlQueries
    {
        // Client queries
        public const string GetClient = @"
            SELECT client_id, client_name, client_secret, allowed_redirect_uris, allowed_logout_uris
            FROM oauth_clients
            WHERE client_id = @Id";

        // User authentication queries
        public const string LoginLocal = @"
            SELECT ""Id"", ""Email"", ""PasswordHash"", ""DisplayName"", ""FirstName"", ""LastName"", 
                   ""ProfilePictureUrl"", ""EmailConfirmed"", ""PhoneNumber"", ""PhoneNumberConfirmed"",
                   ""TwoFactorEnabled"", ""LockoutEnd"", ""LockoutEnabled"", ""AccessFailedCount""
            FROM ""AspNetUsers""
            WHERE ""Email"" = @email";

        // User retrieval queries
        public const string GetUserById = @"
            SELECT ""Id"", ""Email"", ""PasswordHash"", ""DisplayName"", ""FirstName"", ""LastName"", 
                   ""ProfilePictureUrl"", ""EmailConfirmed"", ""PhoneNumber"", ""PhoneNumberConfirmed"",
                   ""TwoFactorEnabled"", ""LockoutEnd"", ""LockoutEnabled"", ""AccessFailedCount""
            FROM ""AspNetUsers""
            WHERE ""Id"" = @userId";

        public const string GetUserByEmail = @"
            SELECT ""Id"", ""Email"", ""PasswordHash"", ""DisplayName"", ""FirstName"", ""LastName"", 
                   ""ProfilePictureUrl"", ""EmailConfirmed"", ""PhoneNumber"", ""PhoneNumberConfirmed"",
                   ""TwoFactorEnabled"", ""LockoutEnd"", ""LockoutEnabled"", ""AccessFailedCount""
            FROM ""AspNetUsers""
            WHERE ""Email"" = @email";

        // Alias for GetUserById
        public const string GetUser = GetUserById;

        // Password update query
        public const string UpdateUserPassword = @"
            UPDATE ""AspNetUsers""
            SET ""PasswordHash"" = @PasswordHash
            WHERE ""Id"" = @Id";

        // Email verification update query
        public const string UpdateEmailVerified = @"
            UPDATE ""AspNetUsers""
            SET ""EmailConfirmed"" = @EmailConfirmed
            WHERE ""Id"" = @Id";

        // User update queries
        public const string UpdateUser = @"
            UPDATE ""AspNetUsers""
            SET ""DisplayName"" = @DisplayName,
                ""FirstName"" = @FirstName,
                ""LastName"" = @LastName,
                ""ProfilePictureUrl"" = @ProfilePictureUrl,
                ""PhoneNumber"" = @PhoneNumber,
                ""EmailConfirmed"" = @EmailConfirmed,
                ""PhoneNumberConfirmed"" = @PhoneNumberConfirmed,
                ""TwoFactorEnabled"" = @TwoFactorEnabled
            WHERE ""Id"" = @Id";

        // User address queries
        public const string GetUserAddresses = @"
            SELECT ""Id"", ""UserId"", ""AddressLine1"", ""AddressLine2"", ""City"", ""State"", 
                   ""PostalCode"", ""Country"", ""AddressType"", ""IsDefault"", ""CreatedAt""
            FROM user_addresses
            WHERE ""UserId"" = @userId";

        public const string AddUserAddress = @"
            INSERT INTO user_addresses (""Id"", ""UserId"", ""AddressLine1"", ""AddressLine2"", ""City"", 
                                       ""State"", ""PostalCode"", ""Country"", ""AddressType"", ""IsDefault"", ""CreatedAt"")
            VALUES (@Id, @UserId, @AddressLine1, @AddressLine2, @City, @State, @PostalCode, @Country, @AddressType, @IsDefault, @CreatedAt)";

        public const string UpdateUserAddress = @"
            UPDATE user_addresses
            SET ""AddressLine1"" = @AddressLine1,
                ""AddressLine2"" = @AddressLine2,
                ""City"" = @City,
                ""State"" = @State,
                ""PostalCode"" = @PostalCode,
                ""Country"" = @Country,
                ""AddressType"" = @AddressType,
                ""IsDefault"" = @IsDefault
            WHERE ""Id"" = @Id AND ""UserId"" = @UserId";

        public const string DeleteUserAddress = @"
            DELETE FROM user_addresses
            WHERE ""Id"" = @addressId AND ""UserId"" = @userId";

        // User settings queries
        public const string GetUserSettings = @"
            SELECT ""UserId"", ""TwoFactorEnabled"", ""EmailVerified"", ""PhoneVerified"", 
                   ""NotificationsEnabled"", ""CreatedAt"", ""UpdatedAt""
            FROM user_settings
            WHERE ""UserId"" = @userId";

        public const string UpsertUserSettings = @"
            INSERT INTO user_settings (""UserId"", ""TwoFactorEnabled"", ""EmailVerified"", ""PhoneVerified"", 
                                      ""NotificationsEnabled"", ""CreatedAt"", ""UpdatedAt"")
            VALUES (@UserId, @TwoFactorEnabled, @EmailVerified, @PhoneVerified, @NotificationsEnabled, @CreatedAt, @UpdatedAt)
            ON CONFLICT (""UserId"")
            DO UPDATE SET
                ""TwoFactorEnabled"" = @TwoFactorEnabled,
                ""EmailVerified"" = @EmailVerified,
                ""PhoneVerified"" = @PhoneVerified,
                ""NotificationsEnabled"" = @NotificationsEnabled,
                ""UpdatedAt"" = @UpdatedAt";

        // Wallet queries
        public const string GetUserWallet = @"
            SELECT ""Id"", ""UserId"", ""Balance"", ""Currency"", ""CreatedAt"", ""UpdatedAt""
            FROM wallets
            WHERE ""UserId"" = @userId";

        public const string UpdateWalletBalance = @"
            UPDATE wallets
            SET ""Balance"" = @Balance,
                ""UpdatedAt"" = @UpdatedAt
            WHERE ""UserId"" = @userId";
    }
}
