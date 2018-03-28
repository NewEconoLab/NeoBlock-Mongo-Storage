using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using System.Linq;

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

        public class Transfer {
            public Transfer(string Txid,int N,JObject notification,int decimals)
            {
                txid = Txid;
                n = N;
                asset = (string)notification["contract"];

                JArray JA = (JArray)notification["state"]["value"];

                from = getAddrFromScriptHash((string)JA[1]["value"]);
                to = getAddrFromScriptHash((string)JA[2]["value"]);
                value = getNumFromByteArray((string)JA[3]["value"],decimals);
            }

            string txid { get; set; }
            int n { get; set; }
            string asset { get; set; }
            string from { get; set; }
            string to { get; set; }
            decimal value { get; set; }

        }

        private static string getAddrFromScriptHash(string scripitHash)
        {
            return ThinNeo.Helper.GetAddressFromScriptHash(ThinNeo.Helper.HexString2Bytes(scripitHash));
        }

        private static decimal getNumFromByteArray(string byteArray, int decimals)
        {
            byte[] bytes = ThinNeo.Helper.HexString2Bytes(byteArray).Reverse().ToArray();
            string hex = ThinNeo.Helper.Bytes2HexString(bytes);
            decimal num = Convert.ToInt64(hex, 16);
            num = num / (decimal)Math.Pow(10,decimals); //根据精度调整小数点

            return num;
        }

    }
}
