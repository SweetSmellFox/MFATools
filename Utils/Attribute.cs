using MFATools.Utils.Converters;
using Newtonsoft.Json;

namespace MFATools.Utils;

public class Attribute
{
    public string? Key { get; set; }
    [JsonConverter(typeof(AutoConverter))] public object? Value { get; set; }

    public Attribute(string key, object? value)
    {
        Key = key;
        Value = value;
    }

    public Attribute()
    {
    }
    
    public override string ToString()
    {
        if (Value is List<int> li)
        {
            return $"\"{Key}\" : [{string.Join(",", li)}]";
        }
        else if (Value is List<string> ls)
        {
            return $"\"{Key}\" : [{string.Join(",", ls)}]";
        }
        else if (Value is string s)
        {
            return $"\"{Key}\" : \"{s}\"";
        }
        else
        {
            return $"\"{Key}\" : {Value}";
        }
    }
}