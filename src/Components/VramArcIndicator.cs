using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace StreamTalkerClient.Components;

public class VramArcIndicator : Control
{
    #region StyledProperties

    public static readonly StyledProperty<double> UsagePercentProperty =
        AvaloniaProperty.Register<VramArcIndicator, double>(nameof(UsagePercent));

    public static readonly StyledProperty<double> LimitPercentProperty =
        AvaloniaProperty.Register<VramArcIndicator, double>(nameof(LimitPercent), 100);

    public static readonly StyledProperty<bool> ShowLimitProperty =
        AvaloniaProperty.Register<VramArcIndicator, bool>(nameof(ShowLimit));

    public double UsagePercent
    {
        get => GetValue(UsagePercentProperty);
        set => SetValue(UsagePercentProperty, value);
    }

    public double LimitPercent
    {
        get => GetValue(LimitPercentProperty);
        set => SetValue(LimitPercentProperty, value);
    }

    public bool ShowLimit
    {
        get => GetValue(ShowLimitProperty);
        set => SetValue(ShowLimitProperty, value);
    }

    #endregion

    #region Animation State

    private double _displayUsage;
    private double _displayLimit = 100;
    private const double LerpRate = 3.0;

    #endregion

    #region Arc Ratios (relative to control width)

    private const double ArcCenterRadius = 0.5;
    private const double ArcStroke = 0.028;
    private const double LimitTickLength = 0.045;
    private const double LimitTickThickness = 0.012;

    #endregion

    #region Colors

    private static readonly Color ColorGreen = Color.Parse("#40C060");
    private static readonly Color ColorYellow = Color.Parse("#FFD040");
    private static readonly Color ColorRed = Color.Parse("#FF4040");
    private static readonly Color TrackColor = Color.Parse("#30808080");
    private static readonly Color LimitTickColor = Color.Parse("#80FFFFFF");

    #endregion

    #region Cached Drawing Objects

    private double _cachedSize;
    private double _cachedRadius;
    private double _cachedStroke;
    private double _cachedCx;
    private double _cachedCy;
    private IPen? _trackPen;
    private StreamGeometry? _trackGeometry;
    private IPen? _limitTickPen;

    #endregion

    public void Tick(double dt)
    {
        // Fast path: skip entirely when fully converged
        if (_displayUsage == UsagePercent && _displayLimit == LimitPercent)
            return;

        var t = 1.0 - Math.Exp(-LerpRate * dt);
        var oldUsage = _displayUsage;
        var oldLimit = _displayLimit;

        _displayUsage = Lerp(_displayUsage, UsagePercent, t);
        _displayLimit = Lerp(_displayLimit, LimitPercent, t);

        if (Math.Abs(_displayUsage - oldUsage) > 0.01 ||
            Math.Abs(_displayLimit - oldLimit) > 0.01)
        {
            InvalidateVisual();
        }
    }

    public void SnapToCurrentValues()
    {
        _displayUsage = UsagePercent;
        _displayLimit = LimitPercent;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        var size = Math.Min(Bounds.Width, Bounds.Height);
        if (size < 1) return;

        // Rebuild cached objects when size changes
        if (Math.Abs(size - _cachedSize) > 0.1)
            RebuildCache(size);

        // 1. Track arc (fully cached — geometry + pen don't change between frames)
        if (_trackGeometry != null && _trackPen != null)
            context.DrawGeometry(null, _trackPen, _trackGeometry);

        // 2. Fill arc (geometry changes per frame during animation)
        if (_displayUsage > 0.1)
        {
            var fillColor = GetFillColor(_displayUsage, _displayLimit);
            var fillPen = new Pen(new SolidColorBrush(fillColor), _cachedStroke, lineCap: PenLineCap.Round);
            var fillGeometry = BuildArcGeometry(_cachedCx, _cachedCy, _cachedRadius, 0, Math.Min(_displayUsage, 100));
            context.DrawGeometry(null, fillPen, fillGeometry);
        }

        // 3. Limit tick mark (pen cached, position computed per frame)
        if (ShowLimit && _displayLimit > 0 && _displayLimit < 100 && _limitTickPen != null)
        {
            DrawLimitTick(context, _cachedCx, _cachedCy, _cachedRadius, size);
        }
    }

    private void RebuildCache(double size)
    {
        _cachedSize = size;
        _cachedStroke = Math.Max(1, size * ArcStroke);
        _cachedRadius = size * ArcCenterRadius - _cachedStroke / 2.0;
        _cachedCx = Bounds.Width / 2.0;
        _cachedCy = Bounds.Height / 2.0;

        _trackPen = new Pen(new SolidColorBrush(TrackColor), _cachedStroke, lineCap: PenLineCap.Round);
        _trackGeometry = BuildArcGeometry(_cachedCx, _cachedCy, _cachedRadius, 0, 100);
        _limitTickPen = new Pen(new SolidColorBrush(LimitTickColor),
            Math.Max(1, size * LimitTickThickness), lineCap: PenLineCap.Round);
    }

    private static StreamGeometry BuildArcGeometry(double cx, double cy,
        double radius, double fromPercent, double toPercent)
    {
        var startAngle = 180.0 + (fromPercent / 100.0) * 180.0;
        var endAngle = 180.0 + (toPercent / 100.0) * 180.0;

        var startRad = startAngle * Math.PI / 180.0;
        var endRad = endAngle * Math.PI / 180.0;

        var startX = cx + radius * Math.Cos(startRad);
        var startY = cy + radius * Math.Sin(startRad);
        var endX = cx + radius * Math.Cos(endRad);
        var endY = cy + radius * Math.Sin(endRad);

        var isLargeArc = (endAngle - startAngle) > 180;

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(startX, startY), false);
            ctx.ArcTo(
                new Point(endX, endY),
                new Size(radius, radius),
                0,
                isLargeArc,
                SweepDirection.Clockwise);
            ctx.EndFigure(false);
        }

        return geometry;
    }

    private void DrawLimitTick(DrawingContext context, double cx, double cy,
        double radius, double size)
    {
        var tickLen = size * LimitTickLength;

        var angle = 180.0 + (_displayLimit / 100.0) * 180.0;
        var rad = angle * Math.PI / 180.0;

        var cosA = Math.Cos(rad);
        var sinA = Math.Sin(rad);

        var innerR = radius - tickLen / 2.0;
        var outerR = radius + tickLen / 2.0;

        var p1 = new Point(cx + innerR * cosA, cy + innerR * sinA);
        var p2 = new Point(cx + outerR * cosA, cy + outerR * sinA);

        context.DrawLine(_limitTickPen!, p1, p2);
    }

    private static Color GetFillColor(double usage, double limit)
    {
        if (limit <= 0) return ColorRed;

        var pressure = usage / limit;

        if (pressure >= 1.0)
            return ColorRed;

        if (pressure <= 0.6)
        {
            var t = pressure / 0.6;
            return LerpColor(ColorGreen, ColorYellow, t);
        }
        else
        {
            var t = (pressure - 0.6) / 0.4;
            return LerpColor(ColorYellow, ColorRed, t);
        }
    }

    private static Color LerpColor(Color a, Color b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return Color.FromArgb(
            (byte)(a.A + (b.A - a.A) * t),
            (byte)(a.R + (b.R - a.R) * t),
            (byte)(a.G + (b.G - a.G) * t),
            (byte)(a.B + (b.B - a.B) * t));
    }

    private static double Lerp(double a, double b, double t)
    {
        if (a == b) return b;
        var result = a + (b - a) * t;
        return Math.Abs(result - b) < 0.0001 ? b : result;
    }
}
