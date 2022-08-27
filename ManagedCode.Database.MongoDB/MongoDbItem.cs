using ManagedCode.Database.Core;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ManagedCode.Database.MongoDB;

public class MongoDbItem : IItem<ObjectId>
{
    public MongoDbItem()
    {
        Id = ObjectId.GenerateNewId();
    }

    public MongoDbItem(string id)
    {
        Id = ObjectId.Parse(id);
    }

    public MongoDbItem(ObjectId id)
    {
        Id = id;
    }

    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public ObjectId Id { get; set; }
}