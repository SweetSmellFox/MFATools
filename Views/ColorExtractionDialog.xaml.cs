using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using MFATools.Utils;
using Microsoft.Win32;

namespace MFATools.Views;

public partial class ColorExtractionDialog
{
    private Point _startPoint;
    private Rectangle? _selectionRectangle;
    private List<int>? _outputRoi { get; set; }

    public List<int>? OutputRoi
    {
        get => _outputRoi;
        set => _outputRoi = value?.Select(i => i < 0 ? 0 : i).ToList();
    }

    public List<int>? OutputUpper { get; set; }
    public List<int>? OutputLower { get; set; }

    public ColorExtractionDialog()
    {
        InitializeComponent();
        Task.Run(() =>
        {
            var ima = MaaProcessor.Instance.GetBitmapImage();
            Growls.Process(() => { UpdateImage(ima); });
        });
    }

    public void UpdateImage(BitmapImage? _imageSource)
    {
        if (_imageSource == null)
            return;
        LoadingCircle.Visibility = Visibility.Collapsed;
        ImageArea.Visibility = Visibility.Visible;
        image.Source = _imageSource;
        double imageWidth = _imageSource.PixelWidth;
        double imageHeight = _imageSource.PixelHeight;

        double maxWidth = image.MaxWidth;
        double maxHeight = image.MaxHeight;

        double widthRatio = maxWidth / imageWidth;
        double heightRatio = maxHeight / imageHeight;
        _scaleRatio = Math.Min(widthRatio, heightRatio);

        image.Width = imageWidth * _scaleRatio;
        image.Height = imageHeight * _scaleRatio;

        SelectionCanvas.Width = image.Width;
        SelectionCanvas.Height = image.Height;
        Width = image.Width + 20;
        Height = image.Height + 100;
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
        CenterWindow();
    }

    private Point _dragStartPoint;
    private bool _isDragging;
    private double _scaleRatio;
    private const double ZoomFactor = 1.1; // 缩放因子
    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
    public void CenterWindow()
    {
        var screenWidth = SystemParameters.PrimaryScreenWidth;
        var screenHeight = SystemParameters.PrimaryScreenHeight;

        var left = (screenWidth - Width) / 2;
        var top = (screenHeight - Height) / 2;

        this.Left = left;
        this.Top = top;
    }

