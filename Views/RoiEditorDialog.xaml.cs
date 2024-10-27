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
            W = ((int)(rectangle.Width / d)).ToString();
            H = ((int)(rectangle.Height / d)).ToString();
        }
    }

    private void Save(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void Cancel(object sender, RoutedEventArgs e)
    {
        Close();
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