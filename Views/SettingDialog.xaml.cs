using System.Windows;
using MFATools.Utils;

namespace MFATools.Views;

public partial class SettingDialog
{
    public SettingDialog()
    {
        InitializeComponent();
        Init();
    }

    private void Init()
    {
        if ((MainWindow.Data?.IsAdb).IsTrue())
        {
            MainWindow.Instance?.AddSettingOption(settingPanel, "CaptureModeOption",
                [
                    "Default", "RawWithGzip", "RawByNetcat",
                    "Encode", "EncodeToFileAndPull", "MinicapDirect", "MinicapStream",
                    "EmulatorExtras"
                ],
                "AdbControlScreenCapType");
            MainWindow.Instance?.AddBindSettingOption(settingPanel, "InputModeOption",
                ["MiniTouch", "MaaTouch", "AdbInput", "AutoDetect"],
                "AdbControlInputType");
          
        }
        else
        {
            MainWindow.Instance?.AddSettingOption(settingPanel, "CaptureModeOption",
                ["FramePool", "DXGIDesktopDup", "GDI"],
                "Win32ControlScreenCapType");

            MainWindow.Instance?.AddSettingOption(settingPanel, "InputModeOption",
                ["Seize", "SendMessage"],
                "Win32ControlInputType");
        }
        MainWindow.Instance?.AddLanguageOption(settingPanel);
    }

    private void Save(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void Cancel(object sender, RoutedEventArgs e)
    {
        Close();
    }
}