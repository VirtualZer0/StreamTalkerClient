using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Data;
using Avalonia.Media;
using Avalonia.Threading;
using StreamTalkerClient.Models;

namespace StreamTalkerClient.Components;

public partial class NeuralIndicator : UserControl
{
    #region StyledProperties

    public static readonly StyledProperty<IndicatorState> StateProperty =
        AvaloniaProperty.Register<NeuralIndicator, IndicatorState>(nameof(State), IndicatorState.Unloaded);

    public static readonly StyledProperty<string> StatusTextProperty =
        AvaloniaProperty.Register<NeuralIndicator, string>(nameof(StatusText), "");

    public static readonly StyledProperty<double> IndicatorSizeProperty =
        AvaloniaProperty.Register<NeuralIndicator, double>(nameof(IndicatorSize), 120);

    public static readonly StyledProperty<double> VramUsagePercentProperty =
        AvaloniaProperty.Register<NeuralIndicator, double>(nameof(VramUsagePercent));

    public static readonly StyledProperty<double> VramLimitPercentProperty =
        AvaloniaProperty.Register<NeuralIndicator, double>(nameof(VramLimitPercent), 100);

    public static readonly StyledProperty<bool> VramShowLimitProperty =
        AvaloniaProperty.Register<NeuralIndicator, bool>(nameof(VramShowLimit));

    public static readonly StyledProperty<string?> VramTooltipProperty =
        AvaloniaProperty.Register<NeuralIndicator, string?>(nameof(VramTooltip));

