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

    static string ConvertListToString(List<List<int>> listOfLists)
    {
        var formattedLists = listOfLists
            .Select(innerList => $"[{string.Join(",", innerList)}]");
        return string.Join(",", formattedLists);
    }

    public override string ToString()
    {
        if (Value is List<List<int>> lli)
            return $"\"{Key}\" : [{ConvertListToString(lli)}]";

        if (Value is List<int> li)
            return $"\"{Key}\" : [{string.Join(",", li)}]";

        if (Value is List<string> ls)
            return $"\"{Key}\" : [{string.Join(",", ls)}]";

        if (Value is string s)
            return $"\"{Key}\" : \"{s}\"";

        return $"\"{Key}\" : {Value}";
    }
    
    public string GetKey()
    {
        return Key;
    }
    
    public string GetValue()
    {
        if (Value is List<List<int>> lli)
            return $"{ConvertListToString(lli)}";

        if (Value is List<int> li)
            return $"{string.Join(",", li)}";

        if (Value is List<string> ls)
            return $"{string.Join(",", ls)}";

        if (Value is string s)
            return s;

        return $"{Value}";
    }

    public static bool operator ==(Attribute a1, Attribute a2)
    {
        return a1.Key.Equals(a2.Key) && a1.Value == a2.Value;
    }

    public static bool operator !=(Attribute a1, Attribute a2)
    {
        return !a1.Key.Equals(a2.Key) || a1.Value != a2.Value;
    }
}