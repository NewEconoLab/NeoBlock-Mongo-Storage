using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Text;

namespace NeoBlockMongoStorage
{
    //[BsonIgnoreExtraElements]
    //class UTXO
    //{
    //    public ObjectId _id { get; set; }
    //    public string Addr { get; set; }
    //    public int LastBlockindex { get; set; }
    //    public List<UTXOrecord> UTXOrecord { get; set; }
    //}

    [BsonIgnoreExtraElements]
    class UTXO {
        public ObjectId _id { get; set; }
        public string addr { get; set; }
        public string voutTx { get; set; }
        public int voutN { get; set; }
        public string asset { get; set; }
        public decimal value { get; set; }
        public string vinTx { get; set; }
    }
}
