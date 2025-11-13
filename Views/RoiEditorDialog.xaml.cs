using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;

namespace MFATools.Views;

public partial class RoiEditorDialog
{
    private bool _hasColor = false;
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
    public RoiEditorDialog((int x, int y, int w, int h) rectangle)
    {
        InitializeComponent();

        X = rectangle.x.ToString();
        Y = rectangle.y.ToString();
        W = rectangle.w.ToString();
        H = rectangle.h.ToString();

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
    
    private void SaveColor(object sender, RoutedEventArgs e)
    {
        EditColor = true;
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
    public static readonly DependencyProperty EditColorProperty =
        DependencyProperty.Register(
            nameof(EditColor),
            typeof(bool),
            typeof(AdbEditorDialog),
            new FrameworkPropertyMetadata(
                false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public bool EditColor
    {
        get => (bool)GetValue(EditColorProperty);
        set => SetValue(EditColorProperty, value);
    }
    
    public static readonly DependencyProperty HasColorProperty =
        DependencyProperty.Register(
            nameof(HasColor),
            typeof(bool),
            typeof(AdbEditorDialog),
            new FrameworkPropertyMetadata(
                false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public bool HasColor
    {
        get => (bool)GetValue(HasColorProperty);
        set => SetValue(HasColorProperty, value);
    }

    public static readonly DependencyProperty URProperty =
        DependencyProperty.Register(
            nameof(UR),
            typeof(double),
            typeof(AdbEditorDialog),
            new FrameworkPropertyMetadata(
                0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public double UR
    {
        get => (double)GetValue(URProperty);
        set => SetValue(URProperty, value);
    }

    // Upper RGB - G（绿色通道）
    public static readonly DependencyProperty UGProperty =
        DependencyProperty.Register(
            nameof(UG),
            typeof(double),
            typeof(AdbEditorDialog),
            new FrameworkPropertyMetadata(
                0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public double UG
    {
        get => (double)GetValue(UGProperty);
        set => SetValue(UGProperty, value);
    }

    // Upper RGB - B（蓝色通道）
    public static readonly DependencyProperty UBProperty =
        DependencyProperty.Register(
            nameof(UB),
            typeof(double),
            typeof(AdbEditorDialog),
            new FrameworkPropertyMetadata(
                0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public double UB
    {
        get => (double)GetValue(UBProperty);
        set => SetValue(UBProperty, value);
    }


    // ====================== Lower RGB ======================
    // Lower RGB - R（红色通道）
    public static readonly DependencyProperty LRProperty =
        DependencyProperty.Register(
            nameof(LR),
            typeof(double),
            typeof(AdbEditorDialog),
            new FrameworkPropertyMetadata(
                0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public double LR
    {
        get => (double)GetValue(LRProperty);
        set => SetValue(LRProperty, value);
    }

    // Lower RGB - G（绿色通道）
    public static readonly DependencyProperty LGProperty =
        DependencyProperty.Register(
            nameof(LG),
            typeof(double),
            typeof(AdbEditorDialog),
            new FrameworkPropertyMetadata(
                0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public double LG
    {
        get => (double)GetValue(LGProperty);
        set => SetValue(LGProperty, value);
    }

    // Lower RGB - B（蓝色通道）
    public static readonly DependencyProperty LBProperty =
        DependencyProperty.Register(
            nameof(LB),
            typeof(double),
            typeof(AdbEditorDialog),
            new FrameworkPropertyMetadata(
                0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public double LB
    {
        get => (double)GetValue(LBProperty);
        set => SetValue(LBProperty, value);
    }


    // ====================== Upper HSV ======================
    // Upper HSV - H（色相，常规范围0-180或0-360，此处按XAML中NumericUpDown的180上限适配）
    public static readonly DependencyProperty UHProperty =
        DependencyProperty.Register(
            nameof(UH),
            typeof(double),
            typeof(AdbEditorDialog),
            new FrameworkPropertyMetadata(
                0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public double UH
    {
        get => (double)GetValue(UHProperty);
        set => SetValue(UHProperty, value);
    }

    // Upper HSV - S（饱和度，常规范围0-255）
    public static readonly DependencyProperty USProperty =
        DependencyProperty.Register(
            nameof(US),
            typeof(double),
            typeof(AdbEditorDialog),
            new FrameworkPropertyMetadata(
                0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public double US
    {
        get => (double)GetValue(USProperty);
        set => SetValue(USProperty, value);
    }

    // Upper HSV - V（明度，常规范围0-255）
    public static readonly DependencyProperty UVProperty =
        DependencyProperty.Register(
            nameof(UV),
            typeof(double),
            typeof(AdbEditorDialog),
            new FrameworkPropertyMetadata(
                0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public double UV
    {
        get => (double)GetValue(UVProperty);
        set => SetValue(UVProperty, value);
    }


    // ====================== Lower HSV ======================
    // Lower HSV - H（色相）
    public static readonly DependencyProperty LHProperty =
        DependencyProperty.Register(
            nameof(LH),
            typeof(double),
            typeof(AdbEditorDialog),
            new FrameworkPropertyMetadata(
                0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public double LH
    {
        get => (double)GetValue(LHProperty);
        set => SetValue(LHProperty, value);
    }

    // Lower HSV - S（饱和度）
    public static readonly DependencyProperty LSProperty =
        DependencyProperty.Register(
            nameof(LS),
            typeof(double),
            typeof(AdbEditorDialog),
            new FrameworkPropertyMetadata(
                0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public double LS
    {
        get => (double)GetValue(LSProperty);
        set => SetValue(LSProperty, value);
    }

    // Lower HSV - V（明度）
    public static readonly DependencyProperty LVProperty =
        DependencyProperty.Register(
            nameof(LV),
            typeof(double),
            typeof(AdbEditorDialog),
            new FrameworkPropertyMetadata(
                0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public double LV
    {
        get => (double)GetValue(LVProperty);
        set => SetValue(LVProperty, value);
    }


    // ====================== Upper Gray ======================
    // Upper Gray（灰度值，常规范围0-255）
    public static readonly DependencyProperty UGrayProperty =
        DependencyProperty.Register(
            nameof(UGray),
            typeof(double),
            typeof(AdbEditorDialog),
            new FrameworkPropertyMetadata(
                0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public double UGray
    {
        get => (double)GetValue(UGrayProperty);
        set => SetValue(UGrayProperty, value);
    }


    // ====================== Lower Gray ======================
    // Lower Gray（灰度值）
    public static readonly DependencyProperty LGrayProperty =
        DependencyProperty.Register(
            nameof(LGray),
            typeof(double),
            typeof(AdbEditorDialog),
            new FrameworkPropertyMetadata(
                0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public double LGray
    {
        get => (double)GetValue(LGrayProperty);
        set => SetValue(LGrayProperty, value);
    }
    
    public static readonly DependencyProperty ColorTypeProperty =
        DependencyProperty.Register(
            nameof(ColorType),
            typeof(int),
            typeof(AdbEditorDialog),
            new FrameworkPropertyMetadata(
                0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public int ColorType
    {
        get => (int)GetValue(ColorTypeProperty);
        set => SetValue(ColorTypeProperty, value);
    }
}
