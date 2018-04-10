using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
            balanceOfUTXO = new List<assetBalance>();
            balanceOfNEP5 = new List<assetBalance>();
            //balances = new List<AddrBalance>();
        }

        public ObjectId _id { get; set; }
        public string addr { get; set; }
        public AddrUse firstuse { get; set; }
        public AddrUse lastuse { get; set; }
        public int txcount { get; set; }
        public List<assetBalance> balanceOfUTXO{ get;set;}
        public List<assetBalance> balanceOfNEP5 { get; set; }
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

    public class assetBalance {
        
        //UTXO资产构造方法
        public assetBalance(string mongodbConnStr ,string mongodbDatabase, string addr ,string assetID)
        {
            var client = new MongoClient(mongodbConnStr);
            var database = client.GetDatabase(mongodbDatabase);

            assetid = assetID;

            ////获取资产名称
            //var collAsset = database.GetCollection<BsonDocument>("asset");
            //var assetFindBson = BsonDocument.Parse("{id:'" + assetID + "'}");
            //var queryAsset = collAsset.Find(assetFindBson).ToList();
            //if (queryAsset.Count > 0)
            //{
            //    name = queryAsset[0]["name"].AsBsonArray[0]["name"].AsString;
            //    if (name == "小蚁股") { name = "NEO"; }
            //    if (name == "小蚁币") { name = "GAS"; }
            //}
            //else
            //{
            //    name = string.Empty;
            //}

            //获取某地址资产余额（UTXO求和）
            decimal num = 0;
            var collUTXO = database.GetCollection<BsonDocument>("utxo");
            var UTXOFindBson = BsonDocument.Parse("{addr:'" + addr + "',used:'',asset:'" + assetID + "'}");
            var queryUTXO = collUTXO.Find(UTXOFindBson).ToList();
            if (queryUTXO.Count > 0)
            {
                foreach (var utxo in queryUTXO)
                {
                    string utxoValue = utxo["value"].AsString;
                    num += decimal.Parse(utxoValue);
                }
            }
            balance = num.ToString();

            client = null;
        }

        //NEP5资产构造方法


        public string assetid { get; set; }
        //public string name { get; set; }
        public string balance { get; set; }
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