    public IndicatorState State
    {
        get => GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    public string StatusText
    {
        get => GetValue(StatusTextProperty);
        set => SetValue(StatusTextProperty, value);
    }

    public double IndicatorSize
    {
        get => GetValue(IndicatorSizeProperty);
        set => SetValue(IndicatorSizeProperty, value);
    }

    public double VramUsagePercent
    {
        get => GetValue(VramUsagePercentProperty);
        set => SetValue(VramUsagePercentProperty, value);
    }

    public double VramLimitPercent
    {
        get => GetValue(VramLimitPercentProperty);
        set => SetValue(VramLimitPercentProperty, value);
    }

    public bool VramShowLimit
    {
        get => GetValue(VramShowLimitProperty);
        set => SetValue(VramShowLimitProperty, value);
    }

    public string? VramTooltip
    {
        get => GetValue(VramTooltipProperty);
        set => SetValue(VramTooltipProperty, value);
    }

    #endregion

    #region Control References

    private TextBlock? _statusTextBlock;
    private Grid? _rootGrid;
    private Ellipse? _glow;
    private Ellipse? _outerRing;
    private Canvas? _orbit1;
    private Canvas? _orbit2;
    private Canvas? _orbit3;
    private Ellipse? _dot1;
    private Ellipse? _dot2;
    private Ellipse? _dot3;
    private Ellipse? _innerRing;
    private Ellipse? _core;
    private VramArcIndicator? _vramArc;
    private Window? _hostWindow;

    #endregion

    #region Transforms

    private readonly RotateTransform _orbit1Transform = new();
    private readonly RotateTransform _orbit2Transform = new();
    private readonly RotateTransform _orbit3Transform = new();
    private readonly ScaleTransform _coreScaleTransform = new();

    #endregion

    #region Size Ratios

    private const double GlowRatio = 1.0;
    private const double OuterRingRatio = 0.818;
    private const double OrbitalRatio = 0.818;
    private const double InnerRingRatio = 0.545;
    private const double CoreRatio = 0.341;
    private const double Dot1Ratio = 0.068;
    private const double Dot2Ratio = 0.057;
    private const double Dot3Ratio = 0.045;
    private const double OuterStrokeRatio = 0.017;
    private const double InnerStrokeRatio = 0.011;
    private const double DotOrbitRadius = 0.42;

    #endregion

    #region Animation Parameters

    /// <summary>
    /// Parameters for continuous animations that are driven by the code-behind timer.
    /// Colors and static opacities are handled by AXAML Transitions instead.
    /// </summary>
    private record struct StateAnimParams(
        double Orbit1Speed, double Orbit2Speed, double Orbit3Speed, // degrees/second
        double CoreScaleMin, double CoreScaleMax, double CorePulsePeriod,
        double CoreOpMin, double CoreOpMax, // core opacity pulse (1/1 = static, 0.5/1 = blink)
        double GlowOpMin, double GlowOpMax, double GlowPulsePeriod,
        double OuterOpMin, double OuterOpMax, double OuterPulsePeriod
    );

    private static readonly Dictionary<IndicatorState, StateAnimParams> StateAnimations = new()
    {
        [IndicatorState.Unloaded] = new(
            Orbit1Speed: 0, Orbit2Speed: 0, Orbit3Speed: 0,
            CoreScaleMin: 0.95, CoreScaleMax: 1.0, CorePulsePeriod: 4.0,
            CoreOpMin: 1.0, CoreOpMax: 1.0,
            GlowOpMin: 0.1, GlowOpMax: 0.1, GlowPulsePeriod: 4.0,
            OuterOpMin: 0.3, OuterOpMax: 0.3, OuterPulsePeriod: 2.0
        ),
        [IndicatorState.Loading] = new(
            Orbit1Speed: 144, Orbit2Speed: 102.86, Orbit3Speed: 72,
            CoreScaleMin: 0.85, CoreScaleMax: 1.1, CorePulsePeriod: 1.5,
            CoreOpMin: 1.0, CoreOpMax: 1.0,
            GlowOpMin: 0.15, GlowOpMax: 0.4, GlowPulsePeriod: 2.0,
            OuterOpMin: 0.4, OuterOpMax: 0.9, OuterPulsePeriod: 2.0
        ),
        [IndicatorState.WarmingUp] = new(
            Orbit1Speed: 240, Orbit2Speed: 144, Orbit3Speed: 102.86,
            CoreScaleMin: 0.88, CoreScaleMax: 1.12, CorePulsePeriod: 1.0,
            CoreOpMin: 1.0, CoreOpMax: 1.0,
            GlowOpMin: 0.2, GlowOpMax: 0.5, GlowPulsePeriod: 1.5,
            OuterOpMin: 0.5, OuterOpMax: 1.0, OuterPulsePeriod: 1.5
        ),
        [IndicatorState.Ready] = new(
            Orbit1Speed: 60, Orbit2Speed: 40, Orbit3Speed: 25.71,
            CoreScaleMin: 0.96, CoreScaleMax: 1.04, CorePulsePeriod: 3.0,
            CoreOpMin: 1.0, CoreOpMax: 1.0,
            GlowOpMin: 0.15, GlowOpMax: 0.3, GlowPulsePeriod: 4.0,
            OuterOpMin: 0.7, OuterOpMax: 0.7, OuterPulsePeriod: 2.0
        ),
        [IndicatorState.Active] = new(
            Orbit1Speed: 360, Orbit2Speed: 211.76, Orbit3Speed: 128.57,
            CoreScaleMin: 0.88, CoreScaleMax: 1.15, CorePulsePeriod: 0.8,
            CoreOpMin: 1.0, CoreOpMax: 1.0,
            GlowOpMin: 0.25, GlowOpMax: 0.6, GlowPulsePeriod: 0.8,
            OuterOpMin: 0.6, OuterOpMax: 1.0, OuterPulsePeriod: 1.0
        ),
        [IndicatorState.Error] = new(
            Orbit1Speed: 0, Orbit2Speed: 0, Orbit3Speed: 0,
            CoreScaleMin: 0.9, CoreScaleMax: 1.05, CorePulsePeriod: 0.8,
            CoreOpMin: 0.5, CoreOpMax: 1.0,
            GlowOpMin: 0.2, GlowOpMax: 0.2, GlowPulsePeriod: 4.0,
            OuterOpMin: 0.3, OuterOpMax: 0.7, OuterPulsePeriod: 1.2
        ),
    };

    #endregion

    #region Animation State

    private readonly Stopwatch _sw = new();
    private DispatcherTimer? _timer;
    private double _prevTime;

    // Orbit angles (continuous, degrees)
    private double _angle1, _angle2, _angle3;

    // Pulse phases (continuous, in cycles — never reset)
    private double _corePhase, _glowPhase, _outerPhase;

    // Current interpolated parameters (smoothly approach targets)
    private double _curSpeed1, _curSpeed2, _curSpeed3;
    private double _curCoreMin, _curCoreMax, _curCorePeriod;
    private double _curCoreOpMin, _curCoreOpMax;
    private double _curGlowMin, _curGlowMax, _curGlowPeriod;
    private double _curOuterMin, _curOuterMax, _curOuterPeriod;

    // Target parameters (set instantly on state change)
    private double _tgtSpeed1, _tgtSpeed2, _tgtSpeed3;
    private double _tgtCoreMin, _tgtCoreMax, _tgtCorePeriod;
    private double _tgtCoreOpMin, _tgtCoreOpMax;
    private double _tgtGlowMin, _tgtGlowMax, _tgtGlowPeriod;
    private double _tgtOuterMin, _tgtOuterMax, _tgtOuterPeriod;

    // How fast current values approach targets (per second, exponential)
    private const double LerpRate = 3.0;

    #endregion

    public NeuralIndicator()
    {
        InitializeComponent();
    }

    #region Lifecycle

    protected override void OnLoaded(Avalonia.Interactivity.RoutedEventArgs e)
    {
        base.OnLoaded(e);
        FindControls();
        AttachTransforms();
        ApplySize(IndicatorSize);
        UpdatePseudoClasses(State);
        SnapToState(State);

        // Subscribe to host window visibility to pause/resume timer when minimized to tray
        _hostWindow = TopLevel.GetTopLevel(this) as Window;
        if (_hostWindow != null)
            _hostWindow.PropertyChanged += OnHostPropertyChanged;

        StartTimer();
    }

    protected override void OnUnloaded(Avalonia.Interactivity.RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        StopTimer();

        if (_hostWindow != null)
        {
            _hostWindow.PropertyChanged -= OnHostPropertyChanged;
            _hostWindow = null;
        }
    }

    #endregion

    #region Setup

    private void FindControls()
    {
        _statusTextBlock ??= this.FindControl<TextBlock>("StatusTextBlock");
        _rootGrid ??= this.FindControl<Grid>("RootGrid");
        _glow ??= this.FindControl<Ellipse>("Glow");
        _outerRing ??= this.FindControl<Ellipse>("OuterRing");
        _orbit1 ??= this.FindControl<Canvas>("Orbit1");
        _orbit2 ??= this.FindControl<Canvas>("Orbit2");
        _orbit3 ??= this.FindControl<Canvas>("Orbit3");
        _dot1 ??= this.FindControl<Ellipse>("Dot1");
        _dot2 ??= this.FindControl<Ellipse>("Dot2");
        _dot3 ??= this.FindControl<Ellipse>("Dot3");
        _innerRing ??= this.FindControl<Ellipse>("InnerRing");
        _core ??= this.FindControl<Ellipse>("Core");
        _vramArc ??= this.FindControl<VramArcIndicator>("VramArc");

        if (_vramArc != null)
        {
            _vramArc.UsagePercent = VramUsagePercent;
            _vramArc.LimitPercent = VramLimitPercent;
            _vramArc.ShowLimit = VramShowLimit;
        }

        if (_statusTextBlock != null)
        {
            _statusTextBlock.Bind(TextBlock.TextProperty,
                new Binding(nameof(StatusText)) { Source = this });
        }
    }

    private void AttachTransforms()
    {
        if (_orbit1 != null) _orbit1.RenderTransform = _orbit1Transform;
        if (_orbit2 != null) _orbit2.RenderTransform = _orbit2Transform;
        if (_orbit3 != null) _orbit3.RenderTransform = _orbit3Transform;
        if (_core != null) _core.RenderTransform = _coreScaleTransform;
    }

    #endregion

    #region Timer

    private void StartTimer()
    {
        _sw.Start();
        _prevTime = _sw.Elapsed.TotalSeconds;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    private void StopTimer()
    {
        _timer?.Stop();
        _timer = null;
        _sw.Stop();
    }

    private void PauseTimer()
    {
        _timer?.Stop();
        _sw.Stop();
    }

    private void ResumeTimer()
    {
        if (_timer == null) return;
        _sw.Start();
        _prevTime = _sw.Elapsed.TotalSeconds;
        _timer.Start();
    }

    private void OnHostPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != Visual.IsVisibleProperty) return;

        if (e.NewValue is true)
            ResumeTimer();
        else
            PauseTimer();
    }

