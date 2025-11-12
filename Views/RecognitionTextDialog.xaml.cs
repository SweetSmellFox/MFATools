using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using MFATools.Controls;
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

public partial class RecognitionTextDialog
{
    // 原始图像（始终不变，用于恢复）
    private Bitmap _originBitmap;
    // 当前显示的图像（用于绘制矩形，每次刷新从_originBitmap复制）
    private Bitmap _displayBitmap;
    // 矩形绘制参数（像素坐标）
    private (int X, int Y, int Width, int Height)? _currentRect;
    private Point _startPoint; // 起始像素坐标

    // 输出相关
    private List<int>? _output;
    public List<int>? Output
    {
        get => _output;
        set => _output = value?.Select(i => i < 0 ? 0 : i).ToList();
    }

    private List<int>? _outputRoi { get; set; }
    public List<int>? OutputRoi
    {
        get => _outputRoi;
        set => _outputRoi = value?.Select(i => i < 0 ? 0 : i).ToList();
    }

    // 缩放比例（屏幕显示尺寸 / 实际像素尺寸）
    private double _scale = 1.0;
    private double _originWidth;
    private double _originHeight;
    private const double ZoomFactor = 1.1;
    private Point _dragStartPoint;
    private bool _isDragging;

    public RecognitionTextDialog()
    {
        InitializeComponent();
        // 加载原始图像（与统一逻辑一致）
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

    // 更新图像显示（计算初始缩放，与统一逻辑一致）
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
        SelectionCanvas.Width = image.Width;
        SelectionCanvas.Height = image.Height;

        // 窗口尺寸调整
        Width = image.Width + 40;
        Height = image.Height + 120;
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
        CenterWindow();
    }

    // 屏幕坐标转像素坐标（修正缩放转换逻辑）
    private (int X, int Y) ScreenToPixel(Point screenPos)
    {
        // 关键修正：屏幕坐标（缩放后）需除以缩放比例得到像素坐标
        int x = (int)Math.Round(screenPos.X / _scale);
        int y = (int)Math.Round(screenPos.Y / _scale);
        // 边界限制
        x = Math.Clamp(x, 0, (int)_originWidth);
        y = Math.Clamp(y, 0, (int)_originHeight);
        return (x, y);
    }

    // 刷新显示（核心绘制逻辑：恢复原始图像+绘制矩形）
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

        // 绘制矩形（如果存在）
        if (_currentRect.HasValue)
        {
            var rect = _currentRect.Value;
            using (var g = Graphics.FromImage(_displayBitmap))
            {
                // 抗锯齿与像素对齐（统一样式）
                g.SmoothingMode = SmoothingMode.None;
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                // 绘制虚线矩形（与其他对话框样式一致）
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

    // 窗口居中（统一实现）
    public void CenterWindow()
    {
        var screenWidth = SystemParameters.PrimaryScreenWidth;
        var screenHeight = SystemParameters.PrimaryScreenHeight;
        Left = (screenWidth - Width) / 2;
        Top = (screenHeight - Height) / 2;
    }

    // 鼠标滚轮缩放（与统一逻辑一致）
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

    // 检查缩放边界（统一实现）
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

    // 鼠标按下（开始绘制矩形或拖动，统一逻辑）
    private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        var isCtrlKeyPressed = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
        if (e.LeftButton != MouseButtonState.Pressed)
            return;

        if (isCtrlKeyPressed)
        {
            // 拖拽模式
            _isDragging = true;
            _dragStartPoint = e.GetPosition(ImageArea);
            Mouse.Capture(ImageArea);
        }
        else
        {
            // 开始绘制矩形
            var position = e.GetPosition(image);
            var canvasPosition = e.GetPosition(SelectionCanvas);
            if (canvasPosition.X < image.ActualWidth + 5 && canvasPosition.Y < image.ActualHeight + 5 && 
                canvasPosition.X > -5 && canvasPosition.Y > -5)
            {
                // 限制初始位置在图像范围内
                position.X = Math.Clamp(position.X, 0, image.ActualWidth);
                position.Y = Math.Clamp(position.Y, 0, image.ActualHeight);

                var (actualX, actualY) = ScreenToPixel(position);
                _startPoint = new Point(actualX, actualY);
                _currentRect = (actualX, actualY, 0, 0);
                RefreshDisplay(); // 初始刷新（清空之前的矩形）
                Mouse.Capture(SelectionCanvas);
            }
        }
    }

    // 鼠标移动（更新矩形或拖动，统一逻辑：Canvas绘制临时矩形提升性能）
    private void Canvas_MouseMove(object sender, MouseEventArgs e)
    {
        // 更新鼠标像素坐标显示
        var screenPos = e.GetPosition(image);
        var (pixelX, pixelY) = ScreenToPixel(screenPos);
        MousePositionText.Text = $"[ {pixelX}, {pixelY} ]";

        var isCtrlKeyPressed = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
        if (_isDragging && isCtrlKeyPressed && e.LeftButton == MouseButtonState.Pressed)
        {
            // 处理拖拽
            var currentPos = e.GetPosition(ImageArea);
            var offsetX = currentPos.X - _dragStartPoint.X;
            var offsetY = currentPos.Y - _dragStartPoint.Y;

            var translateTransform = ttf;
            translateTransform.X += offsetX;
            translateTransform.Y += offsetY;

            _dragStartPoint = currentPos;
        }
        else if (_currentRect.HasValue && e.LeftButton == MouseButtonState.Pressed)
        {
            // 限制当前位置在图像范围内
            screenPos.X = Math.Clamp(screenPos.X, 0, image.ActualWidth);
            screenPos.Y = Math.Clamp(screenPos.Y, 0, image.ActualHeight);

            var (actualX, actualY) = ScreenToPixel(screenPos);
            // 计算矩形坐标（确保左上角为起点，最小尺寸1px）
            double x = Math.Min(_startPoint.X, actualX);
            double y = Math.Min(_startPoint.Y, actualY);
            var w = Math.Max(Math.Abs(_startPoint.X - actualX), 1);
            var h = Math.Max(Math.Abs(_startPoint.Y - actualY), 1);

            _currentRect = (Convert.ToInt32(x), Convert.ToInt32(y), Convert.ToInt32(w), Convert.ToInt32(h));
            DrawRectangle(Convert.ToInt32(x), Convert.ToInt32(y), Convert.ToInt32(w), Convert.ToInt32(h)); // 用Canvas绘制临时矩形

            // 更新矩形坐标文本
            MousePositionText.Text = $"[ {Convert.ToInt32(x)}, {Convert.ToInt32(y)}, {Convert.ToInt32(w)}, {Convert.ToInt32(h)} ]";
        }
    }

    // 鼠标释放（统一逻辑：确认矩形并刷新到图像）
    private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;
        RefreshDisplay(); // 鼠标抬起后刷新到Bitmap
        Mouse.Capture(null);
    }

