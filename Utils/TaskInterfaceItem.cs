﻿using MFATools.Utils.Converters;
using Newtonsoft.Json;

namespace MFATools.Utils;

public class TaskInterfaceItem
{
    public string? name;
    public string? entry;
    public bool? check;
    public bool? repeatable;
    public int? repeat_count;
    [JsonConverter(typeof(MaaInterfaceSelectOptionConverter))]
    public List<MaaInterface.MaaInterfaceSelectOption>? option;

    public Dictionary<string, TaskModel>? param;

    public override string ToString()
    {
        var settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Ignore
        };

        return JsonConvert.SerializeObject(this, settings);
    }    
    
    /// <summary>
    /// Creates a deep copy of the current <see cref="TaskInterfaceItem"/> instance.
    /// </summary>
    /// <returns>A new <see cref="TaskInterfaceItem"/> instance that is a deep copy of the current instance.</returns>
    public TaskInterfaceItem Clone()
    {
        return JsonConvert.DeserializeObject<TaskInterfaceItem>(ToString()) ?? new TaskInterfaceItem();
    }
}