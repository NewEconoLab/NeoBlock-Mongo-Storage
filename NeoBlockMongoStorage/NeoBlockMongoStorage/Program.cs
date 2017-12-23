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
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;

namespace NeoBlockMongoStorage
{
    class Program
    {
        static string mongodbConnStr = string.Empty;
        static string mongodbDatabase = string.Empty;
        static string NeoCliJsonRPCUrl = string.Empty;
        static int sleepTime = 0;

        static void Main(string[] args)
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection()    //将配置文件的数据加载到内存中
                .SetBasePath(System.IO.Directory.GetCurrentDirectory())   //指定配置文件所在的目录
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)  //指定加载的配置文件
                .Build();    //编译成对象  
            mongodbConnStr = config["mongodbConnStr"];
            mongodbDatabase = config["mongodbDatabase"];
            NeoCliJsonRPCUrl = config["NeoCliJsonRPCUrl"];
            sleepTime = int.Parse(config["sleepTime"]);

            Console.WriteLine("NeoBlockMongoStorage Start!");
            Console.WriteLine("*************************************");
            Console.WriteLine("mongodbConnStr" + mongodbConnStr);
            Console.WriteLine("mongodbDatabase" + mongodbDatabase);
            Console.WriteLine("NeoCliJsonRPCUrl" + NeoCliJsonRPCUrl);
            Console.WriteLine("sleepTime" + sleepTime);
            Console.WriteLine("*************************************");

            Console.WriteLine("Block MaxIndex in DB:" + GetBlockMaxIndex());

