using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using HandyControl.Controls;
using HandyControl.Data;
using HandyControl.Themes;
using MaaFramework.Binding;
using MFATools.Controls;
using MFATools.Data;
using MFATools.Utils;
using MFATools.ViewModels;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WPFLocalizeExtension.Deprecated.Extensions;
using WPFLocalizeExtension.Engine;
using WPFLocalizeExtension.Extensions;
using Attribute = MFATools.Utils.Attribute;
using ComboBox = HandyControl.Controls.ComboBox;
using ScrollViewer = HandyControl.Controls.ScrollViewer;
using TextBlock = System.Windows.Controls.TextBlock;

namespace MFATools.Views
{
    public partial class MainWindow : CustomWindow
    {
        public static MainWindow? Instance { get; private set; }
        private readonly MaaToolkit _maaToolkit;
        public bool IsADB { get; set; } = true;

        public static MainViewModel? Data { get; private set; }

        public static readonly string Version =
            $"v{Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "DEBUG"}";

        public Dictionary<string, TaskModel> TaskDictionary = new();

        public MainWindow()
        {
            InitializeComponent();
            Instance = this;
            version.Text = Version;
            _maaToolkit = new MaaToolkit(init: true);
            Data = DataContext as MainViewModel;

            InitializeData();
            OCRHelper.Initialize();
            VersionChecker.CheckVersion();
        }

        private bool InitializeData()
        {
            DataSet.Data = JsonHelper.ReadFromConfigJsonFile("config", new Dictionary<string, object>());
            MaaInterface.Instance =
                JsonHelper.ReadFromJsonFilePath(MaaProcessor.Resource, "interface", new MaaInterface());
            if (MaaInterface.Instance != null)
            {
                Data?.TaskItemViewModels.Clear();
                LoadTasks(MaaInterface.Instance.task ?? new List<TaskInterfaceItem>());
            }

            ConnectToMAA();
            return LoadTask();
        }

        private bool firstTask = true;

        private void LoadTasks(IEnumerable<TaskInterfaceItem> tasks)
        {
            foreach (var task in tasks)
            {
                var dragItem = new DragItemViewModel(task)
                {
                    IsCheckedWithNull = task.check ?? false,
                    SettingVisibility = task.repeatable == true || task.option?.Count > 0
                        ? Visibility.Visible
                        : Visibility.Hidden
                };

                if (firstTask)
                {
                    if (MaaInterface.Instance?.Resources != null &&
                        MaaInterface.Instance.Resources.Count > DataSet.GetData("ResourceIndex", 0))
                        MaaProcessor.CurrentResources =
                            MaaInterface.Instance.Resources[
                                MaaInterface.Instance.Resources.Keys.ToList()[DataSet.GetData("ResourceIndex", 0)]];
                    else MaaProcessor.CurrentResources = new List<string> { MaaProcessor.ResourceBase };
                    firstTask = false;
                }

                Data?.TaskItemViewModels.Add(dragItem);
            }
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
            Application.Current.Shutdown();
        }

        private void btnRestore_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Normal ? WindowState.Maximized : WindowState.Normal;
        }

        private void btnMinimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void ToggleWindowTopMost(object sender, RoutedEventArgs e)
        {
            if (Data == null) return;
            Topmost = !Topmost;
            if (Topmost)
                Data.WindowTopMostButtonForeground = FindResource("PrimaryBrush") as Brush ?? Brushes.DarkGray;
            else
                Data.WindowTopMostButtonForeground = FindResource("ActionIconColor") as Brush ?? Brushes.DarkGray;
        }

        private void TaskList_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var scrollViewer = FindVisualParent<ScrollViewer>((DependencyObject)sender);