    private void Dialog_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        var isCtrlKeyPressed = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
        if (isCtrlKeyPressed)
        {
            Point mousePosition = e.GetPosition(SelectionCanvas);
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
        double imageWidth = SelectionCanvas.ActualWidth;
        double imageHeight = SelectionCanvas.ActualHeight;

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
            var position = e.GetPosition(image);
            var canvasPosition = e.GetPosition(SelectionCanvas);

            if (canvasPosition.X < image.ActualWidth + 5 && canvasPosition.Y < image.ActualHeight + 5 && canvasPosition is { X: > -5, Y: > -5 })
            {
                if (_selectionRectangle != null)
                {
                    SelectionCanvas.Children.Remove(_selectionRectangle);
                }

                if (position.X < 0) position.X = 0;
                if (position.Y < 0) position.Y = 0;
                if (position.X > image.ActualWidth) position.X = image.ActualWidth;
                if (position.Y > image.ActualHeight) position.Y = image.ActualHeight;

                _startPoint = position;

                _selectionRectangle = new Rectangle
                {
                    Stroke = Brushes.LightGreen,
                    StrokeThickness = 1,
                    StrokeDashArray =
                    {
                        2
                    }
                };

                Canvas.SetLeft(_selectionRectangle, _startPoint.X);
                Canvas.SetTop(_selectionRectangle, _startPoint.Y);

                SelectionCanvas.Children.Add(_selectionRectangle);

                Mouse.Capture(SelectionCanvas);
            }
        }
    }

    private void Canvas_MouseMove(object sender, MouseEventArgs e)
    {
        var position = e.GetPosition(image);
        MousePositionText.Text = $"[ {(int)(position.X / _scaleRatio)}, {(int)(position.Y / _scaleRatio)} ]";
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
        else
        {
            if (_selectionRectangle == null)
                return;
            if (e.LeftButton != MouseButtonState.Pressed)
                return;

            var pos = e.GetPosition(SelectionCanvas);

            var x = Math.Min(pos.X, _startPoint.X);
            var y = Math.Min(pos.Y, _startPoint.Y);

            var w = Math.Abs(pos.X - _startPoint.X);
            var h = Math.Abs(pos.Y - _startPoint.Y);

            if (x < 0)
            {
                x = 0;
                w = _startPoint.X;
            }

            if (y < 0)
            {
                y = 0;
                h = _startPoint.Y;
            }

            if (x + w > SelectionCanvas.ActualWidth)
            {
                w = SelectionCanvas.ActualWidth - x;
            }

            if (y + h > SelectionCanvas.ActualHeight)
            {
                h = SelectionCanvas.ActualHeight - y;
            }

            _selectionRectangle.Width = w;
            _selectionRectangle.Height = h;

            Canvas.SetLeft(_selectionRectangle, x);
            Canvas.SetTop(_selectionRectangle, y);

            MousePositionText.Text =
                $"[ {(int)(x / _scaleRatio)}, {(int)(y / _scaleRatio)}, {(int)(w / _scaleRatio)}, {(int)(h / _scaleRatio)} ]";
        }
    }

    private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_selectionRectangle == null)
            return;
        if (_isDragging)
        {
            _isDragging = false;
        }
        // 释放鼠标捕获
        Mouse.Capture(null);
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectionRectangle == null)
        {
            Growls.WarningGlobal("请选择一个区域");
            return;
        }

        var x = (int)(Canvas.GetLeft(_selectionRectangle) / _scaleRatio);
        var y = (int)(Canvas.GetTop(_selectionRectangle) / _scaleRatio);
        var w = (int)(_selectionRectangle.Width / _scaleRatio);
        var h = (int)(_selectionRectangle.Height / _scaleRatio);

        switch (SelectType.SelectedIndex)
        {
            case 0:
                GetColorRange(x, y, w, h);
                break;
            case 1:
                GetColorRangeHSV(x, y, w, h);
                break;
            case 2:
                GetColorRangeGray(x, y, w, h);
                break;
        }
        DialogResult = true;
        Close();
    }

    private void GetColorRange(double x, double y, double width, double height)
    {
        if (width < 1 || !double.IsNormal(width)) width = 1;
        if (height < 1 || !double.IsNormal(height)) height = 1;
        // 创建BitmapImage对象

        if (image.Source is BitmapImage bitmapFrame)
        {
            var roiX = Math.Max(x - 5, 0);
            var roiY = Math.Max(y - 5, 0);
            var roiW = Math.Min(width + 10, bitmapFrame.PixelWidth - roiX);
            var roiH = Math.Min(height + 10, bitmapFrame.PixelHeight - roiY);
            var writeableBitmap = new WriteableBitmap(bitmapFrame);

            OutputRoi = [(int)roiX, (int)roiY, (int)roiW, (int)roiH];
            Console.WriteLine($"image: {bitmapFrame.PixelWidth}, {bitmapFrame.PixelHeight},ROI: {(int)x}, {(int)y}, {(int)width}, {(int)height}");
            try
            {
                var croppedBitmap =
                    new CroppedBitmap(writeableBitmap, new Int32Rect((int)x, (int)y, (int)width, (int)height));

                var pixels = new byte[(int)width * (int)height * 4];
                croppedBitmap.CopyPixels(pixels, (int)width * 4, 0);

                int minR = 255, minG = 255, minB = 255;
                int maxR = 0, maxG = 0, maxB = 0;

                for (int i = 0; i < pixels.Length; i += 4)
                {
                    int r = pixels[i + 2];
                    int g = pixels[i + 1];
                    int b = pixels[i];

                    if (r < minR) minR = r;
                    if (g < minG) minG = g;
                    if (b < minB) minB = b;

                    if (r > maxR) maxR = r;
                    if (g > maxG) maxG = g;
                    if (b > maxB) maxB = b;
                }

                var lower = new List<int>
                {
                    minR,
                    minG,
                    minB
                };
                var upper = new List<int>
                {
                    maxR,
                    maxG,
                    maxB
                };
                OutputUpper = upper;
                OutputLower = lower;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            // 输出颜色上下限值
        }
    }

    private void GetColorRangeHSV(double x, double y, double width, double height)
    {
        if (width < 1 || !double.IsNormal(width)) width = 1;
        if (height < 1 || !double.IsNormal(height)) height = 1;

        if (image.Source is BitmapImage bitmapFrame)
        {
            var roiX = Math.Max(x - 5, 0);
            var roiY = Math.Max(y - 5, 0);
            var roiW = Math.Min(width + 10, bitmapFrame.PixelWidth - roiX);
            var roiH = Math.Min(height + 10, bitmapFrame.PixelHeight - roiY);
            var writeableBitmap = new WriteableBitmap(bitmapFrame);

            OutputRoi = [(int)roiX, (int)roiY, (int)roiW, (int)roiH];

            var croppedBitmap =
                new CroppedBitmap(writeableBitmap, new Int32Rect((int)x, (int)y, (int)width, (int)height));

            var pixels = new byte[(int)width * (int)height * 4];
            croppedBitmap.CopyPixels(pixels, (int)width * 4, 0);

            double minH = 360, minS = 1, minV = 1;
            double maxH = 0, maxS = 0, maxV = 0;

            for (int i = 0; i < pixels.Length; i += 4)
            {
                int r = pixels[i + 2];
                int g = pixels[i + 1];
                int b = pixels[i];

                Color color = Color.FromArgb(255, (byte)r, (byte)g, (byte)b);
                // 将 RGB 转换为 HSV
                double h, s, v;
                ColorToHSV(color, out h, out s, out v);

                if (h < minH) minH = h;
                if (s < minS) minS = s;
                if (v < minV) minV = v;

                if (h > maxH) maxH = h;
                if (s > maxS) maxS = s;
                if (v > maxV) maxV = v;
            }

            var lower = new List<int>
            {
                (int)Math.Round(minH),
                (int)Math.Round(minS * 100),
                (int)Math.Round(minV * 100)
            };
            var upper = new List<int>
            {
                (int)Math.Round(maxH),
                (int)Math.Round(maxS * 100),
                (int)Math.Round(maxV * 100)
            };
            OutputUpper = upper;
            OutputLower = lower;
            // 输出颜色上下限值
        }
    }

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

            if (r == cMax)
            {
                hue = ((g - b) / delta) % 6;
            }
            else if (g == cMax)
            {
                hue = ((b - r) / delta + 2) % 6;
            }
            else
            {
                hue = ((r - g) / delta + 4) % 6;
            }

            hue = hue * 60;
            if (hue < 0) hue += 360;
        }
    }

    private void GetColorRangeGray(double x, double y, double width, double height)
    {
        if (width < 1 || !double.IsNormal(width)) width = 1;
        if (height < 1 || !double.IsNormal(height)) height = 1;

        if (image.Source is BitmapImage bitmapFrame)
        {
            var roiX = Math.Max(x - 5, 0);
            var roiY = Math.Max(y - 5, 0);
            var roiW = Math.Min(width + 10, bitmapFrame.PixelWidth - roiX);
            var roiH = Math.Min(height + 10, bitmapFrame.PixelHeight - roiY);
            var writeableBitmap = new WriteableBitmap(bitmapFrame);

            OutputRoi = [(int)roiX, (int)roiY, (int)roiW, (int)roiH];

            var croppedBitmap =
                new CroppedBitmap(writeableBitmap, new Int32Rect((int)x, (int)y, (int)width, (int)height));

            var pixels = new byte[(int)width * (int)height * 4];
            croppedBitmap.CopyPixels(pixels, (int)width * 4, 0);

            int minGray = 255;
            int maxGray = 0;

            for (int i = 0; i < pixels.Length; i += 4)
            {
                int r = pixels[i + 2];
                int g = pixels[i + 1];
                int b = pixels[i];
                // 计算灰度值
                int gray = (int)(0.299 * r + 0.587 * g + 0.114 * b);

                if (gray < minGray) minGray = gray;
                if (gray > maxGray) maxGray = gray;
            }

            var lower = new List<int>
            {
                minGray
            };
            var upper = new List<int>
            {
                maxGray
            };
            OutputUpper = upper;
            OutputLower = lower;
        }
    }

    private void Load(object sender, RoutedEventArgs e)
    {
        OpenFileDialog openFileDialog = new OpenFileDialog
        {
            Title = "LoadImageTitle".GetLocalizationString()
        };
        openFileDialog.Filter = "ImageFilter".GetLocalizationString();

        if (openFileDialog.ShowDialog() == true)
        {
            try
            {
                var bitmapImage = new BitmapImage(new Uri(openFileDialog.FileName));
                UpdateImage(bitmapImage);
                _selectionRectangle = null;
            }
            catch (Exception ex)
            {
                ErrorView errorView = new ErrorView(ex, false);
                errorView.Show();
            }
        }
    }


    public void DrawRectangle(int x, int y, int width, int height)
    {
        if (x < 1 || !double.IsNormal(x)) x = 1;
        if (y < 1 || !double.IsNormal(y)) y = 1;
        if (width < 1 || !double.IsNormal(width)) width = 1;
        if (height < 1 || !double.IsNormal(height)) height = 1;
        if (x > image.Width) x = (int)image.Width;
        if (y > image.Height) y = (int)image.Height;
        if (x + width > image.Width) width = (int)image.Width - x;
        if (y + height > image.Height) height = (int)image.Height - y;
        if (_selectionRectangle != null)
        {
            SelectionCanvas.Children.Remove(_selectionRectangle);
        }

        _selectionRectangle = new Rectangle
        {
            Stroke = Brushes.LightGreen,
            StrokeThickness = 1,
            StrokeDashArray =
            {
                2
            }
        };

        var scaledX = x * _scaleRatio;
        var scaledY = y * _scaleRatio;
        var scaledWidth = width * _scaleRatio;
        var scaledHeight = height * _scaleRatio;

        Canvas.SetLeft(_selectionRectangle, scaledX);
        Canvas.SetTop(_selectionRectangle, scaledY);
        _selectionRectangle.Width = scaledWidth;
        _selectionRectangle.Height = scaledHeight;

        SelectionCanvas.Children.Add(_selectionRectangle);
    }

    private void Edit(object sender, RoutedEventArgs e)
    {
        var dialog = new RoiEditorDialog(_selectionRectangle, _scaleRatio);
        if (dialog.ShowDialog().IsTrue())
        {
            DrawRectangle(dialog.X.ToNumber(), dialog.Y.ToNumber(), dialog.W.ToNumber(1), dialog.H.ToNumber(1));
        }
    }
    private double _offsetX;
    private double _offsetY;
}
