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

public partial class CropImageDialog
{
    // 原始图像（始终不变，用于恢复）
    private Bitmap? _originBitmap;
    // 矩形绘制参数（像素坐标）
    private (int X, int Y, int Width, int Height)? _currentRect;
    private Point _startPoint; // 起始像素坐标

    public string? Output { get; set; }
    private List<int>? _outputRoi { get; set; }

    public List<int>? OutputRoi
    {
        get => _outputRoi;
        set => _outputRoi = value?.Select(i => i < 0 ? 0 : i).ToList();
    }

    private List<int>? _outputOriginRoi { get; set; }

    public List<int>? OutputOriginRoi
    {
        get => _outputOriginRoi;
        set => _outputOriginRoi = value?.Select(i => i < 0 ? 0 : i).ToList();
    }


    // 缩放比例（屏幕显示尺寸 / 实际像素尺寸）
    private double _scale = 1.0;
    private double _originWidth;
    private double _originHeight;
    private const double ZoomFactor = 1.1;
    private Point _dragStartPoint;
    private bool _isDragging;

    public CropImageDialog()
    {
        InitializeComponent();
        // 加载原始图像
        Task.Run(() =>
        {
            _originBitmap = MaaProcessor.Instance.GetBitmap();
            if (_originBitmap == null) return;

            // 回到UI线程更新
            Dispatcher.Invoke(() =>
            {
                UpdateImage();
                RefreshDisplay();
            });
        });
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _originBitmap?.Dispose();
    }

    // 更新图像显示（计算初始缩放）
    public void UpdateImage()
    {
        if (_originBitmap == null) return;

        LoadingCircle.Visibility = Visibility.Collapsed;
        ImageArea.Visibility = Visibility.Visible;

        image.SnapsToDevicePixels = true;
        SnapsToDevicePixels = true;

        // 更新原始尺寸
        _originWidth = _originBitmap.Width;
        _originHeight = _originBitmap.Height;

        // 重新计算缩放比例
        double maxWidth = Math.Min(1280, SystemParameters.PrimaryScreenWidth - 100);
        double maxHeight = Math.Min(720, SystemParameters.PrimaryScreenHeight - 200);
        double widthRatio = maxWidth / _originWidth;
        double heightRatio = maxHeight / _originHeight;
        _scale = Math.Min(widthRatio, heightRatio);

        // 更新显示尺寸
        image.Width = _originWidth * _scale;
        image.Height = _originHeight * _scale;

        // 窗口调整
        Width = image.Width + 40;
        Height = image.Height + 160;
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
        CenterWindow();
    }

    // 将屏幕坐标转换为实际像素坐标
    private (int X, int Y) ScreenToPixel(Point screenPos)
    {
        // 关键：屏幕坐标 ÷ 缩放比例 = 实际像素坐标
        int x = (int)Math.Ceiling(screenPos.X / _scale);
        int y = (int)Math.Ceiling(screenPos.Y / _scale);
        x = Math.Clamp(x, 0, (int)_originWidth);
        y = Math.Clamp(y, 0, (int)_originHeight);
        return (x, y);
    }

    private WriteableBitmap? _displayWriteableBitmap;
    // 刷新显示（恢复原始图像并绘制矩形）
    private void RefreshDisplay()
    {
        if (_originBitmap == null) return;

        // 首次初始化或尺寸变化时，创建/重置 WriteableBitmap（仅执行一次或尺寸变化时）
        if (_displayWriteableBitmap == null
            || _displayWriteableBitmap.PixelWidth != _originBitmap.Width
            || _displayWriteableBitmap.PixelHeight != _originBitmap.Height)
        {
            // 初始化 WriteableBitmap（与原始图像尺寸一致，格式用 Bgra32 兼容 WPF）
            _displayWriteableBitmap = new WriteableBitmap(
                _originBitmap.Width,
                _originBitmap.Height,
                96, 96,
                PixelFormats.Bgra32,
                null);

            // 仅首次赋值一次 image.Source（后续不再修改 Source）
            image.Source = _displayWriteableBitmap;
        }

        using (var tempBitmap = new Bitmap(_originBitmap))
        {
            // 绘制矩形（如果需要）
            if (_currentRect.HasValue)
            {
                var rect = _currentRect.Value;
                using (var g = Graphics.FromImage(tempBitmap))
                {
                    g.SmoothingMode = SmoothingMode.None;
                    g.InterpolationMode = InterpolationMode.NearestNeighbor;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                    using (var pen = new Pen(
                               Color.FromArgb(
                                   SettingDialog.DefaultLineColor.Color.R,
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

            // 将临时 Bitmap 的像素复制到 WriteableBitmap（核心优化：只更新像素，不换 Source）
            tempBitmap.UpdateWriteableBitmap(_displayWriteableBitmap);
        }

        // 清除选择画布（原有逻辑保留）
        SelectionCanvas.Children.Clear();
        _selectionRectangle = null;
    }

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

            // 更新缩放比例
            sfr.ScaleX = scaleX;
            sfr.ScaleY = scaleY;
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
            // 检查边界
            CheckZoomBounds(mousePosition, scaleX, scaleY);
        }
    }
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

    // 鼠标按下（开始绘制矩形或拖动）
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

    // 鼠标移动（更新矩形或拖动）
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
            // DrawRectangle(Convert.ToInt32(x), Convert.ToInt32(y), Convert.ToInt32(w), Convert.ToInt32(h));
            RefreshDisplay();
            // 更新矩形坐标文本
            MousePositionText.Text = $"[ {Convert.ToInt32(x)}, {Convert.ToInt32(y)}, {Convert.ToInt32(w)}, {Convert.ToInt32(h)} ]";
        }
    }

    // 鼠标释放
    private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;
        RefreshDisplay(); // 刷新显示新矩形
        Mouse.Capture(null);
    }

