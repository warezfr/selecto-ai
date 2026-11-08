using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using SelectBar.Settings;

namespace SelectBar.Setup.UI;

/// <summary>Logo « S. » : tuile bleue, S blanc, point cyan. Le S se trace puis le point tombe et rebondit (700 ms).</summary>
internal sealed class LogoControl : Viewbox
{
    private const string SPath = "M69,30 C66,20 58,15 48,15 C35,15 28,22 28,31 C28,41 36,45 50,49 C64,53 72,58 72,69 C72,79 63,86 50,86 C38,86 29,80 26,70";
    private readonly Path _s;
    private readonly Ellipse _dot;
    private readonly TranslateTransform _dotShift = new();

    public LogoControl(double size)
    {
        Width = Height = size;
        Stretch = Stretch.Uniform;
        var canvas = new Canvas { Width = 100, Height = 100, Clip = new RectangleGeometry(new Rect(0, 0, 100, 100), 22, 22) };
        canvas.Children.Add(new Rectangle { Width = 100, Height = 100, Fill = new SolidColorBrush(SetupTheme.Hex(ThemePalette.Accent)) });
        _s = new Path
        {
            Data = Geometry.Parse(SPath),
            Stroke = Brushes.White,
            StrokeThickness = 12,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            RenderTransform = new TranslateTransform(-5, 0),
        };
        canvas.Children.Add(_s);
        _dot = new Ellipse { Width = 13, Height = 13, Fill = new SolidColorBrush(SetupTheme.Hex(ThemePalette.AccentDark)), RenderTransform = _dotShift };
        Canvas.SetLeft(_dot, 82 - 6.5);
        Canvas.SetTop(_dot, 80 - 6.5);
        canvas.Children.Add(_dot);
        Child = canvas;
    }

    /// <summary>Sans animation, le logo apparaît complet.</summary>
    public void Play(bool animate)
    {
        if (!animate || !Motion.Enabled) return;
        Motion.DrawOn(_s, 16, 600);
        Motion.Drop(_dotShift, -70, 700, 600);
    }
}

/// <summary>Coche animée dans un disque (tracé de 500 ms).</summary>
internal sealed class CheckControl : Grid
{
    public CheckControl(double size, Brush discFill, Brush mark)
    {
        Width = Height = size;
        var scale = new ScaleTransform(1, 1);
        RenderTransformOrigin = new Point(0.5, 0.5);
        RenderTransform = scale;
        Children.Add(new Ellipse { Fill = discFill });
        var box = new Viewbox { Stretch = Stretch.Uniform };
        var canvas = new Canvas { Width = 100, Height = 100 };
        var check = new Path
        {
            Data = Geometry.Parse("M29,52 L44,67 L72,35"),
            Stroke = mark,
            StrokeThickness = 8,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
        };
        canvas.Children.Add(check);
        box.Child = canvas;
        Children.Add(box);
        Loaded += (_, _) =>
        {
            if (!Motion.Enabled) return;
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, Motion.Anim(0.82, 1, 300));
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, Motion.Anim(0.82, 1, 300));
            Motion.DrawOn(check, 9, 500, 120); // longueur du chemin ~ 63 / épaisseur 8
        };
    }
}

/// <summary>Anneau de progression (0 à 100) : propriété Value animable.</summary>
internal sealed class RingControl : Grid
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(RingControl), new PropertyMetadata(0.0, (d, _) => ((RingControl)d).Refresh()));

    private readonly Ellipse _bar;
    private readonly double _circumferenceUnits;

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public RingControl(double size, double thickness)
    {
        Width = Height = size;
        _circumferenceUnits = Math.PI * (size - thickness) / thickness;
        Children.Add(new Ellipse { Stroke = SetupTheme.Track, StrokeThickness = thickness });
        _bar = new Ellipse
        {
            Stroke = SetupTheme.Accent,
            StrokeThickness = thickness,
            StrokeDashCap = PenLineCap.Round,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new RotateTransform(-90),
        };
        Children.Add(_bar);
        Refresh();
    }

    private void Refresh()
    {
        if (_bar == null) return;
        var v = Math.Min(100, Math.Max(0, Value));
        _bar.Opacity = v <= 0 ? 0 : 1;
        _bar.StrokeDashArray = new DoubleCollection { Math.Max(0.001, _circumferenceUnits * v / 100), 1000 };
    }

    public void SetAnimated(double percent)
    {
        if (Motion.Enabled) BeginAnimation(ValueProperty, Motion.Anim(Value, percent, 160));
        else { BeginAnimation(ValueProperty, null); Value = percent; }
    }
}

/// <summary>Carrousel : une diapositive visible à la fois, fondu enchaîné de 400 ms toutes les 3 s.</summary>
internal sealed class Carousel : Grid
{
    private readonly List<UIElement> _slides = new();
    private readonly List<Ellipse> _dots = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(3) };
    private int _index;

    public Carousel(IEnumerable<UIElement> slides)
    {
        var host = new Grid();
        foreach (var s in slides)
        {
            s.Opacity = _slides.Count == 0 ? 1 : 0;
            host.Children.Add(s);
            _slides.Add(s);
        }
        var dots = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 18) };
        for (var i = 0; i < _slides.Count; i++)
        {
            var d = new Ellipse { Width = 7, Height = 7, Margin = new Thickness(4, 0, 4, 0), Fill = SetupTheme.Accent, Opacity = i == 0 ? 1 : 0.3 };
            _dots.Add(d);
            dots.Children.Add(d);
        }
        Children.Add(host);
        Children.Add(dots);
        _timer.Tick += (_, _) => Show((_index + 1) % _slides.Count);
        Loaded += (_, _) => { if (_slides.Count > 1) _timer.Start(); };
        Unloaded += (_, _) => _timer.Stop();
    }

    public void Show(int next)
    {
        if (next == _index) return;
        Motion.FadeTo(_slides[_index], 0, 400);
        Motion.FadeTo(_slides[next], 1, 400);
        Motion.FadeTo(_dots[_index], 0.3, 400);
        Motion.FadeTo(_dots[next], 1, 400);
        _index = next;
    }
}
