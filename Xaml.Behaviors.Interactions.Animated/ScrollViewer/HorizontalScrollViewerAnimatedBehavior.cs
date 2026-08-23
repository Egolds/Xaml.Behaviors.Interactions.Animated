using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Xaml.Interactivity;
using System;
using Xaml.Behaviors.Interactions.Animated.Internal;

namespace Xaml.Behaviors.Interactions.Animated;

/// <summary>
/// Animates horizontal wheel scrolling of the associated <see cref="ScrollViewer"/>: horizontal wheel
/// or trackpad gestures and Shift+wheel. A plain wheel is left untouched, so an outer vertical list
/// keeps scrolling while the pointer is over this one.
/// </summary>
public class HorizontalScrollViewerAnimatedBehavior : StyledElementBehavior<ScrollViewer>
{
    /// <summary>
    /// ScrollStepSize DirectProperty definition
    /// </summary>
    public static readonly DirectProperty<HorizontalScrollViewerAnimatedBehavior, double> ScrollStepSizeProperty =
        AvaloniaProperty.RegisterDirect<HorizontalScrollViewerAnimatedBehavior, double>(nameof(ScrollStepSize),
            o => o.ScrollStepSize,
            (o, v) => o.ScrollStepSize = v);

    private double _ScrollStepSize = 100;

    /// <summary>
    /// Distance in pixels a single wheel notch scrolls.
    /// </summary>
    public double ScrollStepSize
    {
        get => _ScrollStepSize;
        set => SetAndRaise(ScrollStepSizeProperty, ref _ScrollStepSize, value);
    }

    /// <summary>
    /// Whether a wheel notch scrolls by a step or by a whole viewport.
    /// </summary>
    public enum ChangeSize
    {
        /// <summary>Scroll by <see cref="ScrollStepSize"/>.</summary>
        Line,

        /// <summary>Scroll by the viewport width.</summary>
        Page
    }

    /// <summary>
    /// ScrollChangeSize StyledProperty definition
    /// </summary>
    public static readonly StyledProperty<ChangeSize> ScrollChangeSizeProperty =
        AvaloniaProperty.Register<HorizontalScrollViewerAnimatedBehavior, ChangeSize>(nameof(ScrollChangeSize));

    /// <summary>
    /// Whether a wheel notch scrolls by a step or by a whole viewport.
    /// </summary>
    public ChangeSize ScrollChangeSize
    {
        get => GetValue(ScrollChangeSizeProperty);
        set => SetValue(ScrollChangeSizeProperty, value);
    }

    private readonly AnimatedOffsetScroller _scroller = new(Orientation.Horizontal);

    private ScrollContentPresenter? scp;

    /// <inheritdoc/>
    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject!.AddHandler(InputElement.PointerWheelChangedEvent, OnPointerWheelChanged, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);

        AssociatedObject.Loaded += AssociatedObject_Loaded;
    }

    /// <inheritdoc/>
    protected override void OnDetaching()
    {
        base.OnDetaching();
        AssociatedObject!.RemoveHandler(InputElement.PointerWheelChangedEvent, OnPointerWheelChanged);
        _scroller.Stop();
    }

    private void AssociatedObject_Loaded(object? sender, RoutedEventArgs e)
    {
        if (AssociatedObject == null) return;

        scp = AssociatedObject.Presenter as ScrollContentPresenter;

        AssociatedObject.Loaded -= AssociatedObject_Loaded;
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (!IsEnabled)
            return;

        scp ??= AssociatedObject?.Presenter as ScrollContentPresenter;

        if (scp == null)
            return;

        // Gestures addressed to the horizontal axis, plus a plain wheel when there is nothing
        // vertical around to claim it.
        var delta = e.Delta.X != 0
            ? e.Delta.X
            : e.KeyModifiers.HasFlag(KeyModifiers.Shift) || CanTakeOverPlainWheel() ? e.Delta.Y : 0;

        if (delta == 0)
            return;

        if (!NestedScrollChaining.TryWalkToOwnPresenter(e.Source, scp, e, out var stoppedAt) || stoppedAt != scp)
            return; // a nested scroll viewer scrolls itself on this event

        var maxOffset = scp.Extent.Width - scp.Viewport.Width;
        if (maxOffset <= 0)
            return; // nothing to scroll here, let the event reach an outer list

        var scrollable = scp.Child as ILogicalScrollable;
        var isLogical = scrollable?.IsLogicalScrollEnabled == true;
        double width = isLogical ? scrollable!.ScrollSize.Width : ScrollStepSize;

        var x = Math.Clamp(scp.Offset.X - delta * width, 0, maxOffset);

        var newOffset = ScrollSnapping.SnapOffset(scp, new Vector(x, scp.Offset.Y), delta, true, Orientation.Horizontal);
        if (newOffset == scp.Offset)
            return; // already at the edge, let the event chain to an outer list

        var step = ScrollChangeSize == ChangeSize.Line
            ? Math.Abs(newOffset.X - scp.Offset.X)
            : AssociatedObject!.Bounds.Width;

        _scroller.Scroll(AssociatedObject!, delta > 0 ? -step : step);

        e.Handled = true;
    }

    /// <summary>
    /// A plain wheel is scrolled horizontally only when no one else would use it vertically: neither
    /// this list nor any list above it. That keeps a standalone carousel usable with a regular wheel
    /// without stealing the wheel from a scrollable page around it.
    /// </summary>
    private bool CanTakeOverPlainWheel()
        => scp!.Extent.Height - scp.Viewport.Height <= NestedScrollChaining.Tolerance &&
           !NestedScrollChaining.HasScrollableAncestor(AssociatedObject!, Orientation.Vertical);
}
