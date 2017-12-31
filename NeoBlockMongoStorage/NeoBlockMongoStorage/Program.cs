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
using System.Net;
using System.Text;
using System.IO;
using MongoDB.Bson.IO;

namespace NeoBlockMongoStorage
{
    class Program
    {
        static CoreHttpHelper chh = new CoreHttpHelper();

        static string mongodbConnStr = string.Empty;
        static string mongodbDatabase = string.Empty;
        static string NeoCliJsonRPCUrl = string.Empty;
        static int sleepTime = 0;
        static bool utxoIsSleep = false;

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
            if (int.Parse(config["utxoIsSleep"]) == 1) {
                utxoIsSleep = true;
            }

            Console.WriteLine("NeoBlockMongoStorage Start!");
            Console.WriteLine("*************************************");
            Console.WriteLine("mongodbConnStr" + mongodbConnStr);
            Console.WriteLine("mongodbDatabase" + mongodbDatabase);
            Console.WriteLine("NeoCliJsonRPCUrl" + NeoCliJsonRPCUrl);
            Console.WriteLine("sleepTime" + sleepTime);
            Console.WriteLine("*************************************");

            Console.WriteLine("Block MaxIndex in DB:" + GetBlockMaxIndex());

            //创建任务
            Task task_StorageUTXO = new Task(() => {
                
                Console.WriteLine("异步循环执行StorageUTXOData开始");
                while (true)
                {
                    DateTime start = DateTime.Now;

                    //统计处理UTXO数据
                    StorageUTXOData();

                    if (utxoIsSleep) { Thread.Sleep(sleepTime); }

                    DateTime end = DateTime.Now;
                    var doTime = (end - start).TotalMilliseconds;
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("StorageUTXOData in " + doTime + "ms");
                }
            });
            Task task_StorageNotify = new Task(() => {
                Console.WriteLine("异步循环执行StorageNotifyData开始");
                while (true)
                {
                    //处理notify数据
                    StorageNotifyData();

                    Thread.Sleep(sleepTime);
                }
            });
            Task task_StorageFulllog = new Task(() => {
                Console.WriteLine("异步循环执行StorageFulllogData开始");
                while (true)
                {
                    //处理fulllog数据
                    StorageFulllogData();

                    Thread.Sleep(sleepTime);
                }
            });
            //启动任务
            task_StorageUTXO.Start();
            task_StorageNotify.Start();
            task_StorageFulllog.Start();

