using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using MFATools.Utils;
using Microsoft.Win32;

namespace MFATools.Views;

public partial class SelectionRegionDialog
{
    private Point _startPoint;
    private Rectangle? _selectionRectangle;
    private List<int>? _output { get; set; }

    public List<int>? Output
    {
        get => _output;
        set => _output = value?.Select(i => i < 0 ? 1 : i).ToList();
    }

    public bool IsRoi { get; set; }


    private double _scaleRatio;
    private double originWidth;
    private double originHeight;

    public SelectionRegionDialog()
    {
        InitializeComponent();
        Task.Run(() =>
        {
            var image = MaaProcessor.Instance.GetBitmapImage();
            Growls.Process(() => { UpdateImage(image); });
        });
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
        Height = image.Height + 100;     RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
        CenterWindow(); 
    }

   private const double ZoomFactor = 1.1; // 缩放因子
    private Point _dragStartPoint;
    private bool _isDragging;

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

        var x = Canvas.GetLeft(_selectionRectangle) / _scaleRatio;
        var y = Canvas.GetTop(_selectionRectangle) / _scaleRatio;
        var w = _selectionRectangle.Width / _scaleRatio;
        var h = _selectionRectangle.Height / _scaleRatio;

        // 显示 x, y, w, h 值，可以替换为其他处理逻辑
        // Growl.InfoGlobal($"Roi: {x}, {y}, {w}, {h}");
        Output = [(int)x, (int)y, (int)w, (int)h];
        DialogResult = true;
        IsRoi = SelectType.SelectedIndex == 0;
        Close();
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
            StrokeDashArray = { 2 }
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
}