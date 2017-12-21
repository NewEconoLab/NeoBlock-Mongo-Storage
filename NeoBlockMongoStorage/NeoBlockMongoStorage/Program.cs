using System;
using MongoDB;
using MongoDB.Driver;
using MongoDB.Bson;
using System.Threading.Tasks;
using JsonRpc.CoreCLR.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Timers;
using System.Threading;

namespace NeoBlockMongoStorage
{
    class Program
    {
        //static string mongodbConnStr = "mongodb://118.31.39.242:27017";
        //static string mongodbDatabase = "NeoBlockBaseData";
        //static string NeoCliJsonRPCUrl = "http://118.31.39.242:20332";

        static string mongodbConnStr = "mongodb://127.0.0.1:27017";
        static string mongodbDatabase = "NeoBlockBaseData";
        static string NeoCliJsonRPCUrl = "http://127.0.0.1:20332";

        static void Main(string[] args)
        {
            Console.WriteLine("NeoBlockMongoStorage Start!");

            Console.WriteLine("Block MaxIndex in DB:" + GetBlockMaxIndex());

            while (true) {
                StorageBaseData();
                Thread.Sleep(100);
            }

            //Timer t = new Timer(100);
            //t.Enabled = true;
            //t.Elapsed += T_Elapsed;          

            //Console.ReadKey();
        }

        //private static void T_Elapsed(object sender, ElapsedEventArgs e)
        //{
        //    StorageBaseData();
        //}

        private static void StorageBaseData()
        {
            var maxIndex = GetBlockMaxIndex();
            var storageIndex = maxIndex + 1;

            //只处理没有存储过的
            if (!IsBlockStoraged(storageIndex)) {
                //获取Cli block数据
                string resBlock = GetNeoCliData("getblock", new object[]
                    {
                        storageIndex,
                        1
                    });
                //获取有效数据则存储Mongodb
                if (resBlock != "null") {
                    MongoInsert("block", resBlock);

                    Console.WriteLine("StorageBaseData On Block " + maxIndex);
                }
            }
        }

        private static string GetNeoCliData(string method, object[] paras)
        {
            Uri rpcEndpoint = new Uri(NeoCliJsonRPCUrl);
            JsonRpcWebClient rpc = new JsonRpcWebClient(rpcEndpoint);

            var response = rpc.InvokeAsync<JObject>(method, paras);
            JObject resJ = JObject.Parse(JsonConvert.SerializeObject(response.Result));
            var resStr = JsonConvert.SerializeObject(resJ["result"]);

            return resStr;
        }

        private static void MongoInsert(string collName,string jsonStr)
        {
            var client = new MongoClient(mongodbConnStr);
            var database = client.GetDatabase(mongodbDatabase);
            var collection = database.GetCollection<BsonDocument>(collName);

            var document = BsonDocument.Parse(jsonStr);

            collection.InsertOneAsync(document);}

        private static int GetBlockMaxIndex(){
            var client = new MongoClient(mongodbConnStr);
            var database = client.GetDatabase(mongodbDatabase);
            var collection = database.GetCollection<BsonDocument>("block");

            var sortBson = BsonDocument.Parse("{index:-1}");
            var query = collection.Find(new BsonDocument()).Sort(sortBson).Limit(1).ToList();
            if (query.Count == 0)
            {
                return -1;
            }
            else
            {
                int maxIndex = (int)query[0]["index"];
                return maxIndex;
            }
        }

        private static bool IsBlockStoraged(int blockIndex) {
            var client = new MongoClient(mongodbConnStr);
            var database = client.GetDatabase(mongodbDatabase);
            var collection = database.GetCollection<BsonDocument>("block");

            var findBson = BsonDocument.Parse("{index:" + blockIndex + "}");
            var query = collection.Find(findBson).ToList();

            if (query.Count == 0){ return false;}
            else { return true; }
        }
    }
}
