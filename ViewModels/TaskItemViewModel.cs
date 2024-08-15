﻿using MFATools.Utils;
using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;

namespace MFATools.ViewModels;

public class TaskItemViewModel : ObservableObject
{
    private string _name = "未命名";

    public string Name
    {
        get => _name;
        set
        {
            if (Task != null)
                Task.name = value;
            SetProperty(ref _name, value);
        }
    }

    private bool _isNew = false;

    public bool IsNew
    {
        get => _isNew;
        set => SetProperty(ref _isNew, value);
    }

    private TaskModel? _task;

    /// <summary>
    /// Gets or sets the time.
    /// </summary>
    public TaskModel? Task
    {
        get => _task;
        set
        {
            if (value != null)
                Name = value.name;
            SetProperty(ref _task, value);
        }
    }

    public override string ToString()
    {
        var settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Ignore
        };
        Dictionary<string, TaskModel> taskModels = new Dictionary<string, TaskModel>();
        if (Task != null)
            taskModels.Add(Name, Task);
        return JsonConvert.SerializeObject(taskModels, settings);
    }
}