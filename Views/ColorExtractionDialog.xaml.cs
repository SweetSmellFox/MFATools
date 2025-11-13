using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MFATools.Utils;
using Microsoft.Win32;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Controls.Primitives;
using Path = System.IO.Path;
using Point = System.Windows.Point;
using Color = System.Drawing.Color;
using DashStyle = System.Drawing.Drawing2D.DashStyle;
using Image = System.Drawing.Image;
using Pen = System.Drawing.Pen;

namespace MFATools.Views;

public partial class ColorExtractionDialog
{
    // 原始图像（始终不变，用于恢复）
    private Bitmap _originBitmap;
    // 当前显示的图像（用于绘制矩形，每次刷新从_originBitmap复制）
    private Bitmap _displayBitmap;
    // 过滤后的图像（用于预览模式）
    private Bitmap _filteredBitmap;
    // 矩形绘制参数（像素坐标）
    private (int X, int Y, int Width, int Height)? _currentRect;
    private Point _startPoint; // 起始像素坐标

    // 输出相关
    private List<int>? _outputRoi { get; set; }

    public List<int>? OutputRoi
    {
        get => _outputRoi;
        set => _outputRoi = value?.Select(i => i < 0 ? 0 : i).ToList();
    }

    public List<int>? OutputUpper { get; set; }
    public List<int>? OutputLower { get; set; }

    // 缩放比例（屏幕显示尺寸 / 实际像素尺寸）
    private double _scale = 1.0;
    private double _originWidth;
    private double _originHeight;
    private const double ZoomFactor = 1.1;
    private Point _dragStartPoint;
    private bool _isDragging;
    // 预览模式状态
    private bool _isPreviewMode;
    // 临时存储实时计算的颜色范围
    private (List<int> Lower, List<int> Upper)? _tempColorRange;

    public ColorExtractionDialog()
    {
        InitializeComponent();
        // 初始化预览按钮文本
        SwitchButton.Content = "预览";
        // 加载原始图像
        Task.Run(() =>
        {
            _originBitmap = MaaProcessor.Instance.GetBitmap();
            if (_originBitmap == null) return;

            // 初始化显示图像
            _displayBitmap = new Bitmap(_originBitmap);
            _filteredBitmap = new Bitmap(_originBitmap);
            var imageSource = MFAExtensions.BitmapToBitmapImage(_displayBitmap);

            // 回到UI线程更新
            Dispatcher.Invoke(() => UpdateImage(imageSource));
        });
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _originBitmap?.Dispose();
        _displayBitmap?.Dispose();
        _filteredBitmap?.Dispose();
    }

    // 更新图像显示
    public void UpdateImage(BitmapImage? imageSource)
    {
        if (imageSource == null) return;

        LoadingCircle.Visibility = Visibility.Collapsed;
        ImageArea.Visibility = Visibility.Visible;
        image.Source = imageSource;
        image.SnapsToDevicePixels = true;
        SnapsToDevicePixels = true;

        _originWidth = imageSource.PixelWidth;
        _originHeight = imageSource.PixelHeight;

        // 计算初始缩放
        double maxWidth = Math.Min(1280, SystemParameters.PrimaryScreenWidth - 100);
        double maxHeight = Math.Min(720, SystemParameters.PrimaryScreenHeight - 200);
        double widthRatio = maxWidth / _originWidth;
        double heightRatio = maxHeight / _originHeight;
        _scale = Math.Min(widthRatio, heightRatio);

        // 设置显示尺寸
        image.Width = _originWidth * _scale;
        image.Height = _originHeight * _scale;

        // 窗口尺寸调整
        Width = image.Width + 40;
        Height = image.Height + 160; // 增加高度容纳更多文本
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
        CenterWindow();
    }

    // 屏幕坐标转像素坐标（修正缩放转换）
    private (int X, int Y) ScreenToPixel(Point screenPos)
    {
        int x = (int)Math.Round(screenPos.X / _scale);
        int y = (int)Math.Round(screenPos.Y / _scale);
        x = Math.Clamp(x, 0, (int)_originWidth);
        y = Math.Clamp(y, 0, (int)_originHeight);
        return (x, y);
    }

