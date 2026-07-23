using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ModaPanelApi.models
{
    public class DesignCollection
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public List<DesignImage> Images { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public class DesignImage
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string ImageUrl { get; set; } = "";
        public string PublicId { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class UpdateDesignCollectionRequest
    {
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
    }
}