    // 保存裁剪区域
    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_currentRect.HasValue)
        {
            Growls.WarningGlobal("请选择一个区域");
            return;
        }

        var (x, y, width, height) = _currentRect.Value;
        SaveCroppedImage(x, y, width, height);
    }

    // 裁剪并保存图像
    private void SaveCroppedImage(int x, int y, int width, int height)
    {
        if (_originBitmap == null) return;

        // 边界检查
        x = Math.Clamp(x, 0, _originBitmap.Width - 1);
        y = Math.Clamp(y, 0, _originBitmap.Height - 1);
        width = Math.Clamp(width, 1, _originBitmap.Width - x);
        height = Math.Clamp(height, 1, _originBitmap.Height - y);

        // 计算原始ROI和扩展ROI
        OutputOriginRoi =
        [
            x,
            y,
            width,
            height
        ];
        int roiX = Math.Max(x - MFAExtensions.HorizontalExpansion / 2, 0); // 原代码中100/2=50
        int roiY = Math.Max(y - MFAExtensions.VerticalExpansion / 2, 0);
        int roiW = Math.Min(width + MFAExtensions.HorizontalExpansion, _originBitmap.Width - roiX);
        int roiH = Math.Min(height + MFAExtensions.VerticalExpansion, _originBitmap.Height - roiY);
        OutputRoi =
        [
            roiX,
            roiY,
            roiW,
            roiH
        ];

        try
        {
            // 从原始图像裁剪（避免包含绘制的矩形）
            using var croppedBitmap = _originBitmap.Clone(
                new System.Drawing.Rectangle(x, y, width, height),
                _originBitmap.PixelFormat
            );

            // 保存文件
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "ImageFilter".GetLocalizationString(),
                DefaultExt = "png"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                croppedBitmap.Save(saveFileDialog.FileName, GetImageFormat(saveFileDialog.FileName));
                Output = Path.GetFileName(saveFileDialog.FileName);
                DialogResult = true;
                Close();
            }
        }
        catch (Exception ex)
        {
            Growls.ErrorGlobal($"保存失败：{ex.Message}");
        }
    }

    // 根据文件扩展名获取图像格式
    private System.Drawing.Imaging.ImageFormat GetImageFormat(string fileName)
    {
        return Path.GetExtension(fileName).ToLower() switch
        {
            ".jpg" or ".jpeg" => System.Drawing.Imaging.ImageFormat.Jpeg,
            ".bmp" => System.Drawing.Imaging.ImageFormat.Bmp,
            _ => System.Drawing.Imaging.ImageFormat.Png
        };
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
                // 替换原始图像
                _displayWriteableBitmap = null;
                SelectionCanvas.Children.Clear();
                _selectionRectangle = null;
                _originBitmap?.Dispose();
                _originBitmap = new Bitmap(openFileDialog.FileName);
                _currentRect = null;
                UpdateImage();
                RefreshDisplay();

            }
            catch (Exception ex)
            {
                new ErrorView(ex, false).Show();
            }
        }
    }
    private System.Windows.Shapes.Rectangle? _selectionRectangle; // 画布上的选择矩形
    // 绘制矩形（统一缩放与边界处理）
    public ScaleTransform? ScaleTransform;
    public TranslateTransform? TranslateTransform;
    public TransformGroup? Group;
    // 绘制矩形（统一缩放处理）
    public void DrawRectangle(int x, int y, int width, int height)
    {
        // 像素坐标边界检查
        x = Math.Clamp(x, 0, (int)_originWidth - 1);
        y = Math.Clamp(y, 0, (int)_originHeight - 1);
        width = Math.Clamp(width, 1, (int)_originWidth - x) + 1;
        height = Math.Clamp(height, 1, (int)_originHeight - y) + 1;
        Group ??= new TransformGroup();
        if (ScaleTransform == null)
        {
            ScaleTransform = new ScaleTransform(_scale, _scale);
            Group.Children.Add(ScaleTransform);
        }
        else
        {
            ScaleTransform.ScaleX = _scale;
            ScaleTransform.ScaleY = _scale;
        }
        if (TranslateTransform == null)
        {
            TranslateTransform = new TranslateTransform(x - 1, y - 1);
            Group.Children.Add(TranslateTransform);
        }
        else
        {
            TranslateTransform.X = x - 1;
            TranslateTransform.Y = y - 1;
        }

        // 清除之前的矩形
        if (_selectionRectangle == null)
        {
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
                RenderTransform = Group

            };
            SelectionCanvas.Children.Add(_selectionRectangle);
        }
        else
        {
            _selectionRectangle.Width = width;
            _selectionRectangle.Height = height;
        }
        // SelectionCanvas.Children.Remove(_selectionRectangle);

        // 创建矩形（保持样式一致）


    }

    // 编辑矩形（通过对话框输入坐标）
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