    // 刷新显示（支持预览模式）
    private void RefreshDisplay()
    {
        if (_originBitmap == null) return;

        // 从原始图像复制（清除之前的矩形）
        lock (_originBitmap)
        {
            _displayBitmap?.Dispose();
            // 根据预览模式选择显示图像
            _displayBitmap = _isPreviewMode && _filteredBitmap != null
                ? new Bitmap(_filteredBitmap)
                : new Bitmap(_originBitmap);
        }

        SelectionCanvas.Children.Clear();
        _selectionRectangle = null;

        // 绘制矩形
        if (_currentRect.HasValue)
        {
            var rect = _currentRect.Value;
            using (var g = Graphics.FromImage(_displayBitmap))
            {
                g.SmoothingMode = SmoothingMode.None;
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                using (var pen = new Pen(Color.FromArgb(SettingDialog.DefaultLineColor.Color.R,
                               SettingDialog.DefaultLineColor.Color.G,
                               SettingDialog.DefaultLineColor.Color.B),
                           SettingDialog.DefaultLineThickness))
                {
                    pen.DashStyle = DashStyle.Dash;
                    pen.DashPattern = [2, 2];
                    g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
                }
            }
        }

        // 更新UI显示
        image.Source = MFAExtensions.BitmapToBitmapImage(_displayBitmap);
    }

    // 窗口居中
    private void CenterWindow()
    {
        var screenWidth = SystemParameters.PrimaryScreenWidth;
        var screenHeight = SystemParameters.PrimaryScreenHeight;
        Left = (screenWidth - Width) / 2;
        Top = (screenHeight - Height) / 2;
    }