    #endregion

    #region State Targeting

    private void SetTargets(IndicatorState state)
    {
        if (!StateAnimations.TryGetValue(state, out var p)) return;

        _tgtSpeed1 = p.Orbit1Speed;
        _tgtSpeed2 = p.Orbit2Speed;
        _tgtSpeed3 = p.Orbit3Speed;
        _tgtCoreMin = p.CoreScaleMin;
        _tgtCoreMax = p.CoreScaleMax;
        _tgtCorePeriod = p.CorePulsePeriod;
        _tgtCoreOpMin = p.CoreOpMin;
        _tgtCoreOpMax = p.CoreOpMax;
        _tgtGlowMin = p.GlowOpMin;
        _tgtGlowMax = p.GlowOpMax;
        _tgtGlowPeriod = p.GlowPulsePeriod;
        _tgtOuterMin = p.OuterOpMin;
        _tgtOuterMax = p.OuterOpMax;
        _tgtOuterPeriod = p.OuterPulsePeriod;
    }

    /// <summary>
    /// Instantly copies all target values to current — used on initial load
    /// so the indicator doesn't "transition from nothing".
    /// </summary>
    private void SnapToState(IndicatorState state)
    {
        SetTargets(state);
        _curSpeed1 = _tgtSpeed1;
        _curSpeed2 = _tgtSpeed2;
        _curSpeed3 = _tgtSpeed3;
        _curCoreMin = _tgtCoreMin;
        _curCoreMax = _tgtCoreMax;
        _curCorePeriod = _tgtCorePeriod;
        _curCoreOpMin = _tgtCoreOpMin;
        _curCoreOpMax = _tgtCoreOpMax;
        _curGlowMin = _tgtGlowMin;
        _curGlowMax = _tgtGlowMax;
        _curGlowPeriod = _tgtGlowPeriod;
        _curOuterMin = _tgtOuterMin;
        _curOuterMax = _tgtOuterMax;
        _curOuterPeriod = _tgtOuterPeriod;
        ApplyFrame();
        _vramArc?.SnapToCurrentValues();
    }