            while (true) {
                StorageBlockData();
                StorageTxData();
                StorageUTXOData();

                Thread.Sleep(sleepTime);
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

        private static void StorageBlockData()
        {
            DateTime start = DateTime.Now;

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

                    DateTime end = DateTime.Now;
                    var doTime = (end - start).TotalMilliseconds;
                    Console.WriteLine("StorageBlockData On Block " + maxIndex + " in " + doTime + "ms");
                }
            }
        }

        private static void StorageTxData()
        {          
            var maxBlockindex = GetTxMaxBlockindex();
            //检查当前已有区块是否已存所有交易
            DoStorageTxData(maxBlockindex);

            var storageBlockindex = maxBlockindex + 1;
            DoStorageTxData(storageBlockindex);
        }

        private static void DoStorageTxData(int doBlockIndex)
        {
            DateTime start = DateTime.Now;

            var client = new MongoClient(mongodbConnStr);
            var database = client.GetDatabase(mongodbDatabase);
            var collection = database.GetCollection<BsonDocument>("block");

            var findBson = BsonDocument.Parse("{index:" + doBlockIndex + "}");
            var query = collection.Find(findBson).ToList();
            if (query.Count > 0) {
                BsonDocument queryB = query[0].AsBsonDocument;

                foreach (BsonValue bv in queryB["tx"].AsBsonArray)
                {
                    string storageTxid = (string)bv["txid"];
                    storageTxid = storageTxid.Substring(2, storageTxid.Length - 2);
                    //只处理没有存储过的
                    if (!IsTxStoraged("0x" + storageTxid))
                    {
                        //获取Cli block数据
                        string resTx = GetNeoCliData("getrawtransaction", new object[]
                            {
                        storageTxid,
                        1
                            });
                        //获取有效数据则存储Mongodb
                        if (resTx != "null")
                        {
                            JObject txJ = JObject.Parse(resTx);
                            txJ.Remove("confirmations");
                            txJ.Add("blockindex", doBlockIndex);

                            var txJstr = JsonConvert.SerializeObject(txJ);
                            MongoInsert("tx", txJstr);

                            DateTime end = DateTime.Now;
                            var doTime = (end - start).TotalMilliseconds;
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine("StorageTxData On Block " + doBlockIndex + " in " + doTime + "ms");
                            Console.ForegroundColor = ConsoleColor.White;
                        }
                    }
                }
            } 
        }

        private static void StorageUTXOData()
        {
            var maxBlockindex = GetUTXOblockindex();
            //检查当前已有区块是否已处理所有交易utxo
            DoStorageUTXO(maxBlockindex);

            var storageBlockindex = maxBlockindex + 1;
            DoStorageUTXO(storageBlockindex);
        }

        private static void DoStorageUTXO(int doBlockIndex)
        {
            DateTime start = DateTime.Now;

            var client = new MongoClient(mongodbConnStr);
            var database = client.GetDatabase(mongodbDatabase);
            var collection = database.GetCollection<BsonDocument>("block");

            var findBson = BsonDocument.Parse("{index:" + doBlockIndex + "}");
            var query = collection.Find(findBson).ToList();
            if (query.Count > 0 && doBlockIndex <= GetTxMaxBlockindex())
            {
                BsonDocument queryB = query[0].AsBsonDocument;

                foreach (BsonValue bv in queryB["tx"].AsBsonArray)
                {
                    //获取数据库Tx数据
                    string doTxid = (string)bv["txid"];
                    collection = database.GetCollection<BsonDocument>("tx");
                    var queryTx = collection.Find(BsonDocument.Parse("{txid:'" + doTxid + "'}")).ToList()[0];

                    BsonArray vinBA = queryTx["vin"].AsBsonArray;
                    BsonArray voutBA = queryTx["vout"].AsBsonArray;

                    var collUTXO = database.GetCollection<UTXO>("utxo");
                    //先处理UTXO生成
                    if (voutBA.Count > 0) {
                        foreach (BsonValue voutBV in voutBA)
                        {
                            UTXO utxo = new UTXO();

                            string Addr= voutBV["address"].AsString;
                            bool isUTXOexist = IsDataExist("utxo", "Addr", Addr);
                            BsonDocument findB = BsonDocument.Parse("{Addr:'" + Addr + "'}");
                            if (isUTXOexist)//已有UTXO记录则更新
                            {
                                //获取已有UTXO记录                               
                                utxo = collUTXO.Find(findB).ToList()[0];                                
                            }
                            else
                            {
                                utxo.Addr = Addr;
                            }
                            //更新最后区块索引
                            utxo.LastBlockindex = doBlockIndex;
                            //添加本vout数据
                            UTXOrecord utxoR = new UTXOrecord
                            {
                                GetTx = doTxid,
                                N = voutBV["n"].AsInt32,
                                Asset = voutBV["asset"].AsString,
                                Value = decimal.Parse(voutBV["value"].AsString)
                            };
                            //检查当前UTXO记录是否已被记录过
                            bool isUTXOexist_R = false;
                            if (utxo.UTXOrecord != null)
                            {
                                foreach (var r in utxo.UTXOrecord)
                                {
                                    if (r.GetTx == utxoR.GetTx && r.N == utxoR.N)
                                    {
                                        isUTXOexist_R = true;
                                        break;
                                    }
                                }
                            }
                            else { utxo.UTXOrecord = new List<UTXOrecord>(); }
                            //只有不重复才会添加本vout数据并更新或插入
                            if (!isUTXOexist_R) {
                                utxo.UTXOrecord.Add(utxoR);

                                if (isUTXOexist)
                                {
                                    //已存在则更新
                                    collUTXO.ReplaceOne(findB, utxo);
                                }
                                else
                                {
                                    //不存在则插入
                                    collUTXO.InsertOne(utxo);
                                }
                            } 
                        }
                    }

                    //处理UTXO使用
                    if (vinBA.Count > 0) {
                        foreach (BsonValue vinBV in vinBA)
                        {
                            string vinTxid = vinBV["txid"].AsString;
                            int vinN = vinBV["vout"].AsInt32;

                            BsonDocument queryUTXOaddr = database.GetCollection<BsonDocument>("tx").Find(BsonDocument.Parse("{txid:'" + vinTxid + "'}")).ToList()[0];
                            BsonArray queryUTXOaddr_vout = queryUTXOaddr["vout"].AsBsonArray;
                            string Addr = string.Empty; 
                            foreach (BsonValue voutBV in queryUTXOaddr_vout)
                            {
                                if (voutBV["n"] == vinN) {
                                    Addr = voutBV["address"].AsString;
                                    break;
                                }
                            }

                            BsonDocument findB = BsonDocument.Parse("{Addr:'" + Addr + "'}");
                            UTXO utxo = database.GetCollection<UTXO>("utxo").Find(findB).ToList()[0];
                            bool isUseExist = false;//判断是否已被写过use
                            foreach (var r in utxo.UTXOrecord)
                            {
                                if (r.GetTx == vinTxid && r.N == vinN)
                                {
                                    if (r.UseTx != null) {
                                        isUseExist = true;
                                        break;
                                    }

                                    r.UseTx = queryTx["txid"].AsString;
                                    break;
                                }
                            }
                            //只有不重复才写入
                            if (!isUseExist)
                            {
                                database.GetCollection<UTXO>("utxo").ReplaceOne(findB, utxo);
                            }
                        }
                    }
                }
            }

            //更新utxo已处理块高度
            SetSystemCounter("utxo", doBlockIndex);

            DateTime end = DateTime.Now;
            var doTime = (end - start).TotalMilliseconds;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("StorageUTXOData On Block " + doBlockIndex + " in " + doTime + "ms");
            Console.ForegroundColor = ConsoleColor.White;
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

        private static int GetTxMaxBlockindex()
        {
            var client = new MongoClient(mongodbConnStr);
            var database = client.GetDatabase(mongodbDatabase);
            var collection = database.GetCollection<BsonDocument>("tx");

            var sortBson = BsonDocument.Parse("{blockindex:-1}");
            var query = collection.Find(new BsonDocument()).Sort(sortBson).Limit(1).ToList();
            if (query.Count == 0)
            {
                return -1;
            }
            else
            {
                int maxIndex = (int)query[0]["blockindex"];
                return maxIndex;
            }
        }

        private static int GetUTXOblockindex()
        {
            var client = new MongoClient(mongodbConnStr);
            var database = client.GetDatabase(mongodbDatabase);
            var collection = database.GetCollection<BsonDocument>("system_counter");

            var queryBson = BsonDocument.Parse("{counter:'utxo'}");
            var query = collection.Find(queryBson).ToList();
            if (query.Count == 0){ return -1; }
            else
            {
                int maxIndex = (int)query[0]["lastBlockindex"];
                return maxIndex;
            }
        }

        private static void SetSystemCounter(string counter,int lastBlockindex)
        {
            var client = new MongoClient(mongodbConnStr);
            var database = client.GetDatabase(mongodbDatabase);
            var collection = database.GetCollection<BsonDocument>("system_counter");

            var setBson = BsonDocument.Parse("{counter:'" + counter + "',lastBlockindex:" + lastBlockindex + "}");

            var queryBson = BsonDocument.Parse("{counter:'" + counter + "'}");
            var query = collection.Find(queryBson).ToList();
            if (query.Count == 0)
            {
                collection.InsertOne(setBson);
            }
            else {
                collection.ReplaceOne(queryBson,setBson);
            } 
        }

        private static bool IsTxStoraged(string txid)
        {
            var client = new MongoClient(mongodbConnStr);
            var database = client.GetDatabase(mongodbDatabase);
            var collection = database.GetCollection<BsonDocument>("tx");

            var findBson = BsonDocument.Parse("{txid:'" + txid+ "'}");
            var query = collection.Find(findBson).ToList();

            if (query.Count == 0) { return false; }
            else { return true; }
        }

        private static bool IsDataExist(string coll,string key,object value)
        {
            var client = new MongoClient(mongodbConnStr);
            var database = client.GetDatabase(mongodbDatabase);
            var collection = database.GetCollection<BsonDocument>(coll);

            var findBson = BsonDocument.Parse("{" + key + ":'" + value + "'}");
            var query = collection.Find(findBson).ToList();

            if (query.Count == 0) { return false; }
            else { return true; }
        }
    }
}
