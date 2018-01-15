using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Text;

namespace NeoBlockMongoStorage
{
    public class AddrUse {
        public AddrUse() {
            txid = string.Empty;
            block_index = -1;
            block_time = new DateTime();
        }

        public string txid { get; set; }
        public int block_index { get; set; }
        public DateTime block_time { get; set; }
    }

    public class AssetName {
        public AssetName(){
            lang = string.Empty;
            name = string.Empty;
        }

        public string lang;
        public string name;
    }

    public class AddrBalance {

        public AddrBalance() {
            asset = string.Empty;
            balance = 0;
            names = new List<AssetName>();
        }

        public string asset { get; set; }
        public decimal balance { get; set; }
        public List<AssetName> names { get; set; }
    }

    [BsonIgnoreExtraElements]
    public class Address
    {
        public Address()
        {
            addr = string.Empty;
            first_use = new AddrUse();
            last_use = new AddrUse();
            tx_count = 0;
            balances = new List<AddrBalance>();
        }

        public ObjectId _id { get; set; }
        public string addr { get; set; }
        public AddrUse first_use { get; set; }
        public AddrUse last_use { get; set; }
        public int tx_count  { get; set; }
        public List<AddrBalance> balances { get; set; }
    }
}
