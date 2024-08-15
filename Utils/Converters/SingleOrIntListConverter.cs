﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MFATools.Utils.Converters;

public class SingleOrIntListConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(List<string>);
    }

    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        JToken token = JToken.Load(reader);
        if (token.Type == JTokenType.Integer)
        {
            return new List<int> { token.ToObject<int>() };
        }

        return token.ToObject<List<int>>();
    }

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        List<int>? list = value as List<int>;
        if (list?.Count == 1)
        {
            writer.WriteValue(list[0]);
        }
        else
        {
            serializer.Serialize(writer, list);
        }
    }
}