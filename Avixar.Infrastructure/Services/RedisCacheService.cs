using StackExchange.Redis;
using System.Text.Json;
using Avixar.Infrastructure;

namespace Avixar.Infrastructure
{
    /// <summary>
    /// Redis cache service implementation
    /// </summary>
    public class RedisCacheService
    {
        private readonly IConnectionMultiplexer _redis;

        public RedisCacheService(IConnectionMultiplexer redis)
        {
            _redis = redis;
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan expiry)
        {
            var db = _redis.GetDatabase();
            var json = JsonSerializer.Serialize(value);
            await db.StringSetAsync(key, json, expiry);
        }

        public async Task<T?> GetAsync<T>(string key)
        {
            var db = _redis.GetDatabase();
            var json = await db.StringGetAsync(key);

            if (json.IsNullOrEmpty)
                return default;

            return JsonSerializer.Deserialize<T>(json.ToString());
        }

        public async Task RemoveAsync(string key)
        {
            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync(key);
        }

        public async Task DeleteAsync(string key)
        {
            await RemoveAsync(key);
        }
    }
}
