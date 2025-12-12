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
            FROM clients
            WHERE client_id = @Id";

        // User authentication queries
        // JOINs users (profile), user_secrets (password/hash), and user_settings (2FA status)
        // NOTE: This assumes 'app.enc_key' is set in the session for decryption, 
        // and @email passed is the HMAC SHA256 Hash of the email.
        public const string LoginLocal = @"
            SELECT u.""Id"", 
                   pgp_sym_decrypt(s.""Email_Enc"", current_setting('app.enc_key', true)) as ""Email"", 
                   s.""PasswordHash"", 
                   u.""DisplayName"", 
                   u.first_name as ""FirstName"", 
                   u.last_name as ""LastName"", 
                   u.""ProfilePictureUrl"", 
                   st.email_verified as ""EmailConfirmed"", 
                   pgp_sym_decrypt(s.""Mobile_Enc"", current_setting('app.enc_key', true)) as ""PhoneNumber"", 
                   st.two_factor_enabled as ""TwoFactorEnabled""
            FROM users u
            INNER JOIN user_secrets s ON u.""Id"" = s.""Id""
            LEFT JOIN user_settings st ON u.""Id"" = st.user_id
            WHERE s.""Email_Hash"" = @email";

        // User retrieval queries
        public const string GetUserById = @"
            SELECT u.""Id"", 
                   pgp_sym_decrypt(s.""Email_Enc"", current_setting('app.enc_key', true)) as ""Email"", 
                   s.""PasswordHash"", 
                   u.""DisplayName"", 
                   u.first_name as ""FirstName"", 
                   u.last_name as ""LastName"", 
                   u.""ProfilePictureUrl"", 
                   st.email_verified as ""EmailConfirmed"", 
                   pgp_sym_decrypt(s.""Mobile_Enc"", current_setting('app.enc_key', true)) as ""PhoneNumber"", 
                   st.two_factor_enabled as ""TwoFactorEnabled""
            FROM users u
            INNER JOIN user_secrets s ON u.""Id"" = s.""Id""
            LEFT JOIN user_settings st ON u.""Id"" = st.user_id
            WHERE u.""Id"" = @userId";

        public const string GetUserByEmail = @"
            SELECT u.""Id"", 
                   pgp_sym_decrypt(s.""Email_Enc"", current_setting('app.enc_key', true)) as ""Email"", 
                   s.""PasswordHash"", 
                   u.""DisplayName"", 
                   u.first_name as ""FirstName"", 
                   u.last_name as ""LastName"", 
                   u.""ProfilePictureUrl"", 
                   st.email_verified as ""EmailConfirmed"", 
                   pgp_sym_decrypt(s.""Mobile_Enc"", current_setting('app.enc_key', true)) as ""PhoneNumber"", 
                   st.two_factor_enabled as ""TwoFactorEnabled""
            FROM users u
            INNER JOIN user_secrets s ON u.""Id"" = s.""Id""
            LEFT JOIN user_settings st ON u.""Id"" = st.user_id
            WHERE s.""Email_Hash"" = @email";

        // Alias for GetUserById
        public const string GetUser = GetUserById;

        // Password update query (Targets user_secrets)
        public const string UpdateUserPassword = @"
            UPDATE user_secrets
            SET ""PasswordHash"" = @PasswordHash
            WHERE ""Id"" = @Id";

        // Email verification update query (Targets user_settings)
        public const string UpdateEmailVerified = @"
            INSERT INTO user_settings (user_id, email_verified, email_verified_at, updated_at)
            VALUES (@Id, @EmailConfirmed, CASE WHEN @EmailConfirmed = true THEN NOW() ELSE NULL END, NOW())
            ON CONFLICT (user_id) 
            DO UPDATE SET 
                email_verified = @EmailConfirmed,
                email_verified_at = CASE WHEN @EmailConfirmed = true THEN NOW() ELSE NULL END,
                updated_at = NOW()";

        // User update queries (Split between users and user_settings)
        // Note: Updating Email/Phone requires re-encryption and hashing in user_secrets, 
        // which is complex for a single UPDATE string. This query updates the Profile and Settings.
        public const string UpdateUser = @"
    UPDATE users
    SET ""DisplayName"" = @DisplayName,
        first_name = @FirstName,
        last_name = @LastName,
        ""ProfilePictureUrl"" = @ProfilePictureUrl
    WHERE ""Id"" = @Id";

        // User address queries
        // Schema only has: id, user_id, label, address_line_1, address_line_2, city, postal_code, created_at
        // 'State', 'Country', 'AddressType', 'IsDefault' do not exist in the dump. 
        // Mapped 'label' to AddressType concept.
        public const string GetUserAddresses = @"
            SELECT id as ""Id"", user_id as ""UserId"", label as ""AddressType"", 
                   address_line_1 as ""AddressLine1"", address_line_2 as ""AddressLine2"", 
                   city as ""City"", postal_code as ""PostalCode"", created_at as ""CreatedAt""
            FROM user_addresses
            WHERE user_id = @userId";

        public const string AddUserAddress = @"
            INSERT INTO user_addresses (id, user_id, label, address_line_1, address_line_2, city, postal_code, created_at)
            VALUES (@Id, @UserId, @AddressType, @AddressLine1, @AddressLine2, @City, @PostalCode, @CreatedAt)";

        public const string UpdateUserAddress = @"
            UPDATE user_addresses
            SET label = @AddressType,
                address_line_1 = @AddressLine1,
                address_line_2 = @AddressLine2,
                city = @City,
                postal_code = @PostalCode
            WHERE id = @Id AND user_id = @UserId";

        public const string DeleteUserAddress = @"
            DELETE FROM user_addresses
            WHERE id = @addressId AND user_id = @userId";

        // User settings queries
        public const string GetUserSettings = @"
            SELECT user_id as ""UserId"", two_factor_enabled as ""TwoFactorEnabled"", 
                   email_verified as ""EmailVerified"", 
                   email_verified_at as ""EmailVerifiedAt"",
                   email_notifications as ""NotificationsEnabled"", 
                   created_at as ""CreatedAt"", updated_at as ""UpdatedAt""
            FROM user_settings
            WHERE user_id = @userId";

        public const string UpsertUserSettings = @"
            INSERT INTO user_settings (user_id, two_factor_enabled, email_verified, email_notifications, created_at, updated_at)
            VALUES (@UserId, @TwoFactorEnabled, @EmailVerified, @NotificationsEnabled, @CreatedAt, @UpdatedAt)
            ON CONFLICT (user_id)
            DO UPDATE SET
                two_factor_enabled = @TwoFactorEnabled,
                email_verified = @EmailVerified,
                email_notifications = @NotificationsEnabled,
                updated_at = @UpdatedAt";

        // Wallet queries
        // NOTE: Table 'wallets' does not exist in the provided schema dump.
        /* 
        public const string GetUserWallet = @"
            SELECT ""Id"", ""UserId"", ""Balance"", ""Currency"", ""CreatedAt"", ""UpdatedAt""
            FROM wallets
            WHERE ""UserId"" = @userId";

        public const string UpdateWalletBalance = @"
            UPDATE wallets
            SET ""Balance"" = @Balance,
                ""UpdatedAt"" = @UpdatedAt
            WHERE ""UserId"" = @userId";
        */
    }
}