using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Text;

namespace NeoBlockMongoStorage
{
    [BsonIgnoreExtraElements]
    class UTXO
    {
        public ObjectId _id { get; set; }
        public string Addr { get; set; }
        public int LastBlockindex { get; set; }
        public List<UTXOrecord> UTXOrecord { get; set; }
    }

    class UTXOrecord {
        public string GetTx { get; set; }
        public int N { get; set; }
        public string Asset { get; set; }
        public decimal Value { get; set; }
        public string UseTx { get; set; }
    }
}
