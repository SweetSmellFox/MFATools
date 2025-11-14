using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System.IO;
using System.Windows;

namespace MFATools.Views;

public partial class Editor : Window
{
    private static Editor? editor;
    private WebView2? webView;
    public Editor()
    {
        InitializeComponent();
        var executablePath = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "webView");
        if (!Directory.Exists(executablePath))
            Directory.CreateDirectory(executablePath);
        Loaded += async (s, e) =>  // 注意这里添加 async 关键字
        {
            webView = new WebView2();
            Grid.Children.Add(webView);
            // 关键：先异步初始化 CoreWebView2，等待初始化完成
            var env = await CoreWebView2Environment.CreateAsync(userDataFolder: executablePath);
            Console.WriteLine(1);
            await webView.EnsureCoreWebView2Async(env);  // null 表示使用默认环境
        
            // 初始化完成后，再访问 CoreWebView2.Settings
            Console.WriteLine(webView.CoreWebView2 == null);
            if (webView.CoreWebView2 != null)
            {
                webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
                webView.CoreWebView2.Settings.IsScriptEnabled = true;
                webView.Source = new Uri("https://yamaape.codax.site/MaaPipelineEditor/");
                // 可以继续添加其他设置，例如：
                // webView.CoreWebView2.Settings.AllowFileAccessFromFileUrls = true;
            }
        };
    }

    public static bool CreateEditor()
    {
        if (editor == null)
        {
            editor = new Editor();
            editor.Show();
            return true;
        }

        editor.Activate();
        return false;

    }

    protected override void OnClosed(EventArgs e)
    {
        editor = null;
        webView?.Dispose();
        base.OnClosed(e);
    }
}
