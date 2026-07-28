using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace TaynDM.Controls;

/// <summary>
/// Simple line chart that visualizes download speed history.
/// Uses Polyline + Polygon for fill for performance.
/// </summary>
public class SpeedChart : Control
{
    private Polyline? _line;
    private Polygon? _fill;
    private Canvas? _canvas;

    public static readonly DependencyProperty SpeedHistoryProperty =
        DependencyProperty.Register(nameof(SpeedHistory), typeof(SpeedHistory), typeof(SpeedChart),
            new PropertyMetadata(null, OnSpeedHistoryChanged));

    public SpeedHistory? SpeedHistory
    {
        get => (SpeedHistory?)GetValue(SpeedHistoryProperty);
        set => SetValue(SpeedHistoryProperty, value);
    }

    static SpeedChart()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(SpeedChart),
            new FrameworkPropertyMetadata(typeof(SpeedChart)));
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _canvas = GetTemplateChild("PART_Canvas") as Canvas;
        _line = GetTemplateChild("PART_Line") as Polyline;
        _fill = GetTemplateChild("PART_Fill") as Polygon;
        DrawChart();
    }

    private static void OnSpeedHistoryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (SpeedChart)d;
        if (e.OldValue is SpeedHistory old)
            old.PropertyChanged -= control.OnHistoryPropertyChanged;
        if (e.NewValue is SpeedHistory @new)
            @new.PropertyChanged += control.OnHistoryPropertyChanged;
    }

    private void OnHistoryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SpeedHistory.CurrentSpeed))
            Dispatcher.BeginInvoke(DrawChart);
    }

    private void DrawChart()
    {
        if (_canvas == null || _line == null || _fill == null || SpeedHistory == null) return;

        double w = _canvas.ActualWidth;
        double h = _canvas.ActualHeight > 0 ? _canvas.ActualHeight : 60;
        if (w <= 0 || h <= 0) return;

        var samples = SpeedHistory.Samples;
        if (samples.Count < 2) return;

        double maxSpeed = samples.Max();
        if (maxSpeed <= 0) maxSpeed = 1;

        var points = new PointCollection();
        var fillPoints = new PointCollection();

        for (int i = 0; i < samples.Count; i++)
        {
            double x = (i / (double)(samples.Count - 1)) * w;
            double y = h - (samples[i] / maxSpeed) * (h - 4);
            points.Add(new Point(x, y));
            fillPoints.Add(new Point(x, y));
        }

        // Close fill polygon at bottom
        fillPoints.Insert(0, new Point(0, h));
        fillPoints.Add(new Point(w, h));

        _line.Points = points;
        _fill.Points = fillPoints;
    }
}