    #endregion

    #region Animation Tick

    private void OnTick(object? sender, EventArgs e)
    {
        var now = _sw.Elapsed.TotalSeconds;
        var dt = now - _prevTime;
        _prevTime = now;
        if (dt <= 0 || dt > 0.1) dt = 0.016; // clamp for safety

        // Exponential lerp factor — frame-rate independent
        var t = 1.0 - Math.Exp(-LerpRate * dt);

        // Lerp orbit speeds (snap to target when converged)
        _curSpeed1 = Lerp(_curSpeed1, _tgtSpeed1, t);
        _curSpeed2 = Lerp(_curSpeed2, _tgtSpeed2, t);
        _curSpeed3 = Lerp(_curSpeed3, _tgtSpeed3, t);

        // Advance orbit angles only when spinning
        if (_curSpeed1 != 0) _angle1 = (_angle1 + _curSpeed1 * dt) % 360;
        if (_curSpeed2 != 0) _angle2 = (_angle2 + _curSpeed2 * dt) % 360;
        if (_curSpeed3 != 0) _angle3 = (_angle3 + _curSpeed3 * dt) % 360;

        // Lerp core pulse parameters
        _curCoreMin = Lerp(_curCoreMin, _tgtCoreMin, t);
        _curCoreMax = Lerp(_curCoreMax, _tgtCoreMax, t);
        _curCorePeriod = Lerp(_curCorePeriod, _tgtCorePeriod, t);
        _curCoreOpMin = Lerp(_curCoreOpMin, _tgtCoreOpMin, t);
        _curCoreOpMax = Lerp(_curCoreOpMax, _tgtCoreOpMax, t);

        // Lerp glow pulse parameters
        _curGlowMin = Lerp(_curGlowMin, _tgtGlowMin, t);
        _curGlowMax = Lerp(_curGlowMax, _tgtGlowMax, t);
        _curGlowPeriod = Lerp(_curGlowPeriod, _tgtGlowPeriod, t);

        // Lerp outer ring pulse parameters
        _curOuterMin = Lerp(_curOuterMin, _tgtOuterMin, t);
        _curOuterMax = Lerp(_curOuterMax, _tgtOuterMax, t);
        _curOuterPeriod = Lerp(_curOuterPeriod, _tgtOuterPeriod, t);

        // Advance phases — skip when pulse range is zero (min==max produces constant output)
        if (_curCorePeriod > 0.01) _corePhase += dt / _curCorePeriod;
        if (_curGlowPeriod > 0.01 && _curGlowMin != _curGlowMax) _glowPhase += dt / _curGlowPeriod;
        if (_curOuterPeriod > 0.01 && _curOuterMin != _curOuterMax) _outerPhase += dt / _curOuterPeriod;

        ApplyFrame();

        _vramArc?.Tick(dt);
    }