    // 鼠标滚轮缩放
    private void Dialog_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        var isCtrlKeyPressed = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
        if (isCtrlKeyPressed)
        {
            Point mousePosition = e.GetPosition(image);
            double scaleX = sfr.ScaleX;
            double scaleY = sfr.ScaleY;

            double factor = e.Delta > 0 ? ZoomFactor : 1 / ZoomFactor;
            scaleX *= factor;
            scaleY *= factor;

            sfr.ScaleX = scaleX;
            sfr.ScaleY = scaleY;
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
            CheckZoomBounds(mousePosition, scaleX, scaleY);
        }
    }

    // 检查缩放边界
    private void CheckZoomBounds(Point mousePosition, double scaleX, double scaleY)
    {
        double imageWidth = image.ActualWidth;
        double imageHeight = image.ActualHeight;

        sfr.CenterX = Math.Clamp(mousePosition.X, 0, imageWidth);
        sfr.CenterY = Math.Clamp(mousePosition.Y, 0, imageHeight);
    }

    // 鼠标按下
    private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        var isCtrlKeyPressed = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
        if (e.LeftButton != MouseButtonState.Pressed)
            return;

        if (isCtrlKeyPressed)
        {
            _isDragging = true;
            _dragStartPoint = e.GetPosition(ImageArea);
            Mouse.Capture(ImageArea);
        }
        else
        {
            // 开始绘制矩形
            var position = e.GetPosition(image);
            var canvasPosition = e.GetPosition(SelectionCanvas);
            if (canvasPosition.X < image.ActualWidth + 5 && canvasPosition.Y < image.ActualHeight + 5 && canvasPosition is { X: > -5, Y: > -5 })
            {
                position.X = Math.Clamp(position.X, 0, image.ActualWidth);
                position.Y = Math.Clamp(position.Y, 0, image.ActualHeight);
                var (actualX, actualY) = ScreenToPixel(position);
                _startPoint = new Point(actualX, actualY);
                _currentRect = (actualX, actualY, 0, 0);
                RefreshDisplay();
                Mouse.Capture(image);
            }
        }
    }

    // 鼠标移动（实时计算并显示颜色范围）
    private void Canvas_MouseMove(object sender, MouseEventArgs e)
    {
        // 更新鼠标位置文本（像素坐标）
        var screenPos = e.GetPosition(image);
        var (pixelX, pixelY) = ScreenToPixel(screenPos);

        string positionText = $"[ {pixelX}, {pixelY} ]";

        // 实时计算颜色范围并更新文本
        var isCtrlKeyPressed = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
        if (_isDragging && isCtrlKeyPressed && e.LeftButton == MouseButtonState.Pressed)
        {
            var Dposition = e.GetPosition(ImageArea);
            Point previousPosition = _dragStartPoint;
            if (previousPosition == new Point(0, 0))
            {
                previousPosition = Dposition;
            }

            double offsetX = Dposition.X - previousPosition.X;
            double offsetY = Dposition.Y - previousPosition.Y;

            // 使用 Transform 类实现拖动
            var translateTransform = ttf;
            translateTransform.X += offsetX;
            translateTransform.Y += offsetY;

            _dragStartPoint = Dposition;
        }
        else if (_currentRect.HasValue && e.LeftButton == MouseButtonState.Pressed)
        {
            // 限制当前位置在图像范围内
            screenPos.X = Math.Clamp(screenPos.X, 0, image.ActualWidth);
            screenPos.Y = Math.Clamp(screenPos.Y, 0, image.ActualHeight);
            var (actualX, actualY) = ScreenToPixel(screenPos);

            // 计算矩形坐标
            double x = Math.Min(_startPoint.X, actualX);
            double y = Math.Min(_startPoint.Y, actualY);
            var w = Math.Max(Math.Abs(_startPoint.X - actualX), 1);
            var h = Math.Max(Math.Abs(_startPoint.Y - actualY), 1);
            _currentRect = (Convert.ToInt32(x), Convert.ToInt32(y), Convert.ToInt32(w), Convert.ToInt32(h));

            // 绘制临时矩形
            DrawRectangle(Convert.ToInt32(x), Convert.ToInt32(y), Convert.ToInt32(w), Convert.ToInt32(h));

            // 更新坐标文本为矩形区域
            positionText = $" [ {x}, {y}, {w}, {h} ]";

            // 实时计算颜色范围
            if (_originBitmap != null)
            {
                var (lower, upper) = CalculateColorRange(
                    Convert.ToInt32(x), Convert.ToInt32(y),
                    Convert.ToInt32(w), Convert.ToInt32(h));
                _tempColorRange = (lower, upper);

                // 生成颜色范围文本（换行显示）
                string colorType = SelectType.SelectedIndex switch
                {
                    0 => "RGB",
                    1 => "HSV",
                    2 => "Gray",
                    _ => ""
                };

                string rangeText = $"{colorType}:{Environment.NewLine}";
                rangeText += $"Lower: {string.Join(", ", lower)}{Environment.NewLine}";
                rangeText += $"Upper: {string.Join(", ", upper)}";

                // 合并显示文本
                MousePositionText.Text = $"{positionText}{Environment.NewLine}{rangeText}";
            }
        }
        else
        {
            // 仅显示坐标
            if (_tempColorRange != null)
            {
                string colorType = SelectType.SelectedIndex switch
                {
                    0 => "RGB",
                    1 => "HSV",
                    2 => "Gray",
                    _ => ""
                };

                string rangeText = $"{colorType}:{Environment.NewLine}";
                rangeText += $"Lower: {string.Join(", ", _tempColorRange.Value.Lower)}{Environment.NewLine}";
                rangeText += $"Upper: {string.Join(", ", _tempColorRange.Value.Upper)}";
                positionText += $"{Environment.NewLine}{rangeText}";
            }
            MousePositionText.Text = positionText;

        }
    }

    // 实时计算颜色范围
    private (List<int> Lower, List<int> Upper) CalculateColorRange(int x, int y, int width, int height)
    {
        // 边界检查
        x = Math.Clamp(x, 0, _originBitmap.Width - 1);
        y = Math.Clamp(y, 0, _originBitmap.Height - 1);
        width = Math.Clamp(width, 1, _originBitmap.Width - x);
        height = Math.Clamp(height, 1, _originBitmap.Height - y);

        switch (SelectType.SelectedIndex)
        {
            case 0:
                return CalculateRGBRange(x, y, width, height);
            case 1:
                return CalculateHSVRange(x, y, width, height);
            case 2:
                return CalculateGrayRange(x, y, width, height);
            default:
                return (new List<int>(), new List<int>());
        }
    }

    // 计算RGB范围
    private (List<int> Lower, List<int> Upper) CalculateRGBRange(int x, int y, int width, int height)
    {
        int minR = 255, minG = 255, minB = 255;
        int maxR = 0, maxG = 0, maxB = 0;

        for (int i = x; i < x + width; i++)
        {
            for (int j = y; j < y + height; j++)
            {
                Color pixel = _originBitmap.GetPixel(i, j);
                minR = Math.Min(minR, pixel.R);
                minG = Math.Min(minG, pixel.G);
                minB = Math.Min(minB, pixel.B);
                maxR = Math.Max(maxR, pixel.R);
                maxG = Math.Max(maxG, pixel.G);
                maxB = Math.Max(maxB, pixel.B);
            }
        }

        return (
            new List<int>
            {
                minR,
                minG,
                minB
            },
            new List<int>
            {
                maxR,
                maxG,
                maxB
            }
        );
    }

    // 计算HSV范围
    private (List<int> Lower, List<int> Upper) CalculateHSVRange(int x, int y, int width, int height)
    {
        int minH = 180, minS = 255, minV = 255;
        int maxH = 0, maxS = 0, maxV = 0;

        for (int i = x; i < x + width; i++)
        {
            for (int j = y; j < y + height; j++)
            {
                Color pixel = _originBitmap.GetPixel(i, j);
                ColorToHSV(pixel, out double h, out double s, out double v);

                int cvH = (int)Math.Round(h / 2);
                int cvS = (int)Math.Round(s * 255);
                int cvV = (int)Math.Round(v * 255);

                cvH = Math.Clamp(cvH, 0, 180);
                cvS = Math.Clamp(cvS, 0, 255);
                cvV = Math.Clamp(cvV, 0, 255);

                minH = Math.Min(minH, cvH);
                minS = Math.Min(minS, cvS);
                minV = Math.Min(minV, cvV);
                maxH = Math.Max(maxH, cvH);
                maxS = Math.Max(maxS, cvS);
                maxV = Math.Max(maxV, cvV);
            }
        }

        return (
            new List<int>
            {
                minH,
                minS,
                minV
            },
            new List<int>
            {
                maxH,
                maxS,
                maxV
            }
        );
    }

    // 计算灰度范围
    private (List<int> Lower, List<int> Upper) CalculateGrayRange(int x, int y, int width, int height)
    {
        int minGray = 255, maxGray = 0;

        for (int i = x; i < x + width; i++)
        {
            for (int j = y; j < y + height; j++)
            {
                Color pixel = _originBitmap.GetPixel(i, j);
                int gray = (int)(0.299 * pixel.R + 0.587 * pixel.G + 0.114 * pixel.B);
                minGray = Math.Min(minGray, gray);
                maxGray = Math.Max(maxGray, gray);
            }
        }

        return (
            new List<int>
            {
                minGray
            },
            new List<int>
            {
                maxGray
            }
        );
    }

    // 鼠标释放
    private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;
        RefreshDisplay();
        Mouse.Capture(null);

        // 鼠标释放时保存最终颜色范围
        if (_currentRect.HasValue && _tempColorRange.HasValue)
        {
            OutputLower = _tempColorRange.Value.Lower;
            OutputUpper = _tempColorRange.Value.Upper;
        }
    }

    // 保存颜色提取结果
    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_currentRect.HasValue)
        {
            Growls.WarningGlobal("请选择一个区域");
            return;
        }

        var (x, y, width, height) = _currentRect.Value;
        ExtractColorRange(x, y, width, height);
        DialogResult = true;
        Close();
    }

    // 提取颜色范围（核心业务逻辑）
    private void ExtractColorRange(int x, int y, int width, int height)
    {
        if (_originBitmap == null) return;

        // 边界检查
        x = Math.Clamp(x, 0, _originBitmap.Width - 1);
        y = Math.Clamp(y, 0, _originBitmap.Height - 1);
        width = Math.Clamp(width, 1, _originBitmap.Width - x);
        height = Math.Clamp(height, 1, _originBitmap.Height - y);

        // 计算扩展ROI
        int roiX = Math.Max(x - 5, 0);
        int roiY = Math.Max(y - 5, 0);
        int roiW = Math.Min(width + 10, _originBitmap.Width - roiX);
        int roiH = Math.Min(height + 10, _originBitmap.Height - roiY);
        OutputRoi = new List<int>
        {
            roiX,
            roiY,
            roiW,
            roiH
        };

        // 使用已计算的颜色范围
        if (!_tempColorRange.HasValue)
        {
            // 未计算过则重新计算
            var (lower, upper) = CalculateColorRange(x, y, width, height);
            OutputLower = lower;
            OutputUpper = upper;
        }
    }

    // RGB转HSV
    private void ColorToHSV(Color color, out double hue, out double saturation, out double value)
    {
        double r = color.R / 255.0;
        double g = color.G / 255.0;
        double b = color.B / 255.0;

        double cMax = Math.Max(r, Math.Max(g, b));
        double cMin = Math.Min(r, Math.Min(g, b));
        double delta = cMax - cMin;

        value = cMax;

        if (delta == 0)
        {
            hue = 0;
            saturation = 0;
        }
        else
        {
            saturation = delta / cMax;

            hue = cMax switch
            {
                var _ when r == cMax => ((g - b) / delta) % 6,
                var _ when g == cMax => (b - r) / delta + 2,
                _ => (r - g) / delta + 4
            };

            hue *= 60;
            if (hue < 0) hue += 360;
        }
    }

    // 加载图像
    private void Load(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new OpenFileDialog
        {
            Title = "LoadImageTitle".GetLocalizationString(),
            Filter = "ImageFilter".GetLocalizationString()
        };

        if (openFileDialog.ShowDialog() == true)
        {
            try
            {
                _originBitmap?.Dispose();
                _originBitmap = new Bitmap(openFileDialog.FileName);
                _currentRect = null;
                _tempColorRange = null;
                _isPreviewMode = false;
                SwitchButton.Content = "预览";

                _displayBitmap?.Dispose();
                _displayBitmap = new Bitmap(_originBitmap);
                _filteredBitmap?.Dispose();
                _filteredBitmap = new Bitmap(_originBitmap);

                RefreshDisplay();
                UpdateImage(MFAExtensions.BitmapToBitmapImage(_originBitmap));
            }
            catch (Exception ex)
            {
                new ErrorView(ex, false).Show();
            }
        }
    }

    private System.Windows.Shapes.Rectangle? _selectionRectangle;
    // 绘制矩形（统一缩放处理）
    public void DrawRectangle(int x, int y, int width, int height)
    {
        // 像素坐标边界检查
        x = Math.Clamp(x, 0, (int)_originWidth - 1);
        y = Math.Clamp(y, 0, (int)_originHeight - 1);
        width = Math.Clamp(width, 1, (int)_originWidth - x) + 1;
        height = Math.Clamp(height, 1, (int)_originHeight - y) + 1;

        // 清除之前的矩形
        if (_selectionRectangle != null)
            SelectionCanvas.Children.Remove(_selectionRectangle);

        // 创建矩形（保持样式一致）
        _selectionRectangle = new System.Windows.Shapes.Rectangle
        {
            Stroke = SettingDialog.DefaultLineColor,
            StrokeThickness = SettingDialog.DefaultLineThickness,
            StrokeDashArray =
            {
                2,
                2
            },
            Width = width,
            Height = height,
            RenderTransform = new TranslateTransform(x - 1, y - 1)
        };
        SelectionCanvas.Children.Add(_selectionRectangle);
    }

    // 编辑矩形
    private void Edit(object sender, RoutedEventArgs e)
    {
        var initialRect = _currentRect ?? (0, 0, 1, 1);
        var dialog = new RoiEditorDialog(initialRect)
        {
            HasColor = true,
            ColorType = SelectType.SelectedIndex
        };
        switch (SelectType.SelectedIndex)
        {
            case 0: // RGB
                if (_tempColorRange != null)
                {
                    // 赋值 RGB Upper 通道（R/G/B）
                    dialog.UR = _tempColorRange.Value.Upper.Count > 0 ? _tempColorRange.Value.Upper[0] : 0; // R
                    dialog.UG = _tempColorRange.Value.Upper.Count > 1 ? _tempColorRange.Value.Upper[1] : 0; // G
                    dialog.UB = _tempColorRange.Value.Upper.Count > 2 ? _tempColorRange.Value.Upper[2] : 0; // B

                    // 赋值 RGB Lower 通道（R/G/B）
                    dialog.LR = _tempColorRange.Value.Lower.Count > 0 ? _tempColorRange.Value.Lower[0] : 0; // R
                    dialog.LG = _tempColorRange.Value.Lower.Count > 1 ? _tempColorRange.Value.Lower[1] : 0; // G
                    dialog.LB = _tempColorRange.Value.Lower.Count > 2 ? _tempColorRange.Value.Lower[2] : 0; // B
                }
                break;

            case 1: // HSV
                if (_tempColorRange != null)
                {
                    // 赋值 HSV Upper 通道（H/S/V）
                    dialog.UH = _tempColorRange.Value.Upper.Count > 0 ? _tempColorRange.Value.Upper[0] : 0; // H（色相）
                    dialog.US = _tempColorRange.Value.Upper.Count > 1 ? _tempColorRange.Value.Upper[1] : 0; // S（饱和度）
                    dialog.UV = _tempColorRange.Value.Upper.Count > 2 ? _tempColorRange.Value.Upper[2] : 0; // V（明度）

                    // 赋值 HSV Lower 通道（H/S/V）
                    dialog.LH = _tempColorRange.Value.Lower.Count > 0 ? _tempColorRange.Value.Lower[0] : 0; // H
                    dialog.LS = _tempColorRange.Value.Lower.Count > 1 ? _tempColorRange.Value.Lower[1] : 0; // S
                    dialog.LV = _tempColorRange.Value.Lower.Count > 2 ? _tempColorRange.Value.Lower[2] : 0; // V
                }
                break;

            case 2: // 灰度
                if (_tempColorRange != null)
                {
                    // 灰度只有一个通道，直接赋值 Upper 和 Lower
                    dialog.UGray = _tempColorRange.Value.Upper.Count > 0 ? _tempColorRange.Value.Upper[0] : 0; //  Upper Gray
                    dialog.LGray = _tempColorRange.Value.Lower.Count > 0 ? _tempColorRange.Value.Lower[0] : 0; //  Lower Gray
                }
                break;
        }
        if (dialog.ShowDialog().IsTrue())
        {
            if (dialog.EditColor)
            {
                switch (SelectType.SelectedIndex)
                {
                    case 0: // RGB
                        // 新建 RGB 对应的 Lower 和 Upper 列表（3个通道：R/G/B）
                        var rgbLower = new List<int>
                        {
                            (int)dialog.LR, // 转换为 int（原属性是 double）
                            (int)dialog.LG,
                            (int)dialog.LB
                        };
                        var rgbUpper = new List<int>
                        {
                            (int)dialog.UR,
                            (int)dialog.UG,
                            (int)dialog.UB
                        };
                        // 赋值给 _tempColorRange（直接创建新元组）
                        _tempColorRange = (rgbLower, rgbUpper);
                        break;

                    case 1: // HSV
                        // 新建 HSV 对应的 Lower 和 Upper 列表（3个通道：H/S/V）
                        var hsvLower = new List<int>
                        {
                            (int)dialog.LH,
                            (int)dialog.LS,
                            (int)dialog.LV
                        };
                        var hsvUpper = new List<int>
                        {
                            (int)dialog.UH,
                            (int)dialog.US,
                            (int)dialog.UV
                        };
                        _tempColorRange = (hsvLower, hsvUpper);
                        break;

                    case 2: // 灰度
                        // 新建灰度对应的 Lower 和 Upper 列表（1个通道）
                        var grayLower = new List<int>
                        {
                            (int)dialog.LGray
                        };
                        var grayUpper = new List<int>
                        {
                            (int)dialog.UGray
                        };
                        _tempColorRange = (grayLower, grayUpper);
                        break;
                }
                string colorType = SelectType.SelectedIndex switch
                {
                    0 => "RGB",
                    1 => "HSV",
                    2 => "Gray",
                    _ => ""
                };
                var positionText = _currentRect != null ? $"[ {_currentRect.Value.X}, {_currentRect.Value.Y}, {_currentRect.Value.Width}, {_currentRect.Value.Height} ]{Environment.NewLine}" : "";
                string rangeText = $"{colorType}:{Environment.NewLine}";
                rangeText += $"Lower: {string.Join(", ", _tempColorRange.Value.Lower)}{Environment.NewLine}";
                rangeText += $"Upper: {string.Join(", ", _tempColorRange.Value.Upper)}";

                // 合并显示文本
                MousePositionText.Text = $"{positionText}{rangeText}";
            }
            else
            {
                _currentRect = (dialog.X.ToNumber(), dialog.Y.ToNumber(), dialog.W.ToNumber(), dialog.H.ToNumber());
                // 重新计算颜色范围
                if (_originBitmap != null && _currentRect.HasValue)
                {
                    var (x, y, w, h) = _currentRect.Value;
                    _tempColorRange = CalculateColorRange(x, y, w, h);
                }
                RefreshDisplay();
            }

        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    // 预览/还原切换按钮
    private void SwitchButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton toggleButton) return;

        // 切换预览模式
        _isPreviewMode = !_isPreviewMode;

        if (_isPreviewMode && (_tempColorRange != null || (OutputLower != null && OutputUpper != null)))
        {
            // 进入预览模式：应用颜色过滤
            toggleButton.Content = "Restore".GetLocalizationString();
            ApplyColorFilter();
        }
        else
        {
            // 退出预览模式：显示原图
            toggleButton.Content = "Preview".GetLocalizationString();
            RefreshDisplay();
        }
    }

    // 应用颜色过滤（用于预览）
    // 应用颜色过滤（用于预览）
    private void ApplyColorFilter()
    {
        if (_originBitmap == null || (!_tempColorRange.HasValue && (OutputLower == null || OutputUpper == null)))
            return;

        // 获取当前有效的颜色范围
        var (lower, upper) = _tempColorRange ?? (OutputLower, OutputUpper);
        if (lower == null || upper == null) return;

        // 复制原始图像用于过滤
        lock (_originBitmap)
        {
            _filteredBitmap?.Dispose();
            _filteredBitmap = new Bitmap(_originBitmap);
        }

        // 锁定位图数据提高性能
        Rectangle rect = new Rectangle(0, 0, _filteredBitmap.Width, _filteredBitmap.Height);
        BitmapData bmpData = _filteredBitmap.LockBits(rect, ImageLockMode.ReadWrite, _filteredBitmap.PixelFormat);
        IntPtr ptr = bmpData.Scan0;
        int bytes = Math.Abs(bmpData.Stride) * _filteredBitmap.Height;
        byte[] rgbValues = new byte[bytes];
        System.Runtime.InteropServices.Marshal.Copy(ptr, rgbValues, 0, bytes);

        // 关键修复：根据像素格式计算每个像素的字节数（24位=3字节，32位=4字节）
        int bytesPerPixel = Image.GetPixelFormatSize(_filteredBitmap.PixelFormat) / 8;

        // 根据颜色类型应用过滤（传入字节步长）
        switch (SelectType.SelectedIndex)
        {
            case 0: // RGB过滤
                ApplyRgbFilter(rgbValues, bytesPerPixel, lower, upper);
                break;
            case 1: // HSV过滤
                ApplyHsvFilter(rgbValues, bytesPerPixel, lower, upper);
                break;
            case 2: // 灰度过滤
                ApplyGrayFilter(rgbValues, bytesPerPixel, lower, upper);
                break;
        }

        // 复制回数据并解锁
        System.Runtime.InteropServices.Marshal.Copy(rgbValues, 0, ptr, bytes);
        _filteredBitmap.UnlockBits(bmpData);

        // 刷新显示过滤后的图像
        RefreshDisplay();
    }

