using MaaFramework.Binding;
using Newtonsoft.Json;

namespace MFATools.Utils.Converters;

public class AdbInputMethodsConverter : JsonConverter<AdbInputMethods>
{
    public override AdbInputMethods ReadJson(JsonReader reader, Type objectType, AdbInputMethods existingValue,
        bool hasExistingValue, JsonSerializer serializer)
    {
        ulong value = serializer.Deserialize<ulong>(reader);
        if (Enum.IsDefined(typeof(AdbInputMethods), value))
        {
            return (AdbInputMethods)value;
        }

        throw new ArgumentException($"Invalid value for AdbInputMethods: {value}");
    }

    public override void WriteJson(JsonWriter writer, AdbInputMethods value, JsonSerializer serializer)
    {
        serializer.Serialize(writer, (ulong)value);
    }
}