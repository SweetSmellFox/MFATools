using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using HandyControl.Controls;
using HandyControl.Data;
using MFATools.Utils;
using MFATools.Controls;
using Microsoft.Win32;

namespace MFATools.Views;

public partial class CropImageDialog
{
    private Point _startPoint;
    private Rectangle? _selectionRectangle;

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

    public bool AlignToPixels { get; set; } = true;

    public CropImageDialog()
    {
        InitializeComponent();
        Task.Run(() =>
        {
            var image = MaaProcessor.Instance.GetBitmapImage();
            Growls.Process(() => { UpdateImage(image); });
        });
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
    }


    public void UpdateImage(BitmapImage? _imageSource)
    {
        if (_imageSource == null)
            return;
        LoadingCircle.Visibility = Visibility.Collapsed;
        ImageArea.Visibility = Visibility.Visible;
        image.Source = _imageSource;

        originWidth = _imageSource.PixelWidth;
        originHeight = _imageSource.PixelHeight;

        double maxWidth = image.MaxWidth;
        double maxHeight = image.MaxHeight;

        double widthRatio = maxWidth / originWidth;
        double heightRatio = maxHeight / originHeight;
        _scaleRatio = Math.Min(widthRatio, heightRatio);

        image.Width = originWidth * _scaleRatio;
        image.Height = originHeight * _scaleRatio;

        SelectionCanvas.Width = image.Width;
        SelectionCanvas.Height = image.Height;
        Width = image.Width + 20;
        Height = image.Height + 100;
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
        CenterWindow();
    }

    private double originWidth;
    private double originHeight;
    private const double ZoomFactor = 1.1; // 缩放因子
    private Point _dragStartPoint;
    private bool _isDragging;
    private double _scaleRatio;
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

                double actualX = AlignToPixelCoord(position.X);
                double actualY = AlignToPixelCoord(position.Y);
                _startPoint = new Point(PixelToScreenCoord(actualX), PixelToScreenCoord(actualY));

