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
using Path = System.IO.Path;
using Point = System.Windows.Point;
using Color = System.Drawing.Color;
using DashStyle = System.Drawing.Drawing2D.DashStyle;
using Pen = System.Drawing.Pen;

namespace MFATools.Views;

public partial class ColorExtractionDialog
{
    // 原始图像（始终不变，用于恢复）
    private Bitmap _originBitmap;
    // 当前显示的图像（用于绘制矩形，每次刷新从_originBitmap复制）
    private Bitmap _displayBitmap;
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

    public ColorExtractionDialog()
    {
        InitializeComponent();
        // 加载原始图像（与CropImageDialog一致的逻辑）
        Task.Run(() =>
        {
            _originBitmap = MaaProcessor.Instance.GetBitmap();
            if (_originBitmap == null) return;

            // 初始化显示图像（复制原始图像）
            _displayBitmap = new Bitmap(_originBitmap);
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
    }

    // 更新图像显示（计算初始缩放，与CropImageDialog一致）
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

        // 计算初始缩放（适应窗口最大尺寸）
        double maxWidth = Math.Min(1280, SystemParameters.PrimaryScreenWidth - 100);
        double maxHeight = Math.Min(720, SystemParameters.PrimaryScreenHeight - 200);
        double widthRatio = maxWidth / _originWidth;
        double heightRatio = maxHeight / _originHeight;
        _scale = Math.Min(widthRatio, heightRatio);

        // 设置显示尺寸（基于缩放比例）
        image.Width = _originWidth * _scale;
        image.Height = _originHeight * _scale;

        // 窗口尺寸调整
        Width = image.Width + 40;
        Height = image.Height + 120;
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
        CenterWindow();
    }

    // 将屏幕坐标转换为实际像素坐标（与CropImageDialog一致）
    private (int X, int Y) ScreenToPixel(Point screenPos)
    {
        int x = (int)Math.Ceiling(screenPos.X);
        int y = (int)Math.Ceiling(screenPos.Y);
        // 边界限制
        x = Math.Clamp(x, 0, (int)_originWidth);
        y = Math.Clamp(y, 0, (int)_originHeight);
        return (x, y);
    }

    // 刷新显示（恢复原始图像并绘制矩形，核心方法）
    private void RefreshDisplay()
    {
        if (_originBitmap == null) return;

        // 从原始图像复制（清除之前的矩形）
        lock (_originBitmap)
        {
            _displayBitmap?.Dispose();
            _displayBitmap = new Bitmap(_originBitmap);
        }
        SelectionCanvas.Children.Clear();
        _selectionRectangle = null;
        // 如果有矩形，绘制到显示图像上
        if (_currentRect.HasValue)
        {
            var rect = _currentRect.Value;
            using (var g = Graphics.FromImage(_displayBitmap))
            {
                // 抗锯齿绘制（与CropImageDialog一致）
                g.SmoothingMode = SmoothingMode.None;
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                // 绘制虚线矩形
                using (var pen = new Pen(Color.FromArgb(SettingDialog.DefaultLineColor.Color.R,
                               SettingDialog.DefaultLineColor.Color.G,
                               SettingDialog.DefaultLineColor.Color.B),
                           SettingDialog.DefaultLineThickness))
                {
                    pen.DashStyle = DashStyle.Dash;
                    pen.DashPattern = [2, 2];
                    // 绘制时调整1px偏移，确保像素对齐
                    g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
                }
            }
        }

        // 更新UI显示
        image.Source = MFAExtensions.BitmapToBitmapImage(_displayBitmap);
    }

    // 窗口居中（与CropImageDialog一致）
    private void CenterWindow()
    {
        var screenWidth = SystemParameters.PrimaryScreenWidth;
        var screenHeight = SystemParameters.PrimaryScreenHeight;
        Left = (screenWidth - Width) / 2;
        Top = (screenHeight - Height) / 2;
    }

    // 鼠标滚轮缩放（与CropImageDialog一致）
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

            // 更新缩放比例
            sfr.ScaleX = scaleX;
            sfr.ScaleY = scaleY;
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
            // 检查边界
            CheckZoomBounds(mousePosition, scaleX, scaleY);
        }
    }

    // 检查缩放边界（与CropImageDialog一致）
    private void CheckZoomBounds(Point mousePosition, double scaleX, double scaleY)
    {
        double imageWidth = image.ActualWidth;
        double imageHeight = image.ActualHeight;

        if (mousePosition.X >= 0 && mousePosition.X <= imageWidth)
        {
            sfr.CenterX = mousePosition.X;
        }
        else
        {
            sfr.CenterX = imageWidth;
        }

        if (mousePosition.Y >= 0 && mousePosition.Y <= imageHeight)
        {
            sfr.CenterY = mousePosition.Y;
        }
        else
        {
            sfr.CenterY = imageHeight;
        }
    }

    // 鼠标按下（开始绘制矩形或拖动，与CropImageDialog一致）
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
            var canvasPosition = e.GetPosition(image);
            if (canvasPosition.X < image.ActualWidth + 5 && canvasPosition.Y < image.ActualHeight + 5 && canvasPosition is { X: > -5, Y: > -5 })
            {
                if (position.X < 0) position.X = 0;
                if (position.Y < 0) position.Y = 0;
                if (position.X > image.ActualWidth) position.X = image.ActualWidth;
                if (position.Y > image.ActualHeight) position.Y = image.ActualHeight;
                var (actualX, actualY) = ScreenToPixel(position);
                _startPoint = new Point(actualX, actualY);
                _currentRect = (actualX, actualY, 0, 0);
                RefreshDisplay(); // 初始刷新（清空之前的矩形）
                Mouse.Capture(image);
            }
        }
    }

    // 鼠标移动（更新矩形或拖动，与CropImageDialog一致）
    private void Canvas_MouseMove(object sender, MouseEventArgs e)
    {
        // 更新鼠标位置文本（像素坐标）
        var screenPos = e.GetPosition(image);
        var (pixelX, pixelY) = ScreenToPixel(screenPos);
        MousePositionText.Text = $"[ {pixelX}, {pixelY} ]";

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
            if (screenPos.X < 0) screenPos.X = 0;
            if (screenPos.Y < 0) screenPos.Y = 0;
            if (screenPos.X > image.ActualWidth) screenPos.X = image.ActualWidth;
            if (screenPos.Y > image.ActualHeight) screenPos.Y = image.ActualHeight;
            var (actualX, actualY) = ScreenToPixel(screenPos);
            double x = Math.Min(_startPoint.X, actualX);
            double y = Math.Min(_startPoint.Y, actualY);
            // 确保最小尺寸
            var w = Math.Max(Math.Abs(_startPoint.X - actualX), 1);
            var h = Math.Max(Math.Abs(_startPoint.Y - actualY), 1);

            _currentRect = (Convert.ToInt32(x), Convert.ToInt32(y), Convert.ToInt32(w), Convert.ToInt32(h));
            DrawRectangle(Convert.ToInt32(x), Convert.ToInt32(y), Convert.ToInt32(w), Convert.ToInt32(h));

            // 更新矩形坐标文本
            MousePositionText.Text = $"[ {Convert.ToInt32(x)}, {Convert.ToInt32(y)}, {Convert.ToInt32(w)}, {Convert.ToInt32(h)} ]";
        }
    }

    // 鼠标释放（与CropImageDialog一致）
    private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;
        RefreshDisplay(); // 刷新显示新矩形
        Mouse.Capture(null);
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

    // 提取颜色范围（核心业务逻辑，基于_originBitmap像素）
    private void ExtractColorRange(int x, int y, int width, int height)
    {
        if (_originBitmap == null) return;

        // 边界检查（与CropImageDialog一致）
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

        // 根据选择的类型提取颜色
        switch (SelectType.SelectedIndex)
        {
            case 0:
                GetRGBRange(x, y, width, height);
                break;
            case 1:
                GetHSVRange(x, y, width, height);
                break;
            case 2:
                GetGrayRange(x, y, width, height);
                break;
        }
    }

    // 提取RGB范围（基于_originBitmap像素）
    private void GetRGBRange(int x, int y, int width, int height)
    {
        int minR = 255, minG = 255, minB = 255;
        int maxR = 0, maxG = 0, maxB = 0;

        // 遍历选择区域像素（从原始图像提取，避免矩形干扰）
        for (int i = x; i < x + width; i++)
        {
            for (int j = y; j < y + height; j++)
            {
                System.Drawing.Color pixel = _originBitmap.GetPixel(i, j);
                minR = Math.Min(minR, pixel.R);
                minG = Math.Min(minG, pixel.G);
                minB = Math.Min(minB, pixel.B);
                maxR = Math.Max(maxR, pixel.R);
                maxG = Math.Max(maxG, pixel.G);
                maxB = Math.Max(maxB, pixel.B);
            }
        }

        OutputLower = new List<int>
        {
            minR,
            minG,
            minB
        };
        OutputUpper = new List<int>
        {
            maxR,
            maxG,
            maxB
        };
    }

    // 提取HSV范围
    private void GetHSVRange(int x, int y, int width, int height)
    {
        double minH = 360, minS = 1, minV = 1;
        double maxH = 0, maxS = 0, maxV = 0;

        for (int i = x; i < x + width; i++)
        {
            for (int j = y; j < y + height; j++)
            {
                Color pixel = _originBitmap.GetPixel(i, j);
                ColorToHSV(pixel, out double h, out double s, out double v);

                minH = Math.Min(minH, h);
                minS = Math.Min(minS, s);
                minV = Math.Min(minV, v);
                maxH = Math.Max(maxH, h);
                maxS = Math.Max(maxS, s);
                maxV = Math.Max(maxV, v);
            }
        }

        OutputLower = new List<int>
        {
            (int)Math.Round(minH),
            (int)Math.Round(minS * 100),
            (int)Math.Round(minV * 100)
        };
        OutputUpper = new List<int>
        {
            (int)Math.Round(maxH),
            (int)Math.Round(maxS * 100),
            (int)Math.Round(maxV * 100)
        };
    }

    // RGB转HSV
    private void ColorToHSV(System.Drawing.Color color, out double hue, out double saturation, out double value)
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

    // 提取灰度范围
    private void GetGrayRange(int x, int y, int width, int height)
    {
        int minGray = 255, maxGray = 0;

        for (int i = x; i < x + width; i++)
        {
            for (int j = y; j < y + height; j++)
            {
                System.Drawing.Color pixel = _originBitmap.GetPixel(i, j);
                int gray = (int)(0.299 * pixel.R + 0.587 * pixel.G + 0.114 * pixel.B);
                minGray = Math.Min(minGray, gray);
                maxGray = Math.Max(maxGray, gray);
            }
        }

        OutputLower = new List<int>
        {
            minGray
        };
        OutputUpper = new List<int>
        {
            maxGray
        };
    }

    // 加载图像（与CropImageDialog一致）
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
                // 替换原始图像
                _originBitmap?.Dispose();
                _originBitmap = new Bitmap(openFileDialog.FileName);
                _currentRect = null;
                RefreshDisplay();
                UpdateImage(MFAExtensions.BitmapToBitmapImage(_originBitmap));
            }
            catch (Exception ex)
            {
                new ErrorView(ex, false).Show();
            }
        }
    }
    private System.Windows.Shapes.Rectangle? _selectionRectangle; // 画布上的选择矩形
    // 绘制矩形（统一缩放与边界处理）
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

    // 编辑矩形（与CropImageDialog一致）
    private void Edit(object sender, RoutedEventArgs e)
    {
        var initialRect = _currentRect ?? (0, 0, 1, 1);
        var dialog = new RoiEditorDialog(initialRect);
        if (dialog.ShowDialog().IsTrue())
        {
            _currentRect = (dialog.X.ToNumber(), dialog.Y.ToNumber(), dialog.W.ToNumber(), dialog.H.ToNumber());
            RefreshDisplay();
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
