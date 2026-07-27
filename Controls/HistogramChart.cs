using System.Collections;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Media;
using SemParticleAnalyzer.Models;

namespace SemParticleAnalyzer.Controls;

public sealed class HistogramChart : FrameworkElement
{
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(IEnumerable), typeof(HistogramChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnItemsSourceChanged));

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    private INotifyCollectionChanged? _observable;

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var chart = (HistogramChart)d;
        if (chart._observable is not null) chart._observable.CollectionChanged -= chart.OnCollectionChanged;
        chart._observable = e.NewValue as INotifyCollectionChanged;
        if (chart._observable is not null) chart._observable.CollectionChanged += chart.OnCollectionChanged;
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => InvalidateVisual();

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        var bins = ItemsSource?.Cast<object>().OfType<HistogramBin>().ToArray() ?? [];
        var foreground = SystemColors.ControlTextBrush;
        var linePen = new Pen(SystemColors.ActiveBorderBrush, 1);
        const double left = 42;
        const double right = 12;
        const double top = 12;
        const double bottom = 30;
        var width = Math.Max(0, ActualWidth - left - right);
        var height = Math.Max(0, ActualHeight - top - bottom);
        drawingContext.DrawLine(linePen, new Point(left, top), new Point(left, top + height));
        drawingContext.DrawLine(linePen, new Point(left, top + height), new Point(left + width, top + height));
        if (bins.Length == 0 || width <= 0 || height <= 0) return;

        var maximum = Math.Max(1, bins.Max(x => x.Count));
        var slot = width / bins.Length;
        var fill = new SolidColorBrush(Color.FromRgb(80, 132, 174));
        fill.Freeze();
        for (var i = 0; i < bins.Length; i++)
        {
            var barHeight = height * bins[i].Count / maximum;
            var rectangle = new Rect(left + i * slot + 1, top + height - barHeight,
                Math.Max(1, slot - 2), barHeight);
            drawingContext.DrawRectangle(fill, null, rectangle);
        }

        DrawText(drawingContext, maximum.ToString(), new Point(4, top - 7), foreground, 10);
        DrawText(drawingContext, "0", new Point(25, top + height - 7), foreground, 10);
        DrawText(drawingContext, bins[0].Minimum.ToString("F2"), new Point(left, top + height + 5), foreground, 10);
        var maximumLabel = bins[^1].Maximum.ToString("F2");
        DrawText(drawingContext, maximumLabel, new Point(left + width - maximumLabel.Length * 6, top + height + 5), foreground, 10);
        DrawText(drawingContext, "Equivalent diameter (px)", new Point(left + width / 2 - 58, top + height + 5), foreground, 10);
    }

    private static void DrawText(DrawingContext context, string text, Point origin, Brush brush, double size)
    {
        var formatted = new FormattedText(text, System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, new Typeface("Segoe UI"), size, brush, 1);
        context.DrawText(formatted, origin);
    }
}
