using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.ComponentModel;
using System.Windows.Controls.Primitives;
using SemParticleAnalyzer.Models;
using SemParticleAnalyzer.ViewModels;

namespace SemParticleAnalyzer.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();
    private Point? _dragStart;
    private Point? _panStart;
    private double _panStartX;
    private double _panStartY;
    private const double MinimumZoom = 0.2;
    private const double MaximumZoom = 20;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        _viewModel.PropertyChanged += ViewModel_OnPropertyChanged;
    }

    private void ImageHost_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel.DisplayImage is null) return;
        var point = e.GetPosition(ImageHost);
        if (_viewModel.ViewerTool == ViewerTool.Pan)
        {
            _panStart = point;
            _panStartX = ViewerTranslateTransform.X;
            _panStartY = ViewerTranslateTransform.Y;
            ImageHost.Cursor = Cursors.Hand;
            ImageHost.CaptureMouse();
            return;
        }
        if (!TryMapToImage(point, out _)) return;
        _dragStart = point;
        ImageHost.CaptureMouse();
        var rectangle = _viewModel.ViewerTool == ViewerTool.ZoomArea ? ZoomAreaRectangle : RoiRectangle;
        rectangle.Visibility = Visibility.Visible;
        UpdateDragRectangle(rectangle, ToContentPoint(point), ToContentPoint(point));
    }

    private void ImageHost_OnSizeChanged(object sender, SizeChangedEventArgs e) => UpdateSelectionRectangle();

    private void ImageHost_OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_panStart is not null && e.LeftButton == MouseButtonState.Pressed)
        {
            var current = e.GetPosition(ImageHost);
            ViewerTranslateTransform.X = _panStartX + current.X - _panStart.Value.X;
            ViewerTranslateTransform.Y = _panStartY + current.Y - _panStart.Value.Y;
            return;
        }
        if (_dragStart is null || e.LeftButton != MouseButtonState.Pressed) return;
        var currentPoint = ClampToDisplayedImage(e.GetPosition(ImageHost));
        var rectangle = _viewModel.ViewerTool == ViewerTool.ZoomArea ? ZoomAreaRectangle : RoiRectangle;
        UpdateDragRectangle(rectangle, ToContentPoint(_dragStart.Value), ToContentPoint(currentPoint));
    }

    private void ImageHost_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_panStart is not null)
        {
            _panStart = null;
            ImageHost.Cursor = Cursors.Arrow;
            ImageHost.ReleaseMouseCapture();
            return;
        }
        if (_dragStart is null) return;
        var start = _dragStart.Value;
        var end = ClampToDisplayedImage(e.GetPosition(ImageHost));
        _dragStart = null;
        ImageHost.ReleaseMouseCapture();
        if (_viewModel.ViewerTool == ViewerTool.ZoomArea)
        {
            ZoomAreaRectangle.Visibility = Visibility.Collapsed;
            if (Math.Abs(start.X - end.X) >= 5 && Math.Abs(start.Y - end.Y) >= 5)
                ZoomToArea(start, end);
            return;
        }
        if (!TryMapToImage(start, out var imageStart) || !TryMapToImage(end, out var imageEnd)) return;
        if (Math.Abs(start.X - end.X) < 4 && Math.Abs(start.Y - end.Y) < 4)
        {
            if (!_viewModel.SelectObjectAt(imageEnd.X, imageEnd.Y))
                SelectionContour.Visibility = Visibility.Collapsed;
            RoiRectangle.Visibility = Visibility.Collapsed;
            return;
        }
        var left = (int)Math.Floor(Math.Min(imageStart.X, imageEnd.X));
        var top = (int)Math.Floor(Math.Min(imageStart.Y, imageEnd.Y));
        var right = (int)Math.Ceiling(Math.Max(imageStart.X, imageEnd.X));
        var bottom = (int)Math.Ceiling(Math.Max(imageStart.Y, imageEnd.Y));
        if (right - left < 2 || bottom - top < 2) return;
        _viewModel.SetRoi(new RectangleRoi { X = left, Y = top, Width = right - left, Height = bottom - top });
    }

    private void ViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.SourceInfo))
        {
            ResetView();
            RoiRectangle.Visibility = Visibility.Collapsed;
            SelectionContour.Visibility = Visibility.Collapsed;
            ZoomAreaRectangle.Visibility = Visibility.Collapsed;
            return;
        }
        if (e.PropertyName != nameof(MainViewModel.SelectedObject)) return;
        UpdateSelectionRectangle();
        if (_viewModel.SelectedObject is not null)
        {
            ObjectDataGrid.ScrollIntoView(_viewModel.SelectedObject);
            ObjectDataGrid.Focus();
        }
    }

    private void UpdateSelectionRectangle()
    {
        var item = _viewModel.SelectedObject;
        var displayed = GetDisplayedImageRect();
        if (item is null || displayed.IsEmpty || _viewModel.ImagePixelWidth <= 0)
        {
            SelectionContour.Visibility = Visibility.Collapsed;
            return;
        }
        var scaleX = displayed.Width / _viewModel.ImagePixelWidth;
        var scaleY = displayed.Height / _viewModel.ImagePixelHeight;
        if (item.ContourPoints.Count >= 3)
        {
            SelectionContour.Points = new PointCollection(item.ContourPoints.Select(p =>
                new Point(displayed.Left + p.X * scaleX, displayed.Top + p.Y * scaleY)));
        }
        else
        {
            SelectionContour.Points = new PointCollection
            {
                new(displayed.Left + item.BoundingBoxX * scaleX, displayed.Top + item.BoundingBoxY * scaleY),
                new(displayed.Left + (item.BoundingBoxX + item.BoundingBoxWidth) * scaleX, displayed.Top + item.BoundingBoxY * scaleY),
                new(displayed.Left + (item.BoundingBoxX + item.BoundingBoxWidth) * scaleX, displayed.Top + (item.BoundingBoxY + item.BoundingBoxHeight) * scaleY),
                new(displayed.Left + item.BoundingBoxX * scaleX, displayed.Top + (item.BoundingBoxY + item.BoundingBoxHeight) * scaleY)
            };
        }
        SelectionContour.Visibility = Visibility.Visible;
    }

    private static void UpdateDragRectangle(System.Windows.Shapes.Rectangle rectangle, Point first, Point second)
    {
        var left = Math.Min(first.X, second.X);
        var top = Math.Min(first.Y, second.Y);
        Canvas.SetLeft(rectangle, left);
        Canvas.SetTop(rectangle, top);
        rectangle.Width = Math.Abs(first.X - second.X);
        rectangle.Height = Math.Abs(first.Y - second.Y);
    }

    private Point ClampToDisplayedImage(Point point)
    {
        var content = ToContentPoint(point);
        var rect = GetDisplayedImageRect();
        var clamped = new Point(Math.Clamp(content.X, rect.Left, rect.Right), Math.Clamp(content.Y, rect.Top, rect.Bottom));
        return ToHostPoint(clamped);
    }

    private bool TryMapToImage(Point point, out Point imagePoint)
    {
        imagePoint = default;
        var rect = GetDisplayedImageRect();
        var contentPoint = ToContentPoint(point);
        if (rect.IsEmpty || !rect.Contains(contentPoint) || _viewModel.ImagePixelWidth <= 0) return false;
        imagePoint = new Point(
            (contentPoint.X - rect.Left) / rect.Width * _viewModel.ImagePixelWidth,
            (contentPoint.Y - rect.Top) / rect.Height * _viewModel.ImagePixelHeight);
        return true;
    }

    private Rect GetDisplayedImageRect()
    {
        var imageWidth = _viewModel.ImagePixelWidth;
        var imageHeight = _viewModel.ImagePixelHeight;
        if (imageWidth <= 0 || imageHeight <= 0 || ImageHost.ActualWidth <= 0 || ImageHost.ActualHeight <= 0)
            return Rect.Empty;
        var scale = Math.Min(ImageHost.ActualWidth / imageWidth, ImageHost.ActualHeight / imageHeight);
        var width = imageWidth * scale;
        var height = imageHeight * scale;
        return new Rect((ImageHost.ActualWidth - width) / 2, (ImageHost.ActualHeight - height) / 2, width, height);
    }

    private Point ToContentPoint(Point hostPoint) => new(
        (hostPoint.X - ViewerTranslateTransform.X) / ViewerScaleTransform.ScaleX,
        (hostPoint.Y - ViewerTranslateTransform.Y) / ViewerScaleTransform.ScaleY);

    private Point ToHostPoint(Point contentPoint) => new(
        contentPoint.X * ViewerScaleTransform.ScaleX + ViewerTranslateTransform.X,
        contentPoint.Y * ViewerScaleTransform.ScaleY + ViewerTranslateTransform.Y);

    private void ImageHost_OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        ZoomAt(e.GetPosition(ImageHost), e.Delta > 0 ? 1.2 : 1 / 1.2);
        e.Handled = true;
    }

    private void ZoomIn_OnClick(object sender, RoutedEventArgs e) =>
        ZoomAt(new Point(ImageHost.ActualWidth / 2, ImageHost.ActualHeight / 2), 1.25);

    private void ZoomOut_OnClick(object sender, RoutedEventArgs e) =>
        ZoomAt(new Point(ImageHost.ActualWidth / 2, ImageHost.ActualHeight / 2), 1 / 1.25);

    private void Fit_OnClick(object sender, RoutedEventArgs e) => ResetView();

    private void SelectRoiToolButton_OnClick(object sender, RoutedEventArgs e) =>
        SetViewerTool(ViewerTool.SelectAndRoi);

    private void PanToolButton_OnClick(object sender, RoutedEventArgs e) =>
        SetViewerTool(ViewerTool.Pan);

    private void ZoomAreaToolButton_OnClick(object sender, RoutedEventArgs e) =>
        SetViewerTool(ViewerTool.ZoomArea);

    private void SetViewerTool(ViewerTool tool)
    {
        _viewModel.ViewerTool = tool;
        SelectRoiToolButton.IsChecked = tool == ViewerTool.SelectAndRoi;
        PanToolButton.IsChecked = tool == ViewerTool.Pan;
        ZoomAreaToolButton.IsChecked = tool == ViewerTool.ZoomArea;
        ImageHost.Cursor = tool == ViewerTool.Pan ? Cursors.Hand : Cursors.Cross;
        RoiRectangle.Visibility = Visibility.Collapsed;
        ZoomAreaRectangle.Visibility = Visibility.Collapsed;
    }

    private void ActualSize_OnClick(object sender, RoutedEventArgs e)
    {
        var rect = GetDisplayedImageRect();
        if (rect.IsEmpty || _viewModel.ImagePixelWidth <= 0) return;
        var fitPixelsPerImagePixel = rect.Width / _viewModel.ImagePixelWidth;
        var target = Math.Clamp(1 / fitPixelsPerImagePixel, MinimumZoom, MaximumZoom);
        SetZoomAt(new Point(ImageHost.ActualWidth / 2, ImageHost.ActualHeight / 2), target);
    }

    private void ZoomAt(Point hostAnchor, double factor) =>
        SetZoomAt(hostAnchor, Math.Clamp(ViewerScaleTransform.ScaleX * factor, MinimumZoom, MaximumZoom));

    private void SetZoomAt(Point hostAnchor, double newZoom)
    {
        var contentAnchor = ToContentPoint(hostAnchor);
        ViewerScaleTransform.ScaleX = newZoom;
        ViewerScaleTransform.ScaleY = newZoom;
        ViewerTranslateTransform.X = hostAnchor.X - contentAnchor.X * newZoom;
        ViewerTranslateTransform.Y = hostAnchor.Y - contentAnchor.Y * newZoom;
        UpdateOverlayStrokeWidths();
    }

    private void ZoomToArea(Point firstHost, Point secondHost)
    {
        var width = Math.Abs(firstHost.X - secondHost.X);
        var height = Math.Abs(firstHost.Y - secondHost.Y);
        if (width < 1 || height < 1) return;
        var factor = Math.Min(ImageHost.ActualWidth / width, ImageHost.ActualHeight / height);
        var center = new Point((firstHost.X + secondHost.X) / 2, (firstHost.Y + secondHost.Y) / 2);
        var newZoom = Math.Clamp(ViewerScaleTransform.ScaleX * factor, MinimumZoom, MaximumZoom);
        var contentCenter = ToContentPoint(center);
        ViewerScaleTransform.ScaleX = newZoom;
        ViewerScaleTransform.ScaleY = newZoom;
        ViewerTranslateTransform.X = ImageHost.ActualWidth / 2 - contentCenter.X * newZoom;
        ViewerTranslateTransform.Y = ImageHost.ActualHeight / 2 - contentCenter.Y * newZoom;
        UpdateOverlayStrokeWidths();
    }

    private void ResetView()
    {
        ViewerScaleTransform.ScaleX = 1;
        ViewerScaleTransform.ScaleY = 1;
        ViewerTranslateTransform.X = 0;
        ViewerTranslateTransform.Y = 0;
        UpdateOverlayStrokeWidths();
    }

    private void UpdateOverlayStrokeWidths()
    {
        var inverseZoom = 1 / ViewerScaleTransform.ScaleX;
        RoiRectangle.StrokeThickness = 2 * inverseZoom;
        SelectionContour.StrokeThickness = 1.5 * inverseZoom;
        ZoomAreaRectangle.StrokeThickness = 1.5 * inverseZoom;
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.PropertyChanged -= ViewModel_OnPropertyChanged;
        _viewModel.Dispose();
        base.OnClosed(e);
    }
}
