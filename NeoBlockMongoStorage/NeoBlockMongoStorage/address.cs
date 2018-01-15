using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Text;

namespace NeoBlockMongoStorage
{
    [BsonIgnoreExtraElements]
    public class Address
    {
        public Address()
        {
            addr = string.Empty;
            firstuse = new AddrUse();
            lastuse = new AddrUse();
            txcount = 0;
            //balances = new List<AddrBalance>();
        }

        public ObjectId _id { get; set; }
        public string addr { get; set; }
        public AddrUse firstuse { get; set; }
        public AddrUse lastuse { get; set; }
        public int txcount  { get; set; }
        //public List<AddrBalance> balances { get; set; }
    }

    public class AddrUse
    {
        public AddrUse()
        {
            txid = string.Empty;
            blockindex = -1;
            blocktime = new DateTime();
        }

        public string txid { get; set; }
        public int blockindex { get; set; }
        public DateTime blocktime { get; set; }
    }

    //public class AssetName {
    //    public AssetName(){
    //        lang = string.Empty;
    //        name = string.Empty;
    //    }

    //    public string lang;
    //    public string name;
    //}

    //public class AddrBalance {

    //    public AddrBalance() {
    //        asset = string.Empty;
    //        balance = 0;
    //        names = new List<AssetName>();
    //    }

    //    public string asset { get; set; }
    //    public decimal balance { get; set; }
    //    public List<AssetName> names { get; set; }
    //}
}
