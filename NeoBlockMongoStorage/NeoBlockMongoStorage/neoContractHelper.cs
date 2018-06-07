using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NeoBlockMongoStorage
{
    static class neoContractHelper
    {
        static CoreHttpHelper chh = new CoreHttpHelper();

        public static string getNEP5ContractInfo(string netType,string scripthash, string key)
        {
            string apiUrl = string.Empty;
            if (netType == "NeoBlockData_mainnet")
            {
                apiUrl = "https://api.nel.group/api/mainnet";
            }
            else
            {
                apiUrl = "https://api.nel.group/api/testnet";
            }

            string result = string.Empty;
            try
            {
                JObject postData = new JObject();
                postData.Add("jsonrpc", "2.0");
                postData.Add("method", "callcontractfortest");
                postData.Add("params", JArray.Parse("['" + scripthash + "',['(str)" + key + "',[]]]"));
                postData.Add("id", 1);
                string postDataStr = Newtonsoft.Json.JsonConvert.SerializeObject(postData);

                //json格式post
                string resNotify = chh.Post(apiUrl, postDataStr, Encoding.UTF8,1);

                string valueHex = (string)JObject.Parse(resNotify)["result"][0]["stack"][0]["value"];

                result = valueHex;
            }
            catch (Exception ex)
            {
                var a = ex.Message;
            }

            Thread.Sleep(50);//防止过度调用接口导致cli卡死

            return result;
        }

        public static string getStrFromHexstr(string hexStr)
        {
            List<byte> byteArray = new List<byte>();

            for (int i = 0; i < hexStr.Length; i = i + 2)
            {
                string s = hexStr.Substring(i, 2);
                byteArray.Add(Convert.ToByte(s, 16));
            }

            string str = System.Text.Encoding.UTF8.GetString(byteArray.ToArray());

            return str;
        }
    }
}