// RGB过滤实现（修复步长和像素读取）
    private void ApplyRgbFilter(byte[] rgbValues, int bytesPerPixel, List<int> lower, List<int> upper)
    {
        if (lower.Count < 3 || upper.Count < 3) return;
        int rMin = lower[0], gMin = lower[1], bMin = lower[2];
        int rMax = upper[0], gMax = upper[1], bMax = upper[2];

        // 按实际像素字节数循环（修复步长）
        for (int i = 0; i < rgbValues.Length; i += bytesPerPixel)
        {
            // 正确读取BGR值（适配24/32位格式）
            byte b = rgbValues[i];
            byte g = rgbValues[i + 1];
            byte r = rgbValues[i + 2];

            // 不在范围内的像素设为黑色（保留Alpha通道）
            if (r < rMin || r > rMax || g < gMin || g > gMax || b < bMin || b > bMax)
            {
                rgbValues[i] = 0; // B
                rgbValues[i + 1] = 0; // G
                rgbValues[i + 2] = 0; // R
                // 32位格式的Alpha通道不修改，保持原样
            }
        }
    }

// HSV过滤实现（修复步长和像素读取）
    private void ApplyHsvFilter(byte[] rgbValues, int bytesPerPixel, List<int> lower, List<int> upper)
    {
        if (lower.Count < 3 || upper.Count < 3) return;
        int hMin = lower[0], sMin = lower[1], vMin = lower[2];
        int hMax = upper[0], sMax = upper[1], vMax = upper[2];

        for (int i = 0; i < rgbValues.Length; i += bytesPerPixel)
        {
            byte b = rgbValues[i];
            byte g = rgbValues[i + 1];
            byte r = rgbValues[i + 2];

            ColorToHSV(Color.FromArgb(r, g, b), out double h, out double s, out double v);
            int cvH = (int)Math.Round(h / 2); // 转换为0-180范围（适配OpenCV）
            int cvS = (int)Math.Round(s * 255); // 转换为0-255范围
            int cvV = (int)Math.Round(v * 255);

            // 不在范围内的像素设为黑色
            if (cvH < hMin || cvH > hMax || cvS < sMin || cvS > sMax || cvV < vMin || cvV > vMax)
            {
                rgbValues[i] = 0; // B
                rgbValues[i + 1] = 0; // G
                rgbValues[i + 2] = 0; // R
            }
        }
    }