    private void ApplyFrame()
    {
        // Orbit rotations
        _orbit1Transform.Angle = _angle1;
        _orbit2Transform.Angle = _angle2;
        _orbit3Transform.Angle = _angle3;

        // Core scale pulse (always has min != max in all states)
        var cp = Pulse(_corePhase);
        var scale = _curCoreMin + (_curCoreMax - _curCoreMin) * cp;
        _coreScaleTransform.ScaleX = scale;
        _coreScaleTransform.ScaleY = scale;

        // Core opacity — only oscillates in Error state, static 1.0 otherwise
        if (_core != null)
            _core.Opacity = _curCoreOpMin != _curCoreOpMax
                ? _curCoreOpMin + (_curCoreOpMax - _curCoreOpMin) * cp
                : _curCoreOpMin;

        // Glow opacity — skip Pulse() when min==max (static in Unloaded/Error)
        if (_glow != null)
            _glow.Opacity = _curGlowMin != _curGlowMax
                ? _curGlowMin + (_curGlowMax - _curGlowMin) * Pulse(_glowPhase)
                : _curGlowMin;

        // Outer ring opacity — skip Pulse() when min==max (static in Unloaded/Ready)
        if (_outerRing != null)
            _outerRing.Opacity = _curOuterMin != _curOuterMax
                ? _curOuterMin + (_curOuterMax - _curOuterMin) * Pulse(_outerPhase)
                : _curOuterMin;
    }

    #endregion

