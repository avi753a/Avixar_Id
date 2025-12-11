using Avixar.Entity;
using Avixar.Entity;
using Avixar.Entity;

namespace Avixar.Data
{
    public interface IUserRepository
    {
        Task<Guid> LoginWithSocialAsync(string provider, string subjectId, string email, string displayName, string? pictureUrl);
        Task<UserCredentials?> LoginLocalAsync(string email);
        Task<Guid> RegisterLocalAsync(string email, string password, string displayName);
        Task<ApplicationUser?> GetUserAsync(Guid userId);
        Task<ApplicationUser?> GetUserByEmailAsync(string email);
        Task<bool> UpdateUserAsync(ApplicationUser user);
        
        // Address Management
        Task<List<UserAddress>> GetUserAddressesAsync(Guid userId);
        Task<bool> AddUserAddressAsync(UserAddress address);
        Task<bool> UpdateUserAddressAsync(UserAddress address);
        Task<bool> DeleteUserAddressAsync(Guid addressId, Guid userId);

        // User Settings
        Task<UserSettings?> GetUserSettingsAsync(Guid userId);
        Task<bool> UpsertUserSettingsAsync(UserSettings settings);
        Task<bool> MarkEmailAsVerifiedAsync(Guid userId);
        
        // MongoDB - Login History & Security Events
        Task<List<LoginHistory>> GetLoginHistoryAsync(Guid userId, int limit = 50);
        Task LogLoginAttemptAsync(LoginHistory log);
        Task LogSecurityEventAsync(Guid userId, string eventType, string details);
    }
}
