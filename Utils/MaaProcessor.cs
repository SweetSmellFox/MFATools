using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HandyControl.Controls;
using HandyControl.Tools.Extension;
using MaaFramework.Binding;
using MaaFramework.Binding.Buffers;
using MaaFramework.Binding.Messages;
using MFATools.Data;
using MFATools.ViewModels;
using MFATools.Views;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MFATools.Utils;

public class MaaProcessor
{
    private static MaaProcessor? _instance;
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _isStopped;
    private MaaTasker? _currentTasker;

    public static string Resource => AppDomain.CurrentDomain.BaseDirectory + "Resource";
    public static string ModelResource => $"{Resource}/model/";
    public static string ResourceBase => $"{Resource}/base";
    public static string ResourcePipelineFilePath => $"{ResourceBase}/pipeline/";

    public Queue<TaskAndParam> TaskQueue { get; } = new();
    public static int Money { get; set; }
    public static int AllMoney { get; set; }
    public static Config Config { get; } = new();
    public static List<string>? CurrentResources { get; set; }
    public static AutoInitDictionary AutoInitDictionary { get; } = new();

    public event EventHandler? TaskStackChanged;

    public static MaaProcessor Instance
    {
        get => _instance ??= new MaaProcessor();
        set => _instance = value;
    }

    public class TaskAndParam
    {
        public string? Name { get; set; }
        public string? Entry { get; set; }
        public int? Count { get; set; }
        public string? Param { get; set; }
    }

    public void Start(List<DragItemViewModel>? tasks)
    {
        if (!Config.IsConnected)
        {
            Growls.Warning("Warning_CannotConnect".GetLocalizationString()
                .FormatWith((MainWindow.Data?.IsAdb).IsTrue()
                    ? "Simulator".GetLocalizationString()
                    : "Window".GetLocalizationString()));
            return;
        }

        tasks ??= new List<DragItemViewModel>();
        var taskAndParams = tasks.Select(CreateTaskAndParam).ToList();

        foreach (var task in taskAndParams)
            TaskQueue.Enqueue(task);
        OnTaskQueueChanged();

        SetCurrentTasker();
        if (MainWindow.Data != null)
            MainWindow.Data.Idle = false;

        _cancellationTokenSource = new CancellationTokenSource();
        var token = _cancellationTokenSource.Token;

        TaskManager.RunTaskAsync(async () =>
        {
            MainWindow.Data?.AddLogByKey("ConnectingTo", null, (MainWindow.Data?.IsAdb).IsTrue()
                ? "Simulator"
                : "Window");
            var instance = await Task.Run(GetCurrentTasker, token);

            if (instance == null || !instance.Initialized)
            {
                Growls.ErrorGlobal("InitInstanceFailed".GetLocalizationString());
                LoggerService.LogWarning("InitControllerFailed".GetLocalizationString());
                MainWindow.Data?.AddLogByKey("InstanceInitFailedLog");
                Stop(false);
                return;
            }

            bool run = await ExecuteTasks(token);
            if (run)
                Stop(_isStopped);
        }, null, "启动任务");
    }

    public void Stop(bool setIsStopped = true)
    {
        if (_cancellationTokenSource != null)
        {
            _isStopped = setIsStopped;
            _cancellationTokenSource?.Cancel();
            TaskManager.RunTaskAsync(() =>
            {
                MainWindow.Data?.AddLogByKey("Stopping");
                if (_currentTasker == null || (_currentTasker?.Abort()).IsTrue())
                {
                    DisplayTaskCompletionMessage();
                    if (MainWindow.Data != null)
                        MainWindow.Data.Idle = true;
                }
                else
                {
                    Growls.ErrorGlobal("StoppingFailed".GetLocalizationString());
                }
            }, null, "停止任务");
            TaskQueue.Clear();
            OnTaskQueueChanged();
            _cancellationTokenSource = null;
        }
        else
        {
            if (setIsStopped)
            {
                Growls.Warning("NoTaskToStop".GetLocalizationString());
                TaskQueue.Clear();
                OnTaskQueueChanged();
            }
        }
    }

    private TaskAndParam CreateTaskAndParam(DragItemViewModel task)
    {
        var taskModels = task.InterfaceItem?.PipelineOverride ?? new Dictionary<string, TaskModel>();

        UpdateTaskDictionary(ref taskModels, task.InterfaceItem?.Option);

        var taskParams = SerializeTaskParams(taskModels);

        return new TaskAndParam
        {
            Name = task.InterfaceItem?.Name,
            Entry = task.InterfaceItem?.Entry,
            Count = task.InterfaceItem?.Repeatable == true ? (task.InterfaceItem?.RepeatCount ?? 1) : 1,
            Param = taskParams
        };
    }

