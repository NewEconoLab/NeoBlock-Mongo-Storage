using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace NeoBlockMongoStorage
{
    [BsonIgnoreExtraElements]
    class BlockSysfee
    {
        public BlockSysfee(int blockIndex) {
            index = blockIndex;
            totalSysfee = 0;
        }

        public ObjectId _id { get; set; }
        public int index { get; set; }
        public decimal totalSysfee { get; set; }
    }
}
