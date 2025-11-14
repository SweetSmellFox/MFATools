using MFATools.Data;
using System;
using System.Windows;
using System.Windows.Media;
using MFATools.Utils;
using System.Windows.Controls;

namespace MFATools.Views;

public partial class SettingDialog
{
    // 默认值常量
    public static float DefaultLineThickness = 1;
    public static SolidColorBrush DefaultLineColor = Brushes.Red;

    public SettingDialog()
    {
        InitializeComponent();
        Init();
    }

    private void Init()
    {
        if ((MainWindow.Data?.IsAdb()).IsTrue())
        {
            MainWindow.Instance?.AddSettingOption(settingPanel, "CaptureModeOption",
                [
                    "Default", "RawWithGzip", "RawByNetcat",
                    "Encode", "EncodeToFileAndPull", "MinicapDirect", "MinicapStream",
                    "EmulatorExtras"
                ],
                "AdbControlScreenCapType");
            MainWindow.Instance?.AddBindSettingOption(settingPanel, "InputModeOption",
                ["MaaTouch", "MiniTouch", "AdbInput", "AutoDetect"],
                "AdbControlInputType");
        }
        else
        {
            MainWindow.Instance?.AddSettingOption(settingPanel, "CaptureModeOption",
                ["FramePool", "GDI", "DXGI_DesktopDup", "DXGI_DesktopDup_Window", "PrintWindow", "ScreenDC"],
                "Win32ControlScreenCapType");

            MainWindow.Instance?.AddSettingOption(settingPanel, "InputModeOption",
                ["Seize", "SendMessage", "PostMessage", "LegacyEvent", "PostThreadMessage"],
                "Win32ControlInputType");
        }
        MainWindow.Instance?.AddLanguageOption(settingPanel);
        RelativePathBox.Text = RelativePath;
        // 初始化线条样式设置
        InitializeLineStyleSettings();
    }

    // 初始化线条样式设置
    private void InitializeLineStyleSettings()
    {
        try
        {
            // 设置线条粗细默认值
            LineThicknessNumeric.Value = DefaultLineThickness;

            // 设置线条颜色默认值
            LineColorPicker.SelectedBrush = DefaultLineColor;
        }
        catch (Exception ex)
        {
            LoggerService.LogError($"初始化线条样式设置失败: {ex.Message}");
        }
    }

    // 保存事件处理
    private void Save(object sender, RoutedEventArgs e)
    {
        try
        {
            // 保存原有设置
            DialogResult = true;

            // 通知线条样式已更改
            NotifyLineStyleChanged();

            Close();
        }
        catch (Exception ex)
        {
            LoggerService.LogError($"保存设置失败: {ex.Message}");
        }
    }

    // 通知线条样式已更改
    private void NotifyLineStyleChanged()
    {
        try
        {
            DefaultLineThickness = (float)LineThicknessNumeric.Value;
            DefaultLineColor = LineColorPicker.SelectedBrush;
        }
        catch (Exception ex)
        {
            LoggerService.LogError($"应用线条样式失败: {ex.Message}");
            throw;
        }
    }

    private void Cancel(object sender, RoutedEventArgs e)
    {
        Close();
    }

    public static string RelativePath { get; set; } = DataSet.GetData("RelativePath", string.Empty) ?? string.Empty;
    private void RelativePathBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (RelativePath != RelativePathBox.Text)
        {
            RelativePath = RelativePathBox.Text;
            DataSet.SetData("RelativePath", RelativePath);
        }
    }
}
