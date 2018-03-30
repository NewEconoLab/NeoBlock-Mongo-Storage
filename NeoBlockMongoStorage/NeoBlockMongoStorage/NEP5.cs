using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace NeoBlockMongoStorage
{
    public class NEP5
    {
        public bool checkTransfer(JObject notification)
        {
            JArray JA = (JArray)notification["state"]["value"];
            string hexString = (string)JA[0]["value"];

            if (hexString == "7472616e73666572")
            { return true; }
            else
            { return false; }
        }

        [BsonIgnoreExtraElements]
        public class Asset {
            public Asset(string netType,string Assetid){
                assetid = Assetid;
                try
                {
                    string decimalsHex = neoContractHelper.getNEP5ContractInfo(netType, assetid.Replace("0x", ""), "decimals");
                    decimals = int.Parse(decimalsHex);

                    string totalSupplyHex = neoContractHelper.getNEP5ContractInfo(netType, assetid.Replace("0x", ""), "totalSupply");
                    totalsupply = getNumFromByteArray(totalSupplyHex, decimals);

                    string nameHex = neoContractHelper.getNEP5ContractInfo(netType, assetid.Replace("0x", ""), "name");
                    name = neoContractHelper.getStrFromHexstr(nameHex);

                    string symbolHex = neoContractHelper.getNEP5ContractInfo(netType, assetid.Replace("0x", ""), "symbol");
                    symbol = neoContractHelper.getStrFromHexstr(symbolHex);
                }
                catch(Exception ex)
                {
                    var a = ex.Message;
                }
            }

            public ObjectId _id { get; set; }
            public string assetid { get; set; }
            public decimal totalsupply { get; set; }
            public string name { get; set; }
            public string symbol { get; set; }
            public int decimals { get; set; }
        }

        [BsonIgnoreExtraElements]
        public class Transfer {
            public Transfer(int Blockindex,string Txid,int N,JObject notification,int decimals)
            {
                blockindex = Blockindex;
                txid = Txid;
                n = N;
                asset = (string)notification["contract"];

                JArray JA = (JArray)notification["state"]["value"];

                from = getAddrFromScriptHash((string)JA[1]["value"]);          
                to = getAddrFromScriptHash((string)JA[2]["value"]);

                string valueType = (string)JA[3]["type"];
                string valueString = (string)JA[3]["value"];
                if (valueType == "ByteArray")//标准nep5
                {
                    value = getNumFromByteArray(valueString, decimals);
                }
                else if (valueType == "Integer")//变种nep5
                {
                    value = getNumFromInteger(valueString, decimals);
                }
                else//未知情况用-1表示
                {
                    value = -1;
                }
                
                
            }

            public ObjectId _id { get; set; }
            public int blockindex { get; set; }
            public string txid { get; set; }
            public int n { get; set; }
            public string asset { get; set; }
            public string from { get; set; }
            public string to { get; set; }
            public decimal value { get; set; }
        }

        private static string getAddrFromScriptHash(string scripitHash)
        {
            if (scripitHash != string.Empty)
            {
                return ThinNeo.Helper.GetAddressFromScriptHash(ThinNeo.Helper.HexString2Bytes(scripitHash));
            }
            else
            { return string.Empty; } //ICO mintToken 等情况    
        }

        private static decimal getNumFromByteArray(string byteArray, int decimals)
        {
            byte[] bytes = ThinNeo.Helper.HexString2Bytes(byteArray).Reverse().ToArray();
            string hex = ThinNeo.Helper.Bytes2HexString(bytes);
            decimal num = Convert.ToInt64(hex, 16);
            num = num / (decimal)Math.Pow(10,decimals); //根据精度调整小数点

            return num;
        }

        private static decimal getNumFromInteger(string Integer, int decimals)
        {
            decimal num = decimal.Parse(Integer);
            num = num / (decimal)Math.Pow(10, decimals); //根据精度调整小数点

            return num;
        }
    }
}
