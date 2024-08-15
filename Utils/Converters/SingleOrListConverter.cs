﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MFATools.Utils.Converters;

public class SingleOrListConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(List<string>);
    }

    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        JToken token = JToken.Load(reader);
        if (token.Type == JTokenType.String)
        {
            return new List<string> { token.ToString() };
        }
        return token.ToObject<List<string>>();
    }

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        List<string>? list = value as List<string>;
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