    // 保存选择区域（适配_currentRect）
    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_currentRect.HasValue)
        {
            Growls.WarningGlobal("请选择一个区域");
            return;
        }

        var (x, y, w, h) = _currentRect.Value;
        // 边界检查（确保在原始图像范围内）
        x = Math.Clamp(x, 0, (int)_originWidth - 1);
        y = Math.Clamp(y, 0, (int)_originHeight - 1);
        w = Math.Clamp(w, 1, (int)_originWidth - x);
        h = Math.Clamp(h, 1, (int)_originHeight - y);

        Output = new List<int> { x, y, w, h };

        // 计算扩展ROI（保持原有业务逻辑）
        if (_originBitmap != null)
        {
            var roiX = Math.Max(x - (int)(MFAExtensions.HorizontalExpansion / 2), 0);
            var roiY = Math.Max(y - (int)(MFAExtensions.VerticalExpansion / 2), 0);
            var roiW = Math.Min(w + (int)(MFAExtensions.HorizontalExpansion), _originBitmap.Width - roiX);
            var roiH = Math.Min(h + (int)(MFAExtensions.VerticalExpansion), _originBitmap.Height - roiY);
            OutputRoi = new List<int> { roiX, roiY, roiW, roiH };
        }

        DialogResult = true;
        Close();
    }

    // 加载图像（统一逻辑）
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

    // 绘制矩形（修正坐标转换：像素→屏幕，统一缩放处理）
    private System.Windows.Shapes.Rectangle? _selectionRectangle; // 画布上的选择矩形
    public void DrawRectangle(int x, int y, int width, int height)
    {
        // 像素坐标边界检查
        x = Math.Clamp(x, 0, (int)_originWidth - 1);
        y = Math.Clamp(y, 0, (int)_originHeight - 1);
        width = Math.Clamp(width, 1, (int)_originWidth - x);
        height = Math.Clamp(height, 1, (int)_originHeight - y);

        // 清除之前的矩形
        if (_selectionRectangle != null)
            SelectionCanvas.Children.Remove(_selectionRectangle);

        // 转换像素坐标到屏幕坐标（乘以缩放比例）
        double screenX = x * _scale;
        double screenY = y * _scale;
        double screenWidth = width * _scale;
        double screenHeight = height * _scale;

        // 创建矩形（保持样式一致）
        _selectionRectangle = new System.Windows.Shapes.Rectangle
        {
            Stroke = SettingDialog.DefaultLineColor,
            StrokeThickness = SettingDialog.DefaultLineThickness,
            StrokeDashArray = { 2, 2 },
            Width = screenWidth,
            Height = screenHeight,
            RenderTransform = new TranslateTransform(screenX, screenY)
        };
        SelectionCanvas.Children.Add(_selectionRectangle);
    }

    // 编辑矩形（统一逻辑：更新后刷新显示）
    private void Edit(object sender, RoutedEventArgs e)
    {
        var initialRect = _currentRect ?? (0, 0, 1, 1);
        var dialog = new RoiEditorDialog(initialRect);
        if (dialog.ShowDialog().IsTrue())
        {
            _currentRect = (dialog.X.ToNumber(), dialog.Y.ToNumber(), dialog.W.ToNumber(), dialog.H.ToNumber());
            RefreshDisplay(); // 编辑后刷新到图像
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}