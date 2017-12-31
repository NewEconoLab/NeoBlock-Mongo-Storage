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
        public UTXO(){
            addr = string.Empty;
            txid = string.Empty;
            n = 0;
            asset = string.Empty;
            value = 0;
            used = string.Empty;
        }

        public ObjectId _id { get; set; }
        public string addr { get; set; }
        public string txid { get; set; }
        public int n { get; set; }
        public string asset { get; set; }
        public decimal value { get; set; }
        public string used { get; set; }
    }
}
