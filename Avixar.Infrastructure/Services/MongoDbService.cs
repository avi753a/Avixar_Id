using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Avixar.Infrastructure
{
    /// <summary>
    /// MongoDB service implementation for document storage
    /// Used for login history, security events, and audit logs
    /// </summary>
    public class MongoDbService
    {
        private readonly IMongoDatabase _database;
        private readonly ILogger<MongoDbService> _logger;

        public MongoDbService(IConfiguration configuration, ILogger<MongoDbService> logger)
        {
            _logger = logger;
            
            var connectionString = configuration["MongoDB:ConnectionString"] 
                ?? "mongodb://localhost:27017";
            var databaseName = configuration["MongoDB:DatabaseName"] 
                ?? "avixar_identity";

            try
            {
                var client = new MongoClient(connectionString);
                _database = client.GetDatabase(databaseName);
                _logger.LogInformation("MongoDB connected successfully to database: {DatabaseName}", databaseName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to MongoDB");
                throw;
            }
        }

        public async Task InsertAsync<T>(string collectionName, T document)
        {
            try
            {
                var collection = _database.GetCollection<T>(collectionName);
                await collection.InsertOneAsync(document);
                _logger.LogDebug("Inserted document into collection: {CollectionName}", collectionName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inserting document into collection: {CollectionName}", collectionName);
                throw;
            }
        }

        public async Task<List<T>> FindAsync<T>(string collectionName, FilterDefinition<T> filter, int limit = 100)
        {
            try
            {
                var collection = _database.GetCollection<T>(collectionName);
                var documents = await collection.Find(filter)
                    .Limit(limit)
                    .ToListAsync();
                
                _logger.LogDebug("Found {Count} documents in collection: {CollectionName}", documents.Count, collectionName);
                return documents;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding documents in collection: {CollectionName}", collectionName);
                throw;
            }
        }

        public IMongoCollection<T> GetCollection<T>(string collectionName)
        {
            return _database.GetCollection<T>(collectionName);
        }
    }
}
