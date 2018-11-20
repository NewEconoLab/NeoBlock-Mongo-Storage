using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Driver;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace NeoBlockMongoStorage
{
    public class mongoIndexHelper
    {
        public void initIndex(string mongodbConnStr, string mongodbDatabase)
        {
            //读取index配置文件
            JArray indexConfigJA = JArray.Parse(File.ReadAllText("indexSettings.json"));
            //var a = JsonConvert.SerializeObject(indexConfigJA);
            foreach (var J in indexConfigJA)
            {
                string collName = (string)J["collName"];
                foreach (var indexJ in (JArray)J["indexs"])
                {
                    var indexName = (string)indexJ["indexName"];
                    var indexDefinition = JsonConvert.SerializeObject(indexJ["indexDefinition"]);
                    var isUnique = (bool)indexJ["isUnique"];

                    setIndex(mongodbConnStr, mongodbDatabase, collName, indexDefinition, indexName, isUnique);
                }
            }
        }

        public void setIndex(string mongodbConnStr, string mongodbDatabase, string coll, string indexDefinition, string indexName, bool isUnique = false)
        {
            var client = new MongoClient(mongodbConnStr);
            var database = client.GetDatabase(mongodbDatabase);
            var collection = database.GetCollection<BsonDocument>(coll);

            //检查是否已有设置index
            bool isSet = false;
            using (var cursor = collection.Indexes.List())
            {
                JArray JAindexs = JArray.Parse(cursor.ToList().ToJson());
                var query = JAindexs.Children().Where(index => (string)index["name"] == indexName);
                if (query.Count() > 0) isSet = true;
                // do something with the list...
            }

            if (!isSet)
            {
                try
                {
                    var options = new CreateIndexOptions { Name = indexName, Unique = isUnique };
                    collection.Indexes.CreateOne(indexDefinition, options);
                }
                catch { }
            }

            client = null;
        }

        //public JObject readJsonFile(string filePath)
        //{
        //    //读取json文件  
        //    using (StreamReader sr = new StreamReader(filePath))
        //    {
        //        try
        //        {
        //            JsonSerializer serializer = new JsonSerializer();
        //            serializer.Converters.Add(new JavaScriptDateTimeConverter());
        //            serializer.NullValueHandling = NullValueHandling.Ignore;

        //            //构建Json.net的读取流  
        //            JsonReader reader = new JsonTextReader(sr);
        //            //对读取出的Json.net的reader流进行反序列化，并装载到模型中  
        //            return JObject.Load(reader);

        //        }
        //        catch (Exception ex)
        //        {
        //            ex.Message.ToString();
        //            return new JObject();
        //        }
        //    }
        //}

        //public void writeJsonFile(string filePath,JObject J)
        //{
        //    using (StreamWriter sw = new StreamWriter(filePath))
        //    {
        //        try
        //        {

        //            JsonSerializer serializer = new JsonSerializer();
        //            serializer.Converters.Add(new JavaScriptDateTimeConverter());
        //            serializer.NullValueHandling = NullValueHandling.Ignore;

        //            //构建Json.net的写入流  
        //            JsonWriter writer = new JsonTextWriter(sw);
        //            //把模型数据序列化并写入Json.net的JsonWriter流中  
        //            serializer.Serialize(writer, J);

        //            //清理
        //            writer.Close();
        //            sw.Close();

        //        }
        //        catch (Exception ex)
        //        {
        //            ex.Message.ToString();
        //        }

        //    }
        //}
    }
}
