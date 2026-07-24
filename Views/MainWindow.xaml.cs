using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.ComponentModel;
using SemParticleAnalyzer.Models;
using SemParticleAnalyzer.ViewModels;

namespace SemParticleAnalyzer.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();
    private Point? _dragStart;

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
        if (!TryMapToImage(point, out _)) return;
        _dragStart = point;
        ImageHost.CaptureMouse();
        RoiRectangle.Visibility = Visibility.Visible;
        UpdateRoiRectangle(point, point);
    }

    private void ImageHost_OnSizeChanged(object sender, SizeChangedEventArgs e) => UpdateSelectionRectangle();

    private void ImageHost_OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragStart is null || e.LeftButton != MouseButtonState.Pressed) return;
        UpdateRoiRectangle(_dragStart.Value, ClampToDisplayedImage(e.GetPosition(ImageHost)));
    }

    private void ImageHost_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragStart is null) return;
        var start = _dragStart.Value;
        var end = ClampToDisplayedImage(e.GetPosition(ImageHost));
        _dragStart = null;
        ImageHost.ReleaseMouseCapture();
        if (!TryMapToImage(start, out var imageStart) || !TryMapToImage(end, out var imageEnd)) return;
        if (Math.Abs(start.X - end.X) < 4 && Math.Abs(start.Y - end.Y) < 4)
        {
            if (!_viewModel.SelectObjectAt(imageEnd.X, imageEnd.Y))
                SelectionRectangle.Visibility = Visibility.Collapsed;
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
            SelectionRectangle.Visibility = Visibility.Collapsed;
            return;
        }
        var scaleX = displayed.Width / _viewModel.ImagePixelWidth;
        var scaleY = displayed.Height / _viewModel.ImagePixelHeight;
        Canvas.SetLeft(SelectionRectangle, displayed.Left + item.BoundingBoxX * scaleX);
        Canvas.SetTop(SelectionRectangle, displayed.Top + item.BoundingBoxY * scaleY);
        SelectionRectangle.Width = Math.Max(4, item.BoundingBoxWidth * scaleX);
        SelectionRectangle.Height = Math.Max(4, item.BoundingBoxHeight * scaleY);
        SelectionRectangle.Visibility = Visibility.Visible;
    }

    private void UpdateRoiRectangle(Point first, Point second)
    {
        var left = Math.Min(first.X, second.X);
        var top = Math.Min(first.Y, second.Y);
        Canvas.SetLeft(RoiRectangle, left);
        Canvas.SetTop(RoiRectangle, top);
        RoiRectangle.Width = Math.Abs(first.X - second.X);
        RoiRectangle.Height = Math.Abs(first.Y - second.Y);
    }

    private Point ClampToDisplayedImage(Point point)
    {
        var rect = GetDisplayedImageRect();
        return new Point(Math.Clamp(point.X, rect.Left, rect.Right), Math.Clamp(point.Y, rect.Top, rect.Bottom));
    }

    private bool TryMapToImage(Point point, out Point imagePoint)
    {
        imagePoint = default;
        var rect = GetDisplayedImageRect();
        if (rect.IsEmpty || !rect.Contains(point) || _viewModel.ImagePixelWidth <= 0) return false;
        imagePoint = new Point(
            (point.X - rect.Left) / rect.Width * _viewModel.ImagePixelWidth,
            (point.Y - rect.Top) / rect.Height * _viewModel.ImagePixelHeight);
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

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.PropertyChanged -= ViewModel_OnPropertyChanged;
        _viewModel.Dispose();
        base.OnClosed(e);
    }
}
