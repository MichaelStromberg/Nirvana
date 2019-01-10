﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OptimizedCore;
using VariantAnnotation.Interface;
using VariantAnnotation.IO;

namespace SAUtils
{
    public sealed class SaJsonSchema : ISaJsonSchema
    {
        private const string SchemaVersion = "http://json-schema.org/draft-06/schema#";

        public int TotalItems { get; set; }
        private readonly StringBuilder _sb;
        private readonly JsonObject _jsonObject;
        private readonly Dictionary<string, SaJsonKeyAnnotation> _keyAnnotation = new Dictionary<string, SaJsonKeyAnnotation>();
        public List<string> Keys { get; private set; } = new List<string>();
        internal readonly Dictionary<string, int> KeyCounts = new Dictionary<string, int>();
        private Action<JsonObject, List<string>> _jsonStringGenerationAction;
        private bool _finalized;

        internal SaJsonSchema(StringBuilder sb)
        {
            _sb = sb;
            _jsonObject = new JsonObject(sb);
        }

        public static SaJsonSchema Create(StringBuilder sb, string jsonTag, string saDatatype, List<string> jsonKeys)
        {
            var jsonSchema = new SaJsonSchema(sb) {Keys = jsonKeys};
            jsonSchema._jsonObject.StartObject();
            jsonSchema.AddSchemaVersion();
            jsonSchema.AddDefaultType();
            jsonSchema._jsonObject.StartObjectWithKey("properties");
            jsonSchema._jsonObject.StartObjectWithKey(jsonTag);
            jsonSchema._jsonObject.AddStringValue("type", saDatatype);
            jsonSchema._jsonObject.StartObjectWithKey("items");
            jsonSchema._jsonObject.AddStringValue("type", "object");
            jsonSchema._jsonObject.StartObjectWithKey("properties");
            return jsonSchema;
        }


        private void AddSchemaVersion() => _jsonObject.AddStringValue("$schema", SchemaVersion);

        private void AddDefaultType() => _jsonObject.AddStringValue("type", "object");

        public void Count(string key) => KeyCounts[key]++;
        public string GetJsonType(string key) => _keyAnnotation[key].Type;
        private string GetCategory(string key) => _keyAnnotation[key].Category;

        public void AddAnnotation(string key, SaJsonKeyAnnotation annotation)
        {
            _keyAnnotation.Add(key, annotation);
            KeyCounts.Add(key, 0);
        }

        public override string ToString()
        {
            if (!_finalized) FinalizeSchema();
            return _sb.ToString();
        }

        private void FinalizeSchema()
        {
            var requiredKeys = new List<string>();

            foreach (string key in Keys)
            {
                int counts = KeyCounts[key];
                if (counts == 0) continue;
                // boolean is always considered as optional
                if (counts == TotalItems && GetJsonType(key) != "boolean") requiredKeys.Add(key);

                OutputKeyAnnotation(key);
            }

            _jsonObject.EndObject();
            _jsonObject.AddStringValues("required", requiredKeys);
            _jsonObject.EndAllObjects();

            _finalized = true;
        }

        private Action<JsonObject, List<string>> GetJsonStringGenerationAction()
        {
            var actions = new List<Action<JsonObject, string>>();

            foreach (string key in Keys)
            {
                string intendedType = GetJsonType(key);
                switch (intendedType)
                {
                    case "string":
                        if (key == "refAllele" || key == "altAllele")
                            actions.Add((jsonObject, value) => CountKeyIfAdded(jsonObject.AddStringValue(key, VariantAnnotation.Utilities.BaseFormatting.EmptyToDash(value)), key));
                        else
                            actions.Add((jsonObject, value) => CountKeyIfAdded(jsonObject.AddStringValue(key, value), key));
                        break;
                    case "boolean":
                        actions.Add((jsonObject, value) => CountKeyIfAdded(jsonObject.AddBoolValue(key, value == "true"), key));
                        break;
                    case "number":
                        actions.Add((jsonObject, value) =>
                        {
                            string keyCategory = GetCategory(key);
                            CountKeyIfAdded(keyCategory == "AlleleFrequency"
                                ? jsonObject.AddDoubleValue(key, double.Parse(value), "0.######") 
                                : jsonObject.AddStringValue(key, value, false), key);
                        });
                        break;
                    default:
                        throw new Exception($"Unknown data type {intendedType}");
                }
            }

            return (jsonObject, strings) => {
                foreach (var (action, str) in actions.Zip(strings, (a, b) => (a, b)))
                {
                    action(jsonObject, str);
                }

                TotalItems++;
            };
        }


        private void CountKeyIfAdded(bool keyAdded, string key)
        {
            if (keyAdded) Count(key);
        }

        public string GetJsonString(List<string> values)
        {
            if (_jsonStringGenerationAction == null) _jsonStringGenerationAction = GetJsonStringGenerationAction();

            if (values.Count != Keys.Count)
                throw new Exception("Please provide one and only one value for each JSON key.");

            var sb = StringBuilderCache.Acquire();
            var jsonObject = new JsonObject(sb);

            _jsonStringGenerationAction(jsonObject, values);
            
            return StringBuilderCache.GetStringAndRelease(sb);
        }

        internal void OutputKeyAnnotation(string key)
        { 
            _jsonObject.StartObjectWithKey(key);

            foreach (var (annotationKey, annotationValue) in _keyAnnotation[key].GetDefinedAnnotations())
            {
                _jsonObject.AddStringValue(annotationKey, annotationValue);
            }

            _jsonObject.EndObject();
        }
    }
}