    #region Property Changed

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == StateProperty)
        {
            var state = (IndicatorState)change.NewValue!;
            UpdatePseudoClasses(state); // triggers AXAML Transitions for colors
            SetTargets(state);          // code-behind timer will lerp toward new targets
        }
        else if (change.Property == IndicatorSizeProperty)
        {
            ApplySize((double)change.NewValue!);
        }
        else if (change.Property == VramUsagePercentProperty)
        {
            if (_vramArc != null)
                _vramArc.UsagePercent = (double)change.NewValue!;
        }
        else if (change.Property == VramLimitPercentProperty)
        {
            if (_vramArc != null)
                _vramArc.LimitPercent = (double)change.NewValue!;
        }
        else if (change.Property == VramShowLimitProperty)
        {
            if (_vramArc != null)
                _vramArc.ShowLimit = (bool)change.NewValue!;
        }
        else if (change.Property == VramTooltipProperty)
        {
            if (_rootGrid != null)
                ToolTip.SetTip(_rootGrid, change.NewValue as string);
        }
    }

    private void UpdatePseudoClasses(IndicatorState state)
    {
        PseudoClasses.Set(":unloaded", state == IndicatorState.Unloaded);
        PseudoClasses.Set(":loading", state == IndicatorState.Loading);
        PseudoClasses.Set(":warmingup", state == IndicatorState.WarmingUp);
        PseudoClasses.Set(":ready", state == IndicatorState.Ready);
        PseudoClasses.Set(":active", state == IndicatorState.Active);
        PseudoClasses.Set(":error", state == IndicatorState.Error);
    }

    #endregion

    #region Sizing

    private void ApplySize(double size)
    {
        if (_rootGrid == null) return;

        _rootGrid.Width = size;
        _rootGrid.Height = size;

        if (_vramArc != null)
        {
            _vramArc.Width = size;
            _vramArc.Height = size;
        }
        if (_glow != null)
        {
            _glow.Width = size * GlowRatio;
            _glow.Height = size * GlowRatio;
        }
        if (_outerRing != null)
        {
            _outerRing.Width = size * OuterRingRatio;
            _outerRing.Height = size * OuterRingRatio;
            _outerRing.StrokeThickness = size * OuterStrokeRatio;
        }
        if (_innerRing != null)
        {
            _innerRing.Width = size * InnerRingRatio;
            _innerRing.Height = size * InnerRingRatio;
            _innerRing.StrokeThickness = size * InnerStrokeRatio;
        }
        if (_core != null)
        {
            _core.Width = size * CoreRatio;
            _core.Height = size * CoreRatio;
        }

        var orbSize = size * OrbitalRatio;
        var center = orbSize / 2.0;
        var orbitR = orbSize * DotOrbitRadius;

        SizeOrbit(_orbit1, _dot1, orbSize, center, orbitR, size * Dot1Ratio, 0.0);
        SizeOrbit(_orbit2, _dot2, orbSize, center, orbitR, size * Dot2Ratio, 120.0);
        SizeOrbit(_orbit3, _dot3, orbSize, center, orbitR, size * Dot3Ratio, 240.0);
    }

    private static void SizeOrbit(Canvas? orbit, Ellipse? dot, double orbSize,
        double center, double orbitR, double dotSize, double angleDeg)
    {
        if (orbit == null) return;

        orbit.Width = orbSize;
        orbit.Height = orbSize;

        if (dot == null) return;

        dot.Width = dotSize;
        dot.Height = dotSize;

        var angleRad = angleDeg * Math.PI / 180.0;
        Canvas.SetLeft(dot, center + orbitR * Math.Sin(angleRad) - dotSize / 2.0);
        Canvas.SetTop(dot, center - orbitR * Math.Cos(angleRad) - dotSize / 2.0);
    }

    #endregion

    #region Helpers

    private static double Pulse(double phase) =>
        (Math.Sin(phase * 2.0 * Math.PI) + 1.0) / 2.0;

    private static double Lerp(double a, double b, double t)
    {
        if (a == b) return b; // fast path: already converged
        var result = a + (b - a) * t;
        // Snap to target when close enough to stop infinite asymptotic drift
        return Math.Abs(result - b) < 0.0001 ? b : result;
    }

    #endregion
}
