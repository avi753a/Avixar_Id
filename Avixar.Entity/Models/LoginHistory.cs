using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Avixar.Entity
{
    /// <summary>
    /// Represents a login attempt record stored in MongoDB
    /// Used for security auditing and user login history
    /// </summary>
    public class LoginHistory
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }
        
        [BsonElement("user_id")]
        public Guid UserId { get; set; }
        
        [BsonElement("ip_address")]
        public string IpAddress { get; set; } = string.Empty;
        
        [BsonElement("user_agent")]
        public string UserAgent { get; set; } = string.Empty;
        
        [BsonElement("login_time")]
        public DateTime LoginTime { get; set; }
        
        [BsonElement("success")]
        public bool Success { get; set; }
        
        [BsonElement("failure_reason")]
        public string? FailureReason { get; set; }
        
        [BsonElement("login_method")]
        public string LoginMethod { get; set; } = "LOCAL"; // LOCAL, GOOGLE, MICROSOFT
    }
}
