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
using MFATools.Utils.Converters;
using MFATools.ViewModels;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WPFLocalizeExtension.Extensions;
using Attribute = MFATools.Utils.Attribute;
using ComboBox = System.Windows.Controls.ComboBox;
using ScrollViewer = HandyControl.Controls.ScrollViewer;


namespace MFATools.Views;

public partial class MainWindow
{
    public static MainWindow? Instance { get; private set; }
    private readonly MaaToolkit _maaToolkit;

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

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        Application.Current.Shutdown();
    }

    private bool InitializeData()
    {
        DataSet.Data = JsonHelper.ReadFromConfigJsonFile("config", new Dictionary<string, object>());
        if (!File.Exists($"{MaaProcessor.ResourceBase}/pipeline/sample.json"))
        {
            try
            {
                File.WriteAllText($"{MaaProcessor.ResourceBase}/pipeline/sample.json",
                    JsonConvert.SerializeObject(new Dictionary<string, TaskModel>
                    {
                        {
                            "MFATools", new TaskModel()
                            {
                                Action = "DoNothing"
                            }
                        }
                    }, new JsonSerializerSettings
                    {
                        Formatting = Formatting.Indented,
                        NullValueHandling = NullValueHandling.Ignore,
                        DefaultValueHandling = DefaultValueHandling.Ignore
                    }));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"创建文件时发生错误: {ex.Message}");
                LoggerService.LogError(ex);
            }
        }

        MaaProcessor.CurrentResources = new List<string> { MaaProcessor.ResourceBase };
        ConnectToMAA();
        return true;
    }


    private void ToggleWindowTopMost(object sender, RoutedPropertyChangedEventArgs<bool> e)
    {
        Topmost = e.NewValue;
    }

    private void TabControl_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Data is not null)
        {
            Data.IsAdb = adbTab.IsSelected;
            btnCustom.Visibility = adbTab.IsSelected ? Visibility.Visible : Visibility.Collapsed;
        }
        
        MaaProcessor.Instance.SetCurrentTasker();
        
        if ("adb".Equals(MaaProcessor.Config.AdbDevice.AdbPath) &&
            DataSet.TryGetData<JObject>("AdbDevice", out var jObject))
        {
            var settings = new JsonSerializerSettings();
            settings.Converters.Add(new AdbInputMethodsConverter());
            settings.Converters.Add(new AdbScreencapMethodsConverter());

            var device = jObject?.ToObject<AdbDeviceInfo>(JsonSerializer.Create(settings));
            if (device != null)
            {
                deviceComboBox.ItemsSource = new List<AdbDeviceInfo> { device };
                deviceComboBox.SelectedIndex = 0;
                MaaProcessor.Config.IsConnected = true;
            }
        }
        else AutoDetectDevice();
    }

    public void AddSettingOption(Panel? panel, string titleKey, IEnumerable<string> options, string datatype,
        int defaultValue = 0)
    {
        var comboBox = new ComboBox
        {
            ItemsSource = options,
            SelectedIndex = DataSet.GetData(datatype, defaultValue),
            Style = FindResource("ComboBoxExtend") as Style,
            Margin = new Thickness(5)
        };
        var binding = new Binding("Idle")
        {
            Source = Data,
            Mode = BindingMode.OneWay
        };
        comboBox.SetBinding(IsEnabledProperty, binding);
        comboBox.BindLocalization(titleKey);
        comboBox.SetValue(TitleElement.TitlePlacementProperty, TitlePlacementType.Top);
        comboBox.SelectionChanged += (sender, _) =>
        {
            var index = (sender as ComboBox)?.SelectedIndex ?? 0;
            DataSet.SetData(datatype, index);
            MaaProcessor.Instance.SetCurrentTasker();
            ConnectToMAA();
        };

        panel?.Children.Add(comboBox);
    }

    public void AddLanguageOption(Panel? panel = null, int defaultValue = 0)
    {
        if (panel == null)
            return;
        var comboBox = new ComboBox
        {
            Style = FindResource("ComboBoxExtend") as Style,
            Margin = new Thickness(5)
        };

        comboBox.ItemsSource = new List<string> { "简体中文", "English" };
        var binding = new Binding("Idle")
        {
            Source = Data,
            Mode = BindingMode.OneWay
        };
        comboBox.SetBinding(IsEnabledProperty, binding);
        comboBox.BindLocalization("LanguageOption");
        comboBox.SetValue(TitleElement.TitlePlacementProperty, TitlePlacementType.Top);

        comboBox.SelectionChanged += (sender, _) =>
        {
            var index = (sender as ComboBox)?.SelectedIndex ?? 0;
            LanguageManager.ChangeLanguage(
                CultureInfo.CreateSpecificCulture(index == 0 ? "zh-cn" : "en-us"));
            DataSet.SetData("LangIndex", index);
        };

        comboBox.SelectedIndex = DataSet.GetData("LangIndex", defaultValue);
        panel.Children.Add(comboBox);
    }

    public void AddBindSettingOption(Panel? panel, string titleKey, IEnumerable<string> options, string datatype,
        int defaultValue = 0)

    {
        var comboBox = new ComboBox
        {
            SelectedIndex = DataSet.GetData(datatype, defaultValue),
            Style = FindResource("ComboBoxExtend") as Style,
            Margin = new Thickness(5)
        };
        var binding = new Binding("Idle")
        {
            Source = Data,
            Mode = BindingMode.OneWay
        };
        comboBox.SetBinding(IsEnabledProperty, binding);
        foreach (var s in options)
        {
            var comboBoxItem = new ComboBoxItem();
            comboBoxItem.BindLocalization(s, ContentProperty);
            comboBox.Items.Add(comboBoxItem);
        }

        comboBox.BindLocalization(titleKey);
        comboBox.SetValue(TitleElement.TitlePlacementProperty, TitlePlacementType.Top);
        comboBox.SelectionChanged += (sender, _) =>
        {
            var index = (sender as ComboBox)?.SelectedIndex ?? 0;
            DataSet.SetData(datatype, index);
            MaaProcessor.Instance.SetCurrentTasker();
        };

        panel?.Children.Add(comboBox);
    }

    private void DeviceComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (deviceComboBox.SelectedItem is DesktopWindowInfo window)
        {
            Growl.Info(string.Format(LocExtension.GetLocalizedValue<string>("WindowSelectionMessage"),
                window.Name));
            MaaProcessor.Config.DesktopWindow.HWnd = window.Handle;
            
        }
        else if (deviceComboBox.SelectedItem is AdbDeviceInfo device)
        {
            Growl.Info(string.Format(LocExtension.GetLocalizedValue<string>("EmulatorSelectionMessage"),
                device.Name));
            MaaProcessor.Config.AdbDevice.AdbPath = device.AdbPath;
            MaaProcessor.Config.AdbDevice.AdbSerial = device.AdbSerial;
            MaaProcessor.Config.AdbDevice.Config = device.Config;
            DataSet.SetData("AdbDevice", device);
        }
        MaaProcessor.Instance.SetCurrentTasker();
    }

    private void Refresh(object sender, RoutedEventArgs e)
    {
        AutoDetectDevice();
    }

    private void EditSetting(object sender, RoutedEventArgs e)
    {
        SettingDialog settingDialog = new();
        settingDialog.ShowDialog();
    }

    private void CustomAdb(object sender, RoutedEventArgs e)
    {
        var deviceInfo =
            deviceComboBox.Items.Count > 0 && deviceComboBox.SelectedItem is AdbDeviceInfo device
                ? device
                : null;
        var dialog = new AdbEditorDialog(deviceInfo);
        if (dialog.ShowDialog().IsTrue())
        {
            deviceComboBox.ItemsSource = new List<AdbDeviceInfo> { dialog.Output };
            deviceComboBox.SelectedIndex = 0;
            MaaProcessor.Config.IsConnected = true;
        }
    }

    public async void AutoDetectDevice()
    {
        try
        {
            Growl.Info((Data?.IsAdb).IsTrue()
                ? LocExtension.GetLocalizedValue<string>("EmulatorDetectionStarted")
                : LocExtension.GetLocalizedValue<string>("WindowDetectionStarted"));
            MaaProcessor.Config.IsConnected = false;
            if ((Data?.IsAdb).IsTrue())
            {
                var devices = await _maaToolkit.AdbDevice.FindAsync();
                deviceComboBox.ItemsSource = devices;
                MaaProcessor.Config.IsConnected = devices.Count > 0;
                deviceComboBox.SelectedIndex = 0;
            }
            else
            {
                var windows = _maaToolkit.Desktop.Window.Find();
                deviceComboBox.ItemsSource = windows;
                MaaProcessor.Config.IsConnected = windows.Count > 0;
                deviceComboBox.SelectedIndex = windows.Count > 0
                    ? windows.ToList().FindIndex(win => !string.IsNullOrWhiteSpace(win.Name))
                    : 0;
            }

            if (!MaaProcessor.Config.IsConnected)
            {
                Growl.Info((Data?.IsAdb).IsTrue()
                    ? LocExtension.GetLocalizedValue<string>("NoEmulatorFound")
                    : LocExtension.GetLocalizedValue<string>("NoWindowFound"));
            }
        }
        catch (Exception ex)
        {
            Growls.WarningGlobal(string.Format(LocExtension.GetLocalizedValue<string>("TaskStackError"),
                (Data?.IsAdb).IsTrue() ? "Simulator".GetLocalizationString() : "Window".GetLocalizationString(),
                ex.Message));
            MaaProcessor.Config.IsConnected = false;
            LoggerService.LogError(ex);
            Console.WriteLine(ex);
        }
    }

    public void ConnectToMAA()
    {
        ConfigureMaaProcessorForADB();
        ConfigureMaaProcessorForWin32();
    }

    private void ConfigureMaaProcessorForADB()
    {
        if ((Data?.IsAdb).IsTrue())
        {
            var adbInputType = ConfigureAdbInputTypes();
            var adbScreenCapType = ConfigureAdbScreenCapTypes();

            MaaProcessor.Config.AdbDevice.Input = adbInputType;
            MaaProcessor.Config.AdbDevice.ScreenCap = adbScreenCapType;

            Console.WriteLine(
                $"{LocExtension.GetLocalizedValue<string>("AdbInputMode")}{adbInputType},{LocExtension.GetLocalizedValue<string>("AdbCaptureMode")}{adbScreenCapType}");
        }
    }

    public string ScreenshotType()
    {
        if ((Data?.IsAdb).IsTrue())
            return ConfigureAdbScreenCapTypes().ToString();
        return ConfigureWin32ScreenCapTypes().ToString();
    }

    private AdbInputMethods ConfigureAdbInputTypes()
    {
        return DataSet.GetData("AdbControlInputType", 0) switch
        {
            0 => AdbInputMethods.MinitouchAndAdbKey,
            1 => AdbInputMethods.Maatouch,
            2 => AdbInputMethods.AdbShell,
            3 => AdbInputMethods.All,
            _ => 0
        };
    }

    private AdbScreencapMethods ConfigureAdbScreenCapTypes()
    {
        return DataSet.GetData("AdbControlScreenCapType", 0) switch
        {
            0 => AdbScreencapMethods.Default,
            1 => AdbScreencapMethods.RawWithGzip,
            2 => AdbScreencapMethods.RawByNetcat,
            3 => AdbScreencapMethods.Encode,
            4 => AdbScreencapMethods.EncodeToFileAndPull,
            5 => AdbScreencapMethods.MinicapDirect,
            6 => AdbScreencapMethods.MinicapStream,
            7 => AdbScreencapMethods.EmulatorExtras,
            _ => 0
        };
    }

    private void ConfigureMaaProcessorForWin32()
    {
        if (!(Data?.IsAdb).IsTrue())
        {
            var win32InputType = ConfigureWin32InputTypes();
            var winScreenCapType = ConfigureWin32ScreenCapTypes();

            MaaProcessor.Config.DesktopWindow.Input = win32InputType;
            MaaProcessor.Config.DesktopWindow.ScreenCap = winScreenCapType;

            Console.WriteLine(
                $"{"AdbInputMode".GetLocalizationString()}{win32InputType},{"AdbCaptureMode".GetLocalizationString()}{winScreenCapType}");
            LoggerService.LogInfo(
                $"{"AdbInputMode".GetLocalizationString()}{win32InputType},{"AdbCaptureMode".GetLocalizationString()}{winScreenCapType}");
        }
    }

    private Win32ScreencapMethod ConfigureWin32ScreenCapTypes()
    {
        return DataSet.GetData("Win32ControlScreenCapType", 0) switch
        {
            0 => Win32ScreencapMethod.FramePool,
            1 => Win32ScreencapMethod.DXGIDesktopDup,
            2 => Win32ScreencapMethod.GDI,
            _ => 0
        };
    }

    private Win32InputMethod ConfigureWin32InputTypes()
    {
        return DataSet.GetData("Win32ControlInputType", 0) switch
        {
            0 => Win32InputMethod.Seize,
            1 => Win32InputMethod.SendMessage,
            _ => 0
        };
    }


    /// <summary>
    /// 向日志框中添加文本，可以包含换行符。
    /// </summary>
    /// <param name="content">要添加的内容</param>
    public void AppendLog(Attribute? content)
    {
        Growls.Process(() =>
        {
            TagContainer.Items.Add(new AttributeTag(content)
            {
                Margin = new Thickness(2)
            });
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
        SelectionRegionDialog selectionRegionDialog = new SelectionRegionDialog();
        if (selectionRegionDialog.ShowDialog() == true)
        {
            AppendLog(selectionRegionDialog.IsRoi
                ? new Attribute("roi", selectionRegionDialog.Output)
                : new Attribute("target", selectionRegionDialog.Output));
        }
    }

    private void Screenshot(object sender, RoutedEventArgs e)
    {
        CropImageDialog cropImageDialog = new CropImageDialog();
        if (cropImageDialog.ShowDialog() == true)
        {
            AppendLog(new Attribute("template", cropImageDialog.Output));
            AppendLog(new Attribute("origin roi", cropImageDialog.OutputOriginRoi));
            AppendLog(new Attribute("recommended roi", cropImageDialog.OutputRoi));
        }
    }

    private void Swipe(object sender, RoutedEventArgs e)
    {
        SwipeDialog swipeDialog = new SwipeDialog();
        if (swipeDialog.ShowDialog() == true)
        {
            AppendLog(new Attribute("begin", swipeDialog.OutputBegin));
            AppendLog(new Attribute("end", swipeDialog.OutputEnd));
        }
    }

    private void ColorExtraction(object sender, RoutedEventArgs e)
    {
        ColorExtractionDialog colorExtractionDialog = new ColorExtractionDialog();
        if (colorExtractionDialog.ShowDialog() == true)
        {
            AppendLog(new Attribute("upper", colorExtractionDialog.OutputUpper));
                AppendLog(new Attribute("lower", colorExtractionDialog.OutputLower));
            AppendLog(new Attribute("recommended roi", colorExtractionDialog.OutputRoi));
            switch (colorExtractionDialog.SelectType.SelectedIndex)
            {
                case 0:AppendLog(new Attribute("method", 4));break;
                case 1:AppendLog(new Attribute("method", 40));break;
                case 2:AppendLog(new Attribute("method", 6));break;
            }
        }
    }

    private void RecognitionText(object sender, RoutedEventArgs e)
    {
        RecognitionTextDialog recognition = new RecognitionTextDialog();
        if (recognition.ShowDialog() == true && recognition.Output != null)
        {
            AppendLog(new Attribute("expected",
                OCRHelper.ReadTextFromMAATasker(recognition.Output[0], recognition.Output[1],
                    recognition.Output[2], recognition.Output[3])));
            AppendLog(new Attribute("recommended roi", recognition.OutputRoi));
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