            if (scrollViewer != null)
            {
                scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta / 3);
                e.Handled = true;
            }
        }

        private T? FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            var parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;

            var parent = parentObject as T;
            return parent ?? FindVisualParent<T>(parentObject);
        }

        private bool LoadTask()
        {
            try
            {
                var taskDictionary = new Dictionary<string, TaskModel>();
                if (MaaProcessor.CurrentResources != null)
                {
                    foreach (var resourcePath in MaaProcessor.CurrentResources)
                    {
                        var jsonFiles = Directory.GetFiles($"{resourcePath}/pipeline/", "*.json");
                        var taskDictionaryA = new Dictionary<string, TaskModel>();
                        foreach (var file in jsonFiles)
                        {
                            var content = File.ReadAllText(file);
                            var taskData = JsonConvert.DeserializeObject<Dictionary<string, TaskModel>>(content);
                            if (taskData == null || taskData.Count == 0)
                                break;
                            foreach (var task in taskData)
                            {
                                if (!taskDictionaryA.TryAdd(task.Key, task.Value))
                                {
                                    Growls.ErrorGlobal(string.Format(
                                        LocExtension.GetLocalizedValue<string>("DuplicateTaskError"), task.Key));
                                    return false;
                                }
                            }
                        }

                        taskDictionary = taskDictionary.MergeTaskModels(taskDictionaryA);
                    }
                }

                PopulateTasks(taskDictionary);

                return true;
            }
            catch (Exception ex)
            {
                Growls.ErrorGlobal(string.Format(LocExtension.GetLocalizedValue<string>("PipelineLoadError"),
                    ex.Message));
                Console.WriteLine(ex);
                LoggerService.LogError(ex);
                return false;
            }
        }

        private void PopulateTasks(Dictionary<string, TaskModel> taskDictionary)
        {
            TaskDictionary = taskDictionary;
            foreach (var task in taskDictionary)
            {
                task.Value.name = task.Key;
                ValidateTaskLinks(taskDictionary, task);
                Data?.SourceItems.Add(new TaskItemViewModel { Task = task.Value });
            }
        }

        private void ValidateTaskLinks(Dictionary<string, TaskModel> taskDictionary,
            KeyValuePair<string, TaskModel> task)
        {
            ValidateNextTasks(taskDictionary, task.Value.next);
            ValidateNextTasks(taskDictionary, task.Value.runout_next, "runout_next");
            ValidateNextTasks(taskDictionary, task.Value.timeout_next, "timeout_next");
        }

        private void ValidateNextTasks(Dictionary<string, TaskModel> taskDictionary, object? nextTasks,
            string name = "next")
        {
            if (nextTasks is List<string> tasks)
            {
                foreach (var task in tasks)
                {
                    if (!taskDictionary.ContainsKey(task))
                    {
                        Growls.ErrorGlobal(string.Format(LocExtension.GetLocalizedValue<string>("TaskNotFoundError"),
                            name, task));
                    }
                }
            }
        }

        private void Start(object sender, RoutedEventArgs e)
        {
            if (InitializeData())
            {
                MaaProcessor.Money = 0;
                var tasks = Data?.TaskItemViewModels.ToList().FindAll(task => task.IsChecked);
                ConnectToMAA();
                MaaProcessor.Instance.Start(tasks);
            }
        }

        private void Stop(object sender, RoutedEventArgs e)
        {
            MaaProcessor.Instance.Stop();
        }

        private void TabControl_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            IsADB = adbTab.IsSelected;

            if ("adb".Equals(MaaProcessor.Config.Adb.Adb) && DataSet.TryGetData<JObject>("Adb", out var jObject))
            {
                var device = jObject?.ToObject<DeviceInfo>();
                if (device != null)
                {
                    deviceComboBox.ItemsSource = new List<DeviceInfo> { device };
                    deviceComboBox.SelectedIndex = 0;
                    MaaProcessor.Config.IsConnected = true;
                }
            }
            else AutoDetectDevice();

            MaaProcessor.Instance.SetCurrentInstance(null);
        }

        private void DeviceComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (deviceComboBox.SelectedItem is WindowInfo window)
            {
                Growl.Info(string.Format(LocExtension.GetLocalizedValue<string>("WindowSelectionMessage"),
                    window.Name));
                MaaProcessor.Config.Win32.HWnd = window.Handle;
                MaaProcessor.Instance?.SetCurrentInstance(null);
            }
            else if (deviceComboBox.SelectedItem is DeviceInfo device)
            {
                Growl.Info(string.Format(LocExtension.GetLocalizedValue<string>("EmulatorSelectionMessage"),
                    device.Name));
                MaaProcessor.Config.Adb.Adb = device.AdbPath;
                MaaProcessor.Config.Adb.AdbAddress = device.AdbSerial;
                MaaProcessor.Config.Adb.AdbConfig = device.AdbConfig;
                MaaProcessor.Instance?.SetCurrentInstance(null);
                DataSet.SetData("Adb", device);
            }
        }

        private void Refresh(object sender, RoutedEventArgs e)
        {
            AutoDetectDevice();
        }

        public async void AutoDetectDevice()
        {
            try
            {
                Growl.Info(IsADB
                    ? LocExtension.GetLocalizedValue<string>("EmulatorDetectionStarted")
                    : LocExtension.GetLocalizedValue<string>("WindowDetectionStarted"));
                MaaProcessor.Config.IsConnected = false;
                if (IsADB)
                {
                    var devices = await _maaToolkit.Device.FindAsync();
                    deviceComboBox.ItemsSource = devices;
                    MaaProcessor.Config.IsConnected = devices.Length > 0;
                    deviceComboBox.SelectedIndex = 0;
                }
                else
                {
                    var windows = _maaToolkit.Win32.Window.ListWindows().ToList();
                    deviceComboBox.ItemsSource = windows;
                    MaaProcessor.Config.IsConnected = windows.Count > 0;
                    deviceComboBox.SelectedIndex = windows.Count > 0
                        ? windows.FindIndex(win => !string.IsNullOrWhiteSpace(win.Name))
                        : 0;
                }

                if (!MaaProcessor.Config.IsConnected)
                {
                    Growl.Info(IsADB
                        ? LocExtension.GetLocalizedValue<string>("NoEmulatorFound")
                        : LocExtension.GetLocalizedValue<string>("NoWindowFound"));
                }
            }
            catch (Exception ex)
            {
                Growls.WarningGlobal(string.Format(LocExtension.GetLocalizedValue<string>("TaskStackError"),
                    IsADB ? "Simulator".GetLocalizationString() : "Window".GetLocalizationString(), ex.Message));
                MaaProcessor.Config.IsConnected = false;
            }
        }

        public void ConnectToMAA()
        {
            ConfigureMaaProcessorForADB();
            ConfigureMaaProcessorForWin32();
        }

        private void ConfigureMaaProcessorForADB()
        {
            if (IsADB)
            {
                var adbTouchType = ConfigureAdbControllerTypes();
                var adbScreenCapType = ConfigureAdbScreenCapTypes();

                MaaProcessor.Config.Adb.Touch = adbTouchType;
                MaaProcessor.Config.Adb.ScreenCap = adbScreenCapType;

                Console.WriteLine(
                    $"{LocExtension.GetLocalizedValue<string>("AdbTouchMode")}{adbTouchType},{LocExtension.GetLocalizedValue<string>("AdbCaptureMode")}{adbScreenCapType}");
            }
        }

        private AdbControllerTypes ConfigureAdbControllerTypes()
        {
            return DataSet.GetData("AdbControlTouchType", 0) switch
            {
                0 => AdbControllerTypes.InputPresetMiniTouch,
                1 => AdbControllerTypes.InputPresetMaaTouch,
                2 => AdbControllerTypes.InputPresetAdb,
                3 => AdbControllerTypes.InputPresetAutoDetect,
                _ => 0
            };
        }

        private AdbControllerTypes ConfigureAdbScreenCapTypes()
        {
            return DataSet.GetData("AdbControlScreenCapType", 0) switch
            {
                0 => AdbControllerTypes.ScreencapFastestLosslessWay,
                1 => AdbControllerTypes.ScreencapRawWithGzip,
                2 => AdbControllerTypes.ScreencapFastestWayCompatible,
                3 => AdbControllerTypes.ScreencapRawByNetcat,
                4 => AdbControllerTypes.ScreencapEncode,
                5 => AdbControllerTypes.ScreencapEncodeToFile,
                6 => AdbControllerTypes.ScreencapMinicapDirect,
                7 => AdbControllerTypes.ScreencapMinicapStream,
                8 => AdbControllerTypes.ScreencapEmulatorExtras,
                9 => AdbControllerTypes.ScreencapFastestWay,
                _ => 0
            };
        }

        private void ConfigureMaaProcessorForWin32()
        {
            if (!IsADB)
            {
                var winTouchType = ConfigureWin32ControllerTypes();
                var winScreenCapType = ConfigureWin32ScreenCapTypes();

                MaaProcessor.Config.Win32.Touch = winTouchType;
                MaaProcessor.Config.Win32.ScreenCap = winScreenCapType;

                Console.WriteLine(
                    $"{LocExtension.GetLocalizedValue<string>("AdbTouchMode")}{winTouchType},{LocExtension.GetLocalizedValue<string>("AdbCaptureMode")}{winScreenCapType}");
            }
        }

        private Win32ControllerTypes ConfigureWin32ControllerTypes()
        {
            return DataSet.GetData("Win32ControlTouchType", 0) switch
            {
                0 => Win32ControllerTypes.ScreencapDXGIFramePool,
                1 => Win32ControllerTypes.ScreencapDXGIDesktopDup,
                2 => Win32ControllerTypes.ScreencapGDI,
                _ => 0
            };
        }

        private Win32ControllerTypes ConfigureWin32ScreenCapTypes()
        {
            return DataSet.GetData("Win32ControlTouchType", 0) switch
            {
                0 => Win32ControllerTypes.TouchSeize | Win32ControllerTypes.KeySeize,
                1 => Win32ControllerTypes.TouchSendMessage | Win32ControllerTypes.KeySendMessage,
                _ => 0
            };
        }


        /// <summary>
        /// 向日志框中添加文本，可以包含换行符。
        /// </summary>
        /// <param name="content">要添加的内容</param>
        public void AppendLog(Attribute? content)
        {
            TagContainer.Items.Add(new AttributeTag(content)
            {
                Margin = new Thickness(2)
            });
        }

        /// <summary>
        /// 清空日志框中的所有文本。
        /// </summary>
        public void ClearLog()
        {
            TagContainer.Items.Clear();
        }

        private void SelectionRegion(object sender, RoutedEventArgs e)
        {
            var image = MaaProcessor.Instance.GetBitmapImage();
            if (image != null)
            {
                SelectionRegionDialog selectionRegionDialog = new SelectionRegionDialog(image);
                if (selectionRegionDialog.ShowDialog() == true)
                {
                    AppendLog(selectionRegionDialog.IsRoi
                        ? new Attribute("roi", selectionRegionDialog.Output)
                        : new Attribute("target", selectionRegionDialog.Output));
                }
            }
        }

        private void Screenshot(object sender, RoutedEventArgs e)
        {
            var image = MaaProcessor.Instance.GetBitmapImage();
            if (image != null)
            {
                CropImageDialog cropImageDialog = new CropImageDialog(image);
                if (cropImageDialog.ShowDialog() == true)
                {
                    AppendLog(new Attribute("template", cropImageDialog.Output));
                    AppendLog(new Attribute("expanded roi", cropImageDialog.OutputRoi));
                }
            }
        }

        private void Swipe(object sender, RoutedEventArgs e)
        {
            var image = MaaProcessor.Instance.GetBitmapImage();
            if (image != null)
            {
                SwipeDialog swipeDialog = new SwipeDialog(image);
                if (swipeDialog.ShowDialog() == true)
                {
                    AppendLog(new Attribute("begin", swipeDialog.OutputBegin));
                    AppendLog(new Attribute("end", swipeDialog.OutputEnd));
                }
            }
        }

        private void ColorExtraction(object sender, RoutedEventArgs e)
        {
            var image = MaaProcessor.Instance.GetBitmapImage();
            if (image != null)
            {
                ColorExtractionDialog colorExtractionDialog = new ColorExtractionDialog(image);
                if (colorExtractionDialog.ShowDialog() == true)
                {
                    AppendLog(new Attribute("upper", colorExtractionDialog.OutputUpper));
                    AppendLog(new Attribute("lower", colorExtractionDialog.OutputLower));
                    AppendLog(new Attribute("expanded roi", colorExtractionDialog.OutputRoi));
                }
            }
        }

        private void RecognitionText(object sender, RoutedEventArgs e)
        {
            var image = MaaProcessor.Instance.GetBitmapImage();
            if (image != null)
            {
                RecognitionTextDialog recognition = new RecognitionTextDialog(image);
                if (recognition.ShowDialog() == true && recognition.Output != null)
                {
                    AppendLog(new Attribute("expected",
                        OCRHelper.ReadTextFromMAARecognition(recognition.Output[0], recognition.Output[1],
                            recognition.Output[2], recognition.Output[3])));
                    AppendLog(new Attribute("expanded roi", recognition.OutputRoi));
                }
            }
        }

        private void ClearLog(object sender, RoutedEventArgs e)
        {
            ClearLog();
        }

        private void Copy(object sender, RoutedEventArgs e)
        {
        }
    }
}