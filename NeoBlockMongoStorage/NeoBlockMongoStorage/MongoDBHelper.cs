using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Driver;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NeoBlockMongoStorage
{
    class MongoDBHelper
    {
        public long GetDataCount(string mongodbConnStr, string mongodbDatabase, string coll, string findson = "{}")
        {
            var client = new MongoClient(mongodbConnStr);
            var database = client.GetDatabase(mongodbDatabase);
            var collection = database.GetCollection<BsonDocument>(coll);

            var txCount = collection.Find(BsonDocument.Parse(findson)).Count();

            client = null;

            return txCount;
        }

        public JArray GetData(string mongodbConnStr, string mongodbDatabase, string coll, string findson)
        {
            var client = new MongoClient(mongodbConnStr);
            var database = client.GetDatabase(mongodbDatabase);
            var collection = database.GetCollection<BsonDocument>(coll);

            List<BsonDocument> query = collection.Find(BsonDocument.Parse(findson)).ToList();
            client = null;

            if (query.Count > 0)
            {
                var jsonWriterSettings = new JsonWriterSettings { OutputMode = JsonOutputMode.Strict };
                JArray JA = JArray.Parse(query.ToJson(jsonWriterSettings));
                foreach (JObject j in JA)
                {
                    j.Remove("_id");
                }
                return JA;
            }
            else { return new JArray(); }
        }
        
        public void PutData(string mongodbConnStr, string mongodbDatabase, string coll, string data, bool isAync = false)
        {
            var client = new MongoClient(mongodbConnStr);
            var database = client.GetDatabase(mongodbDatabase);
            var collection = database.GetCollection<BsonDocument>(coll);

            var bson = BsonDocument.Parse(data);
            if (isAync)
            {
                collection.InsertOneAsync(bson);
            }
            else
            {
                collection.InsertOne(bson);
            }

            collection = null;
        }

        public void PutData(string mongodbConnStr, string mongodbDatabase, string coll, JObject[] data, bool isAync = false)
        {
            var client = new MongoClient(mongodbConnStr);
            var database = client.GetDatabase(mongodbDatabase);
            var collection = database.GetCollection<BsonDocument>(coll);
            
            var bson = data.Select(p => BsonDocument.Parse(p.ToString())).ToList() ;
            if (isAync)
            {
                collection.InsertManyAsync(bson);
            }
            else
            {
                collection.InsertManyAsync(bson);
            }

            collection = null;
        }
    }
}