    private void UpdateTaskDictionary(ref Dictionary<string, TaskModel> taskModels,
        List<MaaInterface.MaaInterfaceSelectOption>? options)
    {
        if (MainWindow.Instance?.TaskDictionary != null)
            MainWindow.Instance.TaskDictionary = MainWindow.Instance.TaskDictionary.MergeTaskModels(taskModels);

        if (options == null) return;

        foreach (var selectOption in options)
        {
            if (MaaInterface.Instance?.Option?.TryGetValue(selectOption.Name ?? string.Empty,
                    out var interfaceOption) ==
                true &&
                MainWindow.Instance != null &&
                selectOption.Index is int index &&
                interfaceOption.Cases is { } cases &&
                cases[index]?.PipelineOverride != null)
            {
                var param = interfaceOption.Cases[selectOption.Index.Value].PipelineOverride;
                MainWindow.Instance.TaskDictionary = MainWindow.Instance.TaskDictionary.MergeTaskModels(param);
                taskModels = taskModels.MergeTaskModels(param);
            }
        }
    }

    private string SerializeTaskParams(Dictionary<string, TaskModel> taskModels)
    {
        var settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Ignore
        };

        try
        {
            return JsonConvert.SerializeObject(taskModels, settings);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return "{}";
        }
    }
    
    
    private async Task<bool> ExecuteTasks(CancellationToken token)
    {
        while (TaskQueue.Count > 0)
        {
            if (token.IsCancellationRequested) return false;

            var task = TaskQueue.Peek();
            for (int i = 0; i < task.Count; i++)
            {
                if (TaskQueue.Count > 0)
                {
                    var taskA = TaskQueue.Peek();
                    MainWindow.Data?.AddLogByKey("TaskStart", null, taskA.Name ?? string.Empty);
                    if (!TryRunTasks(_currentTasker, taskA.Entry, taskA.Param))
                    {
                        if (_isStopped) return false;
                        break;
                    }
                }
            }

            if (TaskQueue.Count > 0)
                TaskQueue.Dequeue();
            OnTaskQueueChanged();
        }

        return true;
    }

    private void DisplayTaskCompletionMessage()
    {
        if (_isStopped)
        {
            Growl.Info("TaskStopped".GetLocalizationString());
            MainWindow.Data?.AddLogByKey("TaskAbandoned");
        }
        else
        {
            Growl.Info("TaskCompleted".GetLocalizationString());
            MainWindow.Data?.AddLogByKey("TaskAllCompleted");
        }
    }

    protected virtual void OnTaskQueueChanged()
    {
        TaskStackChanged?.Invoke(this, EventArgs.Empty);
    }

    public MaaTasker? GetCurrentTasker()
    {
        return _currentTasker ??= InitializeMaaTasker();
    }

    public void SetCurrentTasker(MaaTasker? tasker = null)
    {
        _currentTasker = tasker;
    }

    public static string HandleStringsWithVariables(string content)
    {
        try
        {
            return Regex.Replace(content, @"\{(\+\+|\-\-)?(\w+)(\+\+|\-\-)?([\+\-\*/]\w+)?\}", match =>
            {
                var prefix = match.Groups[1].Value;
                var counterKey = match.Groups[2].Value;
                var suffix = match.Groups[3].Value;
                var operation = match.Groups[4].Value;

                int value = AutoInitDictionary.GetValueOrDefault(counterKey, 0);

                // 前置操作符7
                if (prefix == "++")
                {
                    value = ++AutoInitDictionary[counterKey];
                }
                else if (prefix == "--")
                {
                    value = --AutoInitDictionary[counterKey];
                }

                // 后置操作符
                if (suffix == "++")
                {
                    value = AutoInitDictionary[counterKey]++;
                }
                else if (suffix == "--")
                {
                    value = AutoInitDictionary[counterKey]--;
                }

                // 算术操作
                if (!string.IsNullOrEmpty(operation))
                {
                    string operationType = operation[0].ToString();
                    string operandKey = operation.Substring(1);

                    if (AutoInitDictionary.TryGetValue(operandKey, out var operandValue))
                    {
                        value = operationType switch
                        {
                            "+" => value + operandValue,
                            "-" => value - operandValue,
                            "*" => value * operandValue,
                            "/" => value / operandValue,
                            _ => value
                        };
                    }
                }

                return value.ToString();
            });
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            ErrorView.ShowException(e);
            return content;
        }
    }

    private MaaTasker? InitializeMaaTasker()
    {
        AutoInitDictionary.Clear();

        LoggerService.LogInfo("LoadingResources".GetLocalizationString());
        MaaResource maaResource;
        try
        {
            maaResource = new MaaResource(CurrentResources ?? Array.Empty<string>().ToList());
        }
        catch (Exception e)
        {
            HandleInitializationError(e, "LoadResourcesFailed".GetLocalizationString());
            return null;
        }

        LoggerService.LogInfo("Resources initialized successfully".GetLocalizationString());
        LoggerService.LogInfo("LoadingController".GetLocalizationString());
        MaaController controller;
        try
        {
            controller = InitializeController();
        }
        catch (Exception e)
        {
            HandleInitializationError(e,
                "ConnectingSimulatorOrWindow".GetLocalizationString()
                    .FormatWith((MainWindow.Data?.IsAdb).IsTrue()
                        ? "Simulator".GetLocalizationString()
                        : "Window".GetLocalizationString()), true,
                "InitControllerFailed".GetLocalizationString());
            return null;
        }

        LoggerService.LogInfo("InitControllerSuccess".GetLocalizationString());


        try
        {
            var tasker = new MaaTasker
            {
                Controller = controller,
                Resource = maaResource,
                DisposeOptions = DisposeOptions.All,
            };
            RegisterCustomRecognitionsAndActions(tasker);

            return tasker;
        }
        catch (Exception e)
        {
            LoggerService.LogError(e);
            return null;
        }
    }

    private MaaController InitializeController()
    {
        return (MainWindow.Data?.IsAdb).IsTrue()
            ? new MaaAdbController(
                Config.AdbDevice.AdbPath,
                Config.AdbDevice.AdbSerial,
                Config.AdbDevice.ScreenCap, Config.AdbDevice.Input,
                !string.IsNullOrWhiteSpace(Config.AdbDevice.Config) ? Config.AdbDevice.Config : "{}")
            : new MaaWin32Controller(
                Config.DesktopWindow.HWnd,
                Config.DesktopWindow.ScreenCap, Config.DesktopWindow.Input,
                Config.DesktopWindow.Link,
                Config.DesktopWindow.Check);
    }

    private void RegisterCustomRecognitionsAndActions(MaaTasker _)
    {
        
    }
    
    private void HandleInitializationError(Exception e, string message, bool hasWarning = false,
        string waringMessage = "")
    {
        Console.WriteLine(e);
        TaskQueue.Clear();
        OnTaskQueueChanged();
        if (MainWindow.Data != null)
            MainWindow.Data.Idle = true;
        Growls.ErrorGlobal(message);
        if (hasWarning)
            LoggerService.LogWarning(waringMessage);
        LoggerService.LogError(e.ToString());
    }

    public BitmapImage? GetBitmapImage()
    {
        using var buffer = GetImage(GetCurrentTasker()?.Controller);
        if (buffer == null) return null;

        var encodedDataHandle = buffer.GetEncodedData(out var size);
        if (encodedDataHandle == IntPtr.Zero)
        {
            Growls.ErrorGlobal("Handle为空！");
            return null;
        }

        var imageData = new byte[size];
        Marshal.Copy(encodedDataHandle, imageData, 0, (int)size);

        if (imageData.Length == 0)
            return null;

        return CreateBitmapImage(imageData);
    }
    public BitmapFrame? GetBitmapFrame()
    {
        using var buffer = GetImage(GetCurrentTasker()?.Controller);
        if (buffer == null) return null;

        var encodedDataHandle = buffer.GetEncodedData(out var size);
        if (encodedDataHandle == IntPtr.Zero)
        {
            Growls.ErrorGlobal("Handle为空！");
            return null;
        }

        var imageData = new byte[size];
        Marshal.Copy(encodedDataHandle, imageData, 0, (int)size);

        if (imageData.Length == 0)
            return null;

        return CreateBitmapFrame(imageData);
    }
    
    private static BitmapFrame CreateBitmapFrame(byte[] byteArray)
    {
        using (MemoryStream stream = new MemoryStream(byteArray))
        {
            BitmapFrame result = BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            return result;
        }
    }
    private static BitmapImage CreateBitmapImage(byte[] imageData)
    {
        var bitmapImage = new BitmapImage();
        using (var ms = new MemoryStream(imageData))
        {
            bitmapImage.BeginInit();
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.StreamSource = ms;
            bitmapImage.EndInit();
        }

        bitmapImage.Freeze();
        return bitmapImage;
    }

    private bool TryRunTasks(MaaTasker? maa, string? task, string? taskParams)
    {
        if (maa == null || task == null) return false;
        if (string.IsNullOrWhiteSpace(taskParams)) taskParams = "{}";
        return maa.AppendPipeline(task, taskParams).Wait() == MaaJobStatus.Succeeded;
    }

    private static MaaImageBuffer GetImage(IMaaController? maaController)
    {
        var buffer = new MaaImageBuffer();
        if (maaController == null)
            return buffer;
        var status = maaController.Screencap().Wait();
        Console.WriteLine(status);
        if (status != MaaJobStatus.Succeeded)
            return buffer;
        maaController.GetCachedImage(buffer);
        return buffer;
    }
}

