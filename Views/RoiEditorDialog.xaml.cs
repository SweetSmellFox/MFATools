using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;

namespace MFATools.Views;

public partial class RoiEditorDialog
{
    public RoiEditorDialog(Rectangle? rectangle = null, double d = 0)
    {
        InitializeComponent();
        if (rectangle != null)
        {
            X = ((int)(Canvas.GetLeft(rectangle) / d)).ToString();
            Y = ((int)(Canvas.GetTop(rectangle) / d)).ToString();
            W = (rectangle.Width / d) >= 0 ? ((int)(rectangle.Width / d)).ToString() : "1";
            H = (rectangle.Height / d) >= 0 ? ((int)(rectangle.Height / d)).ToString() : "1";
        }
    }

    static int[] ExtractNumbers(string input)
    {
        var matches = Regex.Matches(input, @"\d+")
            .Cast<Match>()
            .Select(m => int.Parse(m.Value))
            .ToList();

        while (matches.Count < 4)
        {
            matches.Add(0);
        }

        return matches.Take(4).ToArray();
    }

    private void Save(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void Cancel(object sender, RoutedEventArgs e)
    {
        Close();
    }
    private void Paste(object sender, RoutedEventArgs e)
    {
        if (Clipboard.ContainsText())
        {
            var clipboardText = Clipboard.GetText();
            var lx = ExtractNumbers(clipboardText);
            xText.Text = lx[0].ToString();
            yText.Text = lx[1].ToString();
            wText.Text = lx[2].ToString();
            hText.Text = lx[3].ToString();
            X = lx[0].ToString();
            Y = lx[1].ToString();
            W = lx[2].ToString();
            H = lx[3].ToString();
        }
    }
    public static readonly DependencyProperty XProperty =
        DependencyProperty.Register(
            nameof(X),
            typeof(string),
            typeof(AdbEditorDialog),
            new FrameworkPropertyMetadata(
                string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public string X
    {
        get => (string)GetValue(XProperty);
        set => SetValue(XProperty, value);
    }

    public static readonly DependencyProperty YProperty =
        DependencyProperty.Register(
            nameof(Y),
            typeof(string),
            typeof(AdbEditorDialog),
            new FrameworkPropertyMetadata(
                string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public string Y
    {
        get => (string)GetValue(YProperty);
        set => SetValue(YProperty, value);
    }

    public static readonly DependencyProperty WProperty =
        DependencyProperty.Register(
            nameof(W),
            typeof(string),
            typeof(AdbEditorDialog),
            new FrameworkPropertyMetadata(
                string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public string W
    {
        get => (string)GetValue(WProperty);
        set => SetValue(WProperty, value);
    }

    public static readonly DependencyProperty HProperty =
        DependencyProperty.Register(
            nameof(H),
            typeof(string),
            typeof(AdbEditorDialog),
            new FrameworkPropertyMetadata(
                string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public string H
    {
        get => (string)GetValue(HProperty);
        set => SetValue(HProperty, value);
    }


}