                _selectionRectangle = new Rectangle
                {
                    Stroke = SettingDialog.DefaultLineColor,
                    StrokeThickness = SettingDialog.DefaultLineThickness,
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

            double startX = AlignToPixelCoord(_startPoint.X);
            double startY = AlignToPixelCoord(_startPoint.Y);
            double currentX = AlignToPixelCoord(pos.X);
            double currentY = AlignToPixelCoord(pos.Y);

            // 2. 计算实际像素坐标的矩形参数
            double actualX = Math.Min(startX, currentX);
            double actualY = Math.Min(startY, currentY);
            double actualW = Math.Abs(startX - currentX);
            double actualH = Math.Abs(startY - currentY);

            // 3. 转回屏幕坐标（并应用边界检查）
            double x = PixelToScreenCoord(actualX);
            double y = PixelToScreenCoord(actualY);
            double w = PixelToScreenCoord(actualW);
            double h = PixelToScreenCoord(actualH);

            if (x < 0)
            {
                x = 0;
                w = PixelToScreenCoord(actualX + actualW); // 重新计算宽度
            }
            if (y < 0)
            {
                y = 0;
                h = PixelToScreenCoord(actualY + actualH); // 重新计算高度
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

        var x = Canvas.GetLeft(_selectionRectangle) / _scaleRatio;
        var y = Canvas.GetTop(_selectionRectangle) / _scaleRatio;
        var w = _selectionRectangle.Width / _scaleRatio;
        var h = _selectionRectangle.Height / _scaleRatio;
        if (AlignToPixels)
        {
            x = Math.Round(x);
            y = Math.Round(y);
            w = Math.Round(w);
            h = Math.Round(h);
        }

        SaveCroppedImage(x, y, w, h);
    }

    private void SaveCroppedImage(double x, double y, double width, double height)
    {
        if (width < 1 || !double.IsNormal(width)) width = 1;
        if (height < 1 || !double.IsNormal(height)) height = 1;
        // 创建BitmapImage对象
        if (image.Source is BitmapImage bitmapImage)
        {
            OutputOriginRoi =
            [
                (int)x,
                (int)y,
                (int)width,
                (int)height
            ];
            var roiX = Math.Max(x - MFAExtensions.HorizontalExpansion / 2, 0);
            var roiY = Math.Max(y - MFAExtensions.VerticalExpansion / 2, 0);
            var roiW = Math.Min(width + MFAExtensions.HorizontalExpansion, bitmapImage.PixelWidth - roiX);
            var roiH = Math.Min(height + MFAExtensions.VerticalExpansion, bitmapImage.PixelHeight - roiY);
            OutputRoi = new List<int>
            {
                (int)roiX,
                (int)roiY,
                (int)roiW,
                (int)roiH
            };
            // 创建WriteableBitmap对象并加载BitmapImage
            var writeableBitmap = new WriteableBitmap(bitmapImage);

            // 创建一个用于存储裁剪区域的矩形
            var cropRect = new Int32Rect((int)x, (int)y, (int)width, (int)height);

            // 创建一个字节数组来保存裁剪区域的像素数据
            var croppedPixels = new byte[cropRect.Width * cropRect.Height * 4];
            writeableBitmap.CopyPixels(cropRect, croppedPixels, cropRect.Width * 4, 0);

            // 创建一个新的WriteableBitmap来保存裁剪后的图像
            var croppedBitmap = new WriteableBitmap(cropRect.Width, cropRect.Height, 96, 96, PixelFormats.Bgra32, null);
            croppedBitmap.WritePixels(new Int32Rect(0, 0, cropRect.Width, cropRect.Height), croppedPixels,
                cropRect.Width * 4, 0);

            var saveFileDialog = new SaveFileDialog
            {
                Filter = "ImageFilter".GetLocalizationString(),
                DefaultExt = "png"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                var encoder = GetEncoderByExtension(saveFileDialog.FileName);
                encoder.Frames.Add(BitmapFrame.Create(croppedBitmap));

                using (var fileStream = new FileStream(saveFileDialog.FileName, FileMode.Create))
                {
                    encoder.Save(fileStream);
                }

                // 设置 Output 属性为保存的文件名和路径
                Output = System.IO.Path.GetFileName(saveFileDialog.FileName);
                DialogResult = true;
                Close();
            }
        }
    }

    private BitmapEncoder GetEncoderByExtension(string fileName)
    {
        var extension = System.IO.Path.GetExtension(fileName).ToLower();

        switch (extension)
        {
            case ".jpg":
            case ".jpeg":
                return new JpegBitmapEncoder();
            case ".bmp":
                return new BmpBitmapEncoder();
            default:
                return new PngBitmapEncoder();
        }
    }

    private void Screenshot(object sender, RoutedEventArgs e)
    {
        new Screenshot().Start();

        // if (openFileDialog.ShowDialog() == true)
        // {
        //     try
        //     {
        //         BitmapImage bitmapImage = new BitmapImage(new Uri(openFileDialog.FileName));
        //         UpdateImage(bitmapImage);
        //     }
        //     catch (Exception ex)
        //     {
        //         ErrorView errorView = new ErrorView(ex, false);
        //         errorView.Show();
        //     }
        // }
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
                BitmapImage bitmapImage = new BitmapImage(new Uri(openFileDialog.FileName));
                UpdateImage(bitmapImage);
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
        // 边界检查：使用实际像素宽高（originWidth/originHeight）而非屏幕宽高（image.Width）
        if (x < 1) x = 1;
        if (y < 1) y = 1;
        if (width < 1) width = 1;
        if (height < 1) height = 1;
        // 实际像素坐标不能超过图像的实际像素宽高
        if (x > originWidth) x = (int)originWidth;
        if (y > originHeight) y = (int)originHeight;
        if (x + width > originWidth) width = (int)(originWidth - x);
        if (y + height > originHeight) height = (int)(originHeight - y);

        // 移除旧矩形
        if (_selectionRectangle != null)
        {
            SelectionCanvas.Children.Remove(_selectionRectangle);
        }

        // 计算屏幕坐标（实际像素坐标 -> 屏幕坐标）
        double scaledX = PixelToScreenCoord(x);
        double scaledY = PixelToScreenCoord(y);
        double scaledWidth = PixelToScreenCoord(width);
        double scaledHeight = PixelToScreenCoord(height);

        // 若启用像素对齐，确保屏幕坐标为整数（避免半像素偏移）
        if (AlignToPixels)
        {
            scaledX = Math.Round(scaledX);
            scaledY = Math.Round(scaledY);
            scaledWidth = Math.Round(scaledWidth);
            scaledHeight = Math.Round(scaledHeight);
        }

        // 创建新矩形
        _selectionRectangle = new Rectangle
        {
            Stroke = SettingDialog.DefaultLineColor,
            StrokeThickness = SettingDialog.DefaultLineThickness,
            StrokeDashArray = { 2 },
            Width = scaledWidth,
            Height = scaledHeight
        };

        Canvas.SetLeft(_selectionRectangle, scaledX);
        Canvas.SetTop(_selectionRectangle, scaledY);
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
    /// <summary>
    /// 将屏幕坐标转换为实际像素坐标，并根据_alignToPixels进行整数对齐
    /// </summary>
    /// <param name="screenCoord">屏幕坐标（受缩放影响）</param>
    /// <returns>对齐后的实际像素坐标</returns>
    private double AlignToPixelCoord(double screenCoord)
    {
        // 屏幕坐标 -> 实际像素坐标（除以缩放比例）
        double pixelCoord = screenCoord / _scaleRatio;
        // 若启用对齐，则四舍五入到最近的整数像素
        return AlignToPixels ? Math.Round(pixelCoord) : pixelCoord;
    }

    /// <summary>
    /// 将实际像素坐标转换为屏幕坐标（用于显示）
    /// </summary>
    private double PixelToScreenCoord(double pixelCoord)
    {
        return pixelCoord * _scaleRatio;
    }

    private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
    {
        AlignToPixels = (sender as CheckBox)?.IsChecked ?? true;
    }
}