            //主进程(同步)
            while (true)
            {
                //处理块数据
                StorageBlockTXData();
                ////处理交易数据
                //StorageTxData(); 交易数据在处理块数据时同时处理

                ////统计处理UTXO数据
                //StorageUTXOData();
                ////处理notify数据
                //StorageNotifyData();
                ////处理fulllog数据
                //StorageFulllogData();

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

        private static void StorageBlockTXData()
        {
            int maxIndex = GetBlockMaxIndex();
            //检查当前已有区块是否已存所有交易
            if (!IsDataExist("tx", "blockindex", maxIndex))
            {
                //已存区块没有存tx则再处理一遍
                DoStorageBlockTXData(maxIndex);
            }

            int storageIndex = maxIndex + 1;
            DoStorageBlockTXData(storageIndex);

            //检查asset，如果有新的就存储
            CheckAssetAndStorage();
        }

        private static void DoStorageBlockTXData(int doIndex)
        {
            DateTime start = DateTime.Now;

            //获取Cli block数据
            string resBlock = GetNeoCliData("getblock", new object[]
                {
                    doIndex,
                    1
                });

            //获取有效数据则存储Mongodb
            if (resBlock != "null")
            {
                //只处理没有存储过的
                if (!IsDataExist("block", "index", doIndex))
                {
                    JObject blockJ = JObject.Parse(resBlock);
                    //去除非块原生数据
                    blockJ.Remove("confirmations");
                    blockJ.Remove("nextblockhash");

                    //存储区块数据
                    MongoInsertOne("block", blockJ);
                }

                //依据区块数据存储交易数据
                DoStorageTxDataByBlock(resBlock);

                DateTime end = DateTime.Now;
                var doTime = (end - start).TotalMilliseconds;
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("StorageBlockTxData On Block " + doIndex + " in " + doTime + "ms");
                Console.ForegroundColor = ConsoleColor.White;
            }
        }

        private static void DoStorageTxDataByBlock(string block)
        {
            JObject blockJ = JObject.Parse(block);
            int blockIndex = (int)blockJ["index"];
            JArray blockTx = (JArray)blockJ["tx"];

            var client = new MongoClient(mongodbConnStr);
            var database = client.GetDatabase(mongodbDatabase);
            var collection = database.GetCollection<BsonDocument>("tx");

            List<BsonDocument> listBson = new List<BsonDocument>();
            foreach (JObject j in blockTx)
            {
                j.Add("blockindex", blockIndex);
                listBson.Add(BsonDocument.Parse(Newtonsoft.Json.JsonConvert.SerializeObject(j)));
            }
            if (listBson.Count > 0 && !IsDataExist("tx", "txid", listBson[0]["txid"].AsString))
            {
                //批量写入块所有交易数据
                collection.InsertMany(listBson);
            }

            client = null;
        }

        private static void CheckAssetAndStorage() {
            DateTime start = DateTime.Now;

            var client = new MongoClient(mongodbConnStr);
            var database = client.GetDatabase(mongodbDatabase);
            var collection = database.GetCollection<BsonDocument>("tx");
            BsonDocument findB = BsonDocument.Parse("{type:'RegisterTransaction'}");
            var query = collection.Find(findB).ToList();
            if (query.Count > 0)
            {
                collection = database.GetCollection<BsonDocument>("asset");

                foreach (var tx in query)
                {
                    string txid = tx["txid"].AsString;
                    //只有asset没有记录才会处理
                    if (!IsDataExist("asset", "id", txid)) {
                        //获取Cli asset数据
                        string resAsset = GetNeoCliData("getassetstate", new object[] { txid });

                        //控制接口调用频度
                        Thread.Sleep(sleepTime);

                        //获取有效数据则存储asset
                        if (resAsset != "null")
                        {
                            MongoInsertOne("asset",JObject.Parse(resAsset));

                            DateTime end = DateTime.Now;
                            var doTime = (end - start).TotalMilliseconds;
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine("StorageAssetData On Tx " + txid + " in " + doTime + "ms");
                        }
                    }
                }
            }
        }

        private static void StorageUTXOData()
        {
            var appName = "utxo";
            var maxBlockindex = GetSystemCounter(appName);
            //检查当前已有区块是否已处理所有交易utxo
            DoStorageByEveryTxInBlock(maxBlockindex, appName);

            var storageBlockindex = maxBlockindex + 1;
            DoStorageByEveryTxInBlock(storageBlockindex, appName);
        }

        private static void StorageFulllogData()
        {
            var appName = "fulllog";
            var maxBlockindex = GetSystemCounter(appName);
            //检查当前已有区块是否已处理所有交易fulllog
            DoStorageByEveryTxInBlock(maxBlockindex, appName);

            var storageBlockindex = maxBlockindex + 1;
            DoStorageByEveryTxInBlock(storageBlockindex, appName);
        }

        private static void DoStorageByEveryTxInBlock(int blockindex,string appName)
        {
            var client = new MongoClient(mongodbConnStr);
            var database = client.GetDatabase(mongodbDatabase);
            var collection = database.GetCollection<BsonDocument>("block");

            var findBson = BsonDocument.Parse("{index:" + blockindex + "}");
            var query = collection.Find(findBson).ToList();
            if (query.Count > 0)
            {
                BsonDocument queryB = query[0].AsBsonDocument;
                BsonArray Txs = queryB["tx"].AsBsonArray;

                var i = 0;
                foreach (BsonValue bv in Txs)
                {
                    DateTime start = DateTime.Now;

                    var jsonWriterSettings = new JsonWriterSettings { OutputMode = JsonOutputMode.Strict };
                    JObject Tx =JObject.Parse(bv.ToJson(jsonWriterSettings));
                    ConsoleColor cc = ConsoleColor.White;
                    bool isShow = true;
                    switch (appName)
                    {
                        case "utxo":
                            DoStorageUTXOByTx(Tx);
                            isShow = false;
                            //cc = ConsoleColor.Yellow;
                            break;
                        case "fulllog":
                            DoStorageFulllogByTx(Tx);
                            cc = ConsoleColor.Magenta;
                            Thread.Sleep(sleepTime);
                            break;
                    }

                    i++;

                    if (isShow == true)
                    {
                        DateTime end = DateTime.Now;
                        var doTime = (end - start).TotalMilliseconds;
                        Console.ForegroundColor = cc;
                        Console.WriteLine("Storage_" + appName + "_Data On Block " + blockindex + " On Tx(" + i + "/" + Txs.Count + ") in " + doTime + "ms");
                    }

                    //Thread.Sleep(sleepTime);
                }

                //更新已处理块高度
                SetSystemCounter(appName, blockindex);
            }

            client = null;
        }

        private static void DoStorageUTXOByTx(JObject TxJ)
        {
            string txid = (string)TxJ["txid"];
            JArray vinJA = (JArray)TxJ["vin"];
            JArray voutJA = (JArray)TxJ["vout"];

            var client = new MongoClient(mongodbConnStr);
            var database = client.GetDatabase(mongodbDatabase);
            var collUTXO = database.GetCollection<UTXO>("utxo");

            //先处理UTXO生成
            if (voutJA.Count > 0)
            {
                foreach (JObject voutJ in voutJA)
                {
                    UTXO utxo = new UTXO
                    {
                        addr = (string)voutJ["address"],
                        txid = txid,
                        n = (int)voutJ["n"],
                        asset = (string)voutJ["asset"],
                        value = (decimal)voutJ["value"]
                    };

                    //检查是否已有入库,无则入库
                    string findStr = "{{txid:'{0}',n:{1}}}";
                    findStr = string.Format(findStr, utxo.txid, utxo.n);
                    BsonDocument findB = BsonDocument.Parse(findStr);
                    List<UTXO> query = collUTXO.Find(findB).ToList();
                    if (query.Count == 0)
                    {
                        collUTXO.InsertOne(utxo);
                    }
                }
            }

            //处理UTXO使用
            if (vinJA.Count > 0)
            {
                foreach (JObject vinJ in vinJA)
                {
                    string voutTx = (string)vinJ["txid"];
                    int voutN = (int)vinJ["vout"];

                    //查找UTXO创建记录
                    string findStr = "{{txid:'{0}',n:{1}}}";
                    findStr = string.Format(findStr, voutTx, voutN);
                    BsonDocument findB = BsonDocument.Parse(findStr);
                    UTXO utxo = collUTXO.Find(findB).ToList()[0];
                    if (utxo != null)
                    {
                        //只有不重复才更新
                        if (utxo.used == string.Empty)
                        {
                            utxo.used = txid;
                            collUTXO.ReplaceOne(findB, utxo);
                        }
                    }
                }
            }

            client = null;   
        }

        private static void DoStorageFulllogByTx(JObject TxJ)
        {
            //获取数据库Tx数据
            string doTxid = (string)TxJ["txid"];

            JObject postData = new JObject();
            postData.Add("jsonrpc", "2.0");
            postData.Add("method", "getfullloginfo");
            postData.Add("params", new JArray() { doTxid });
            postData.Add("id", 1);
            string postDataStr = Newtonsoft.Json.JsonConvert.SerializeObject(postData);
            //获取Cli Notify数据
            string resFulllog = chh.Post(NeoCliJsonRPCUrl, postDataStr, Encoding.UTF8);
            JObject resJ = new JObject();
            try {
                resJ = JObject.Parse(resFulllog);
            }
            catch {
                //待加入异常记录
                return;
            }         
            if (resJ["result"] != null)
            {
                resFulllog = JObject.Parse(resFulllog)["result"].ToString();
            }
            else { resFulllog = null; }
            if (resFulllog != null)
            {
                if (!IsDataExist("fulllog", "txid", doTxid))
                {
                    string fulllog7z = resFulllog;
                    JObject j = new JObject
                    {
                        { "txid", doTxid },
                        { "fulllog7z", fulllog7z }
                    };
                    MongoInsertOne("fulllog", j);
                }
            }
        }

        //private static void StorageTxData()
        //{          
        //    var maxBlockindex = GetTxMaxBlockindex();
        //    //检查当前已有区块是否已存所有交易
        //    DoStorageTxData(maxBlockindex);

        //    var storageBlockindex = maxBlockindex + 1;
        //    DoStorageTxData(storageBlockindex);
        //}

        //private static void DoStorageTxData(int doBlockIndex)
        //{
        //    DateTime start = DateTime.Now;

        //    var client = new MongoClient(mongodbConnStr);
        //    var database = client.GetDatabase(mongodbDatabase);
        //    var collection = database.GetCollection<BsonDocument>("block");

        //    var findBson = BsonDocument.Parse("{index:" + doBlockIndex + "}");
        //    var query = collection.Find(findBson).ToList();
        //    if (query.Count > 0) {
        //        BsonDocument queryB = query[0].AsBsonDocument;

        //        foreach (BsonValue bv in queryB["tx"].AsBsonArray)
        //        {
        //            string storageTxid = (string)bv["txid"];
        //            storageTxid = storageTxid.Substring(2, storageTxid.Length - 2);
        //            //只处理没有存储过的
        //            if (!IsTxStoraged("0x" + storageTxid))
        //            {
        //                //获取Cli TX数据
        //                string resTx = GetNeoCliData("getrawtransaction", new object[]
        //                    {
        //                storageTxid,
        //                1
        //                    });
        //                //获取有效数据则存储Mongodb
        //                if (resTx != "null")
        //                {
        //                    JObject txJ = JObject.Parse(resTx);
        //                    txJ.Remove("confirmations");
        //                    txJ.Add("blockindex", doBlockIndex);

        //                    var txJstr = JsonConvert.SerializeObject(txJ);
        //                    MongoInsert("tx", txJstr);

        //                    DateTime end = DateTime.Now;
        //                    var doTime = (end - start).TotalMilliseconds;
        //                    Console.ForegroundColor = ConsoleColor.Green;
        //                    Console.WriteLine("StorageTxData On Block " + doBlockIndex + " in " + doTime + "ms");
        //                    Console.ForegroundColor = ConsoleColor.White;
        //                }
        //            }
        //        }
        //    }

        //    client = null;
        //}

        private static void StorageNotifyData()
        {
            var maxBlockindex = GetSystemCounter("notify");
            //检查当前已有区块是否已处理所有交易notify
            DoStorageNotify(maxBlockindex);

            var storageBlockindex = maxBlockindex + 1;
            DoStorageNotify(storageBlockindex);
        }

        private static void DoStorageNotify(int doBlockIndex)
        {
            DateTime start = DateTime.Now;

            //处理的notify的块不能大于区块高度
            if (doBlockIndex <= GetBlockMaxIndex())
            {
                JObject postData = new JObject();
                postData.Add("jsonrpc", "2.0");
                postData.Add("method", "getnotifyinfo");
                postData.Add("params", new JArray() { doBlockIndex });
                postData.Add("id", 1);
                string postDataStr = Newtonsoft.Json.JsonConvert.SerializeObject(postData);
                //获取Cli Notify数据
                string resNotify = chh.Post(NeoCliJsonRPCUrl, postDataStr, Encoding.UTF8);
                JObject resJ = new JObject();
                try
                {
                    resJ = JObject.Parse(resNotify);
                }
                catch
                {
                    //待加入异常记录
                    return;
                }
                resNotify = Newtonsoft.Json.JsonConvert.SerializeObject(resJ["result"]);
                //GetNeoCliData("getnotifyinfo", new object[]
                //{
                //    doBlockIndex
                //});
                //获取有效数据则存储Mongodb
                if (resNotify != "null")
                {
                    JArray txJA = JArray.Parse(resNotify);
                    long blocktime = (long)txJA[0]["time"];
                    List<JObject> listJ = new List<JObject>();
                    foreach (JToken jk in txJA)
                    {
                        var isListBexist = false;//判断是否已存在txid
                                                 //如果已有txid则添加
                        if (listJ.Count > 0)
                        {
                            foreach (JObject j in listJ)
                            {
                                if ((string)j["txid"] == (string)jk["txid"])
                                {
                                    JObject statesJ = new JObject();
                                    if ((string)jk["state"]["type"] == "Array")
                                    {
                                        statesJ = new JObject
                                    {
                                        { "contract",(string)jk["contract"]},
                                        { "type",(string)jk["state"]["type"]},
                                        { "values",(JArray)jk["state"]["value"] }
                                    };
                                    }
                                    else
                                    {
                                        statesJ = new JObject
                                    {
                                        { "contract",(string)jk["contract"]},
                                        { "type",(string)jk["state"]["type"]},
                                        { "values",(string)jk["state"]["value"] }
                                    };
                                    }

                                    JArray statesJA = (JArray)j["states"];
                                    statesJA.Add(statesJ);

                                    isListBexist = true;
                                    break;
                                }
                            }
                        }
                        //如果没有txid则创建
                        if (listJ.Count == 0 || isListBexist == false)
                        {
                            JObject j = new JObject();
                            if ((string)jk["state"]["type"] == "Array")
                            {
                                j = new JObject
                            {
                                { "txid", (string)jk["txid"] },
                                { "blocktime",blocktime},
                                { "states",new JArray{new JObject{
                                    { "contract",(string)jk["contract"]},
                                    { "type",(string)jk["state"]["type"]},
                                    { "values",(JArray)jk["state"]["value"] }
                                }
                                } }
                            };
                            }
                            else
                            {
                                j = new JObject
                            {
                                { "txid", (string)jk["txid"] },
                                { "blocktime",blocktime},
                                { "states",new JArray{new JObject{
                                    { "contract",(string)jk["contract"]},
                                    { "type",(string)jk["state"]["type"]},
                                    { "values",(string)jk["state"]["value"] }
                                }
                                } }
                            };
                            }


                            listJ.Add(j);
                        }
                    }

                    //每个txid逐一处理，存入数据库
                    foreach (JObject notifyJ in listJ)
                    {
                        if (!IsDataExist("notify", "txid", (string)notifyJ["txid"]))
                        {//判断是否重复
                            MongoInsertOne("notify", notifyJ);
                        }
                    }
                }
                //更新最新处理区块索引
                SetSystemCounter("notify", doBlockIndex);

                DateTime end = DateTime.Now;
                var doTime = (end - start).TotalMilliseconds;
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("StorageNotifyData On Block " + doBlockIndex + " in " + doTime + "ms");
            }
        }

        //private static void DoStorageFulllog(int doBlockIndex)
        //{
        //    DateTime start = DateTime.Now;

        //    var client = new MongoClient(mongodbConnStr);
        //    var database = client.GetDatabase(mongodbDatabase);
        //    var collection = database.GetCollection<BsonDocument>("block");

        //    var findBson = BsonDocument.Parse("{index:" + doBlockIndex + "}");
        //    var query = collection.Find(findBson).ToList();
        //    if (query.Count > 0)
        //    {
        //        BsonDocument queryB = query[0].AsBsonDocument;

        //        foreach (BsonValue bv in queryB["tx"].AsBsonArray)
        //        {
        //            //获取数据库Tx数据
        //            string doTxid = (string)bv["txid"];

        //            JObject postData = new JObject();
        //            postData.Add("jsonrpc", "2.0");
        //            postData.Add("method", "getfullloginfo");
        //            postData.Add("params", new JArray() { doTxid });
        //            postData.Add("id", 1);
        //            string postDataStr = Newtonsoft.Json.JsonConvert.SerializeObject(postData);
        //            //获取Cli Notify数据
        //            string resFulllog = chh.Post(NeoCliJsonRPCUrl, postDataStr,Encoding.UTF8);
        //            if (JObject.Parse(resFulllog)["result"] != null)
        //            {
        //                resFulllog = JObject.Parse(resFulllog)["result"].ToString();
        //            }
        //            else { resFulllog = null; }
        //            if (resFulllog != null)
        //            {
        //                if (!IsDataExist("fulllog", "txid", doTxid))
        //                {
        //                    string fulllog7z = resFulllog;
        //                    JObject j = new JObject
        //                    {
        //                        { "txid", doTxid },
        //                        { "fulllog7z", fulllog7z }
        //                    };
        //                    MongoInsertOne("fulllog", j);
        //                }
        //            }
        //        }

        //        DateTime end = DateTime.Now;
        //        var doTime = (end - start).TotalMilliseconds;
        //        Console.ForegroundColor = ConsoleColor.Magenta;
        //        Console.WriteLine("StorageFulllogData On Block " + doBlockIndex + " in " + doTime + "ms");
        //        Console.ForegroundColor = ConsoleColor.White;

        //        //更新最新处理区块索引
        //        SetSystemCounter("fulllog", doBlockIndex);
        //    }

        //    client = null;
        //}

        //private static void DoStorageUTXO(int doBlockIndex)
        //{
        //    DateTime start = DateTime.Now;

        //    var client = new MongoClient(mongodbConnStr);
        //    var database = client.GetDatabase(mongodbDatabase);
        //    var collection = database.GetCollection<BsonDocument>("block");

        //    var findBson = BsonDocument.Parse("{index:" + doBlockIndex + "}");
        //    var query = collection.Find(findBson).ToList();
        //    if (query.Count > 0)
        //    {
        //        BsonDocument queryB = query[0].AsBsonDocument;

        //        foreach (BsonValue bv in queryB["tx"].AsBsonArray)
        //        {
        //            //获取数据库Tx数据
        //            string doTxid = (string)bv["txid"];
        //            collection = database.GetCollection<BsonDocument>("tx");
        //            var queryTx = collection.Find(BsonDocument.Parse("{txid:'" + doTxid + "'}")).ToList()[0];

        //            BsonArray vinBA = queryTx["vin"].AsBsonArray;
        //            BsonArray voutBA = queryTx["vout"].AsBsonArray;

        //            var collUTXO = database.GetCollection<UTXO>("utxo");
        //            //先处理UTXO生成
        //            if (voutBA.Count > 0)
        //            {
        //                foreach (BsonValue voutBV in voutBA)
        //                {
        //                    UTXO utxo = new UTXO();

        //                    string Addr = voutBV["address"].AsString;
        //                    bool isUTXOexist = IsDataExist("utxo", "Addr", Addr);
        //                    BsonDocument findB = BsonDocument.Parse("{Addr:'" + Addr + "'}");
        //                    if (isUTXOexist)//已有UTXO记录则更新
        //                    {
        //                        //获取已有UTXO记录                               
        //                        utxo = collUTXO.Find(findB).ToList()[0];
        //                    }
        //                    else
        //                    {
        //                        utxo.Addr = Addr;
        //                    }
        //                    //更新最后区块索引
        //                    utxo.LastBlockindex = doBlockIndex;
        //                    //添加本vout数据
        //                    UTXOrecord utxoR = new UTXOrecord
        //                    {
        //                        GetTx = doTxid,
        //                        N = voutBV["n"].AsInt32,
        //                        Asset = voutBV["asset"].AsString,
        //                        Value = decimal.Parse(voutBV["value"].AsString)
        //                    };
        //                    //检查当前UTXO记录是否已被记录过
        //                    bool isUTXOexist_R = false;
        //                    if (utxo.UTXOrecord != null)
        //                    {
        //                        foreach (var r in utxo.UTXOrecord)
        //                        {
        //                            if (r.GetTx == utxoR.GetTx && r.N == utxoR.N)
        //                            {
        //                                isUTXOexist_R = true;
        //                                break;
        //                            }
        //                        }
        //                    }
        //                    else { utxo.UTXOrecord = new List<UTXOrecord>(); }
        //                    //只有不重复才会添加本vout数据并更新或插入
        //                    if (!isUTXOexist_R)
        //                    {
        //                        utxo.UTXOrecord.Add(utxoR);

        //                        if (isUTXOexist)
        //                        {
        //                            //已存在则更新
        //                            collUTXO.ReplaceOne(findB, utxo);
        //                        }
        //                        else
        //                        {
        //                            //不存在则插入
        //                            collUTXO.InsertOne(utxo);
        //                        }
        //                    }
        //                }
        //            }

        //            //处理UTXO使用
        //            if (vinBA.Count > 0)
        //            {
        //                foreach (BsonValue vinBV in vinBA)
        //                {
        //                    string vinTxid = vinBV["txid"].AsString;
        //                    int vinN = vinBV["vout"].AsInt32;

        //                    BsonDocument queryUTXOaddr = database.GetCollection<BsonDocument>("tx").Find(BsonDocument.Parse("{txid:'" + vinTxid + "'}")).ToList()[0];
        //                    BsonArray queryUTXOaddr_vout = queryUTXOaddr["vout"].AsBsonArray;
        //                    string Addr = string.Empty;
        //                    foreach (BsonValue voutBV in queryUTXOaddr_vout)
        //                    {
        //                        if (voutBV["n"] == vinN)
        //                        {
        //                            Addr = voutBV["address"].AsString;
        //                            break;
        //                        }
        //                    }

        //                    BsonDocument findB = BsonDocument.Parse("{Addr:'" + Addr + "'}");
        //                    UTXO utxo = database.GetCollection<UTXO>("utxo").Find(findB).ToList()[0];
        //                    bool isUseExist = false;//判断是否已被写过use
        //                    foreach (var r in utxo.UTXOrecord)
        //                    {
        //                        if (r.GetTx == vinTxid && r.N == vinN)
        //                        {
        //                            if (r.UseTx != null)
        //                            {
        //                                isUseExist = true;
        //                                break;
        //                            }

        //                            r.UseTx = queryTx["txid"].AsString;
        //                            break;
        //                        }
        //                    }
        //                    //只有不重复才写入
        //                    if (!isUseExist)
        //                    {
        //                        database.GetCollection<UTXO>("utxo").ReplaceOne(findB, utxo);
        //                    }
        //                }
        //            }
        //        }

        //        //更新utxo已处理块高度
        //        SetSystemCounter("utxo", doBlockIndex);

        //        DateTime end = DateTime.Now;
        //        var doTime = (end - start).TotalMilliseconds;
        //        Console.ForegroundColor = ConsoleColor.Yellow;
        //        Console.WriteLine("StorageUTXOData On Block " + doBlockIndex + " in " + doTime + "ms");
        //        Console.ForegroundColor = ConsoleColor.White;

        //        client = null;
        //    }
        //}

        private static string GetNeoCliData(string method, object[] paras)
        {
            Uri rpcEndpoint = new Uri(NeoCliJsonRPCUrl);
            JsonRpcWebClient rpc = new JsonRpcWebClient(rpcEndpoint);

            var response = rpc.InvokeAsync<JObject>(method, paras);
            JObject resJ = JObject.Parse(Newtonsoft.Json.JsonConvert.SerializeObject(response.Result));
            var resStr = Newtonsoft.Json.JsonConvert.SerializeObject(resJ["result"]);

            return resStr;
        }

        private static void MongoInsertOne(string collName, JObject J,bool isAsyn = false)
        {
            var client = new MongoClient(mongodbConnStr);
            var database = client.GetDatabase(mongodbDatabase);
            var collection = database.GetCollection<BsonDocument>(collName);

            var document = BsonDocument.Parse(Newtonsoft.Json.JsonConvert.SerializeObject(J));

            if (isAsyn)
            {
                collection.InsertOneAsync(document);
            }
            else
            {
                collection.InsertOne(document);
            }      

            client = null;
        }

        private static int GetBlockMaxIndex()
        {
            int maxIndex = -1;
            var client = new MongoClient(mongodbConnStr);
            var database = client.GetDatabase(mongodbDatabase);
            var collection = database.GetCollection<BsonDocument>("block");

            var sortBson = BsonDocument.Parse("{index:-1}");
            var query = collection.Find(new BsonDocument()).Sort(sortBson).Limit(1).ToList();
            if (query.Count == 0)
            {
                maxIndex = -1;
            }
            else
            {
                maxIndex = (int)query[0]["index"];
            }

            client = null;
            return maxIndex;
        }

        //private static bool IsBlockStoraged(int blockIndex)
        //{
        //    var client = new MongoClient(mongodbConnStr);
        //    var database = client.GetDatabase(mongodbDatabase);
        //    var collection = database.GetCollection<BsonDocument>("block");

        //    var findBson = BsonDocument.Parse("{index:" + blockIndex + "}");
        //    var query = collection.Find(findBson).ToList();

        //    int n = query.Count;

        //    client = null;

        //    if (n == 0) { return false; }
        //    else { return true; }
        //}

        //private static int GetTxMaxBlockindex()
        //{
        //    int maxIndex = -1;
        //    var client = new MongoClient(mongodbConnStr);
        //    var database = client.GetDatabase(mongodbDatabase);
        //    var collection = database.GetCollection<BsonDocument>("tx");

        //    var sortBson = BsonDocument.Parse("{blockindex:-1}");
        //    var query = collection.Find(new BsonDocument()).Sort(sortBson).Limit(1).ToList();
        //    if (query.Count == 0)
        //    {
        //        maxIndex = -1;
        //    }
        //    else
        //    {
        //        maxIndex = (int)query[0]["blockindex"];
        //    }

        //    client = null;
        //    return maxIndex;
        //}

        private static int GetSystemCounter(string counter)
        {
            int maxIndex = -1;
            var client = new MongoClient(mongodbConnStr);
            var database = client.GetDatabase(mongodbDatabase);
            var collection = database.GetCollection<BsonDocument>("system_counter");

            var queryBson = BsonDocument.Parse("{counter:'" + counter + "'}");
            var query = collection.Find(queryBson).ToList();
            if (query.Count == 0) { maxIndex = -1; }
            else
            {
                maxIndex = (int)query[0]["lastBlockindex"];
            }

            client = null;
            return maxIndex;
        }

        private static void SetSystemCounter(string counter, int lastBlockindex)
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
            else
            {
                collection.ReplaceOne(queryBson, setBson);
            }

            client = null;
        }