// 灰度过滤实现（修复步长和像素读取）
    private void ApplyGrayFilter(byte[] rgbValues, int bytesPerPixel, List<int> lower, List<int> upper)
    {
        if (lower.Count < 1 || upper.Count < 1) return;
        int grayMin = lower[0];
        int grayMax = upper[0];

        for (int i = 0; i < rgbValues.Length; i += bytesPerPixel)
        {
            byte b = rgbValues[i];
            byte g = rgbValues[i + 1];
            byte r = rgbValues[i + 2];
            // 正确计算灰度值（符合 luminance 标准公式）
            int gray = (int)(0.299 * r + 0.587 * g + 0.114 * b);

            // 不在范围内的像素设为黑色（保留原色彩像素，仅过滤灰度超范围的）
            if (gray < grayMin || gray > grayMax)
            {
                rgbValues[i] = 0; // B
                rgbValues[i + 1] = 0; // G
                rgbValues[i + 2] = 0; // R
            }
        }
    }
    private void SelectType_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _tempColorRange = null;
        string colorType = SelectType.SelectedIndex switch
        {
            0 => "RGB",
            1 => "HSV",
            2 => "Gray",
            _ => ""
        };
        var positionText = _currentRect != null ? $"[ {_currentRect.Value.X}, {_currentRect.Value.Y}, {_currentRect.Value.Width}, {_currentRect.Value.Height} ]{Environment.NewLine}" : "";
        string rangeText = "";
        if (_tempColorRange != null)
        {
            rangeText = $"{colorType}:{Environment.NewLine}";
            rangeText += $"Lower: {string.Join(", ", _tempColorRange.Value.Lower)}{Environment.NewLine}";
            rangeText += $"Upper: {string.Join(", ", _tempColorRange.Value.Upper)}";
        }
        // 合并显示文本
        MousePositionText.Text = $"{positionText}{rangeText}";
    }
}
