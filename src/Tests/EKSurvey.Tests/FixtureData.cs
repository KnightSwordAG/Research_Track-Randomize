﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EKSurvey.Tests
{
    public class FixtureData<T> : FixtureData, IEnumerable<T>
    {
        private readonly Lazy<IList<T>> _collection;

        public IEnumerator<T> GetEnumerator() => _collection.Value.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        protected FixtureData(string dataPath, params JsonConverter[] jsonConverters) : base(dataPath, jsonConverters)
        {
            _collection = new Lazy<IList<T>>(() => Load(JsonArray));
        }

        public FixtureData<T> Callback(Action<IEnumerable<T>> callback)
        {
            callback.Invoke(_collection.Value);
            return this;
        }

        private static IList<T> Load(JToken jsonObject)
        {
            return jsonObject?.ToObject<IList<T>>();
        }

        public static FixtureData<T> Load(string dataPath, params JsonConverter[] jsonConverters)
        {
            var fixtureData = File.Exists(dataPath)
                ? new FixtureData<T>(dataPath, jsonConverters)
                : null;
           
            return fixtureData;
        }
    }

    public class FixtureData
    {
        private readonly string _fileDataRaw;
        private readonly Lazy<JArray> _array;

        public JArray JsonArray => _array.Value;

        public FixtureData(string dataPath, params JsonConverter[] jsonConverters)
        {
            _array = new Lazy<JArray>(() => LoadJson(_fileDataRaw, jsonConverters));
            using (var reader = new StreamReader(dataPath))
            {
                _fileDataRaw = reader.ReadToEnd();
            }
        }

        private static JArray LoadJson(string jsonData, params JsonConverter[] jsonConverters)
        {
            return jsonConverters != null && jsonConverters.Any()
                ? JsonConvert.DeserializeObject<JArray>(jsonData, jsonConverters) 
                : JsonConvert.DeserializeObject<JArray>(jsonData);
        }
    }
}