        //private static bool IsTxStoraged(string txid)
        //{
        //    var client = new MongoClient(mongodbConnStr);
        //    var database = client.GetDatabase(mongodbDatabase);
        //    var collection = database.GetCollection<BsonDocument>("tx");

        //    var findBson = BsonDocument.Parse("{txid:'" + txid+ "'}");
        //    var query = collection.Find(findBson).ToList();

        //    int n = query.Count;

        //    client = null;

        //    if (n == 0) { return false; }
        //    else { return true; }
        //}

        private static bool IsDataExist(string coll, string key, object value)
        {
            var client = new MongoClient(mongodbConnStr);
            var database = client.GetDatabase(mongodbDatabase);
            var collection = database.GetCollection<BsonDocument>(coll);

            BsonDocument findBson = new BsonDocument();
            if (value.GetType() == typeof(string))
            {
                findBson = BsonDocument.Parse("{" + key + ":'" + value + "'}");
            }
            else
            {
                findBson = BsonDocument.Parse("{" + key + ":" + value + "}");
            }

            var query = collection.Find(findBson).ToList();

            int n = query.Count;

            client = null;

            if (n == 0) { return false; }
            else { return true; }
        }

        ///// <summary>  
        ///// 指定Post地址使用Get 方式获取全部字符串  
        ///// </summary>  
        ///// <param name="url">请求后台地址</param>  
        ///// <param name="content">Post提交数据内容(utf-8编码的)</param>  
        ///// <returns></returns>  
        //public static string Post(string url, string content)
        //{
        //    string result = "";
        //    HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
        //    req.Method = "POST";
        //    req.ContentType = "application/x-www-form-urlencoded";

        //    #region 添加Post 参数  
        //    byte[] data = Encoding.UTF8.GetBytes(content);
        //    req.ContentLength = data.Length;
        //    using (Stream reqStream = req.GetRequestStream())
        //    {
        //        reqStream.Write(data, 0, data.Length);
        //        reqStream.Close();
        //    }
        //    #endregion

        //    HttpWebResponse resp = (HttpWebResponse)req.GetResponse();
        //    Stream stream = resp.GetResponseStream();
        //    //获取响应内容  
        //    using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
        //    {
        //        result = reader.ReadToEnd();
        //    }
        //    return result;
        //}
    }
}
