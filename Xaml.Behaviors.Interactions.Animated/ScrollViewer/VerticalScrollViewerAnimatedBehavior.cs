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
/// Animates vertical wheel scrolling of the associated <see cref="ScrollViewer"/>.
/// </summary>
public class VerticalScrollViewerAnimatedBehavior : StyledElementBehavior<ScrollViewer>
{

    /// <summary>
    /// ScrollStepSize DirectProperty definition
    /// </summary>
    public static readonly DirectProperty<VerticalScrollViewerAnimatedBehavior, double> ScrollStepSizeProperty =
        AvaloniaProperty.RegisterDirect<VerticalScrollViewerAnimatedBehavior, double>(nameof(ScrollStepSize),
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

        /// <summary>Scroll by the viewport height.</summary>
        Page
    }

    /// <summary>
    /// ScrollChangeSize StyledProperty definition
    /// </summary>
    public static readonly StyledProperty<ChangeSize> ScrollChangeSizeProperty =
        AvaloniaProperty.Register<VerticalScrollViewerAnimatedBehavior, ChangeSize>(nameof(ScrollChangeSize));

    /// <summary>
    /// Whether a wheel notch scrolls by a step or by a whole viewport.
    /// </summary>
    public ChangeSize ScrollChangeSize
    {
        get => GetValue(ScrollChangeSizeProperty);
        set => SetValue(ScrollChangeSizeProperty, value);
    }

    private readonly AnimatedOffsetScroller _scroller = new(Orientation.Vertical);

    private ScrollContentPresenter? scp;

    /// <inheritdoc/>
    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject!.AddHandler(InputElement.PointerWheelChangedEvent, OnPointerWheelChanged, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
        AssociatedObject!.SetValue(ScrollChangeSizeProperty, ChangeSize.Line);

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

        scp = AssociatedObject?.Presenter as ScrollContentPresenter;

        AssociatedObject!.Loaded -= AssociatedObject_Loaded;
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (!IsEnabled)
        {
            e.Handled = !scp?.IsScrollChainingEnabled ?? false;
            return;
        }

        scp ??= AssociatedObject?.Presenter as ScrollContentPresenter;

        if (scp == null)
            return;

        // A horizontal gesture is left to whoever scrolls this content horizontally, but only when
        // there is somewhere to move: otherwise Shift+wheel would simply stop scrolling the list.
        if ((e.Delta.X != 0 || e.KeyModifiers.HasFlag(KeyModifiers.Shift)) &&
            scp.Extent.Width - scp.Viewport.Width > NestedScrollChaining.Tolerance)
        {
            return;
        }

        if (!NestedScrollChaining.TryWalkToOwnPresenter(e.Source, scp, e, out var stoppedAt))
            return; // a nested scroll viewer scrolls itself on this event

        if (stoppedAt != scp)
        {
            e.Handled = !(stoppedAt as ScrollContentPresenter)?.IsScrollChainingEnabled ?? false;
            return;
        }

        var delta = e.Delta.Y;
        if (delta == 0)
        {
            // Purely horizontal gesture: nothing to animate vertically. Handled is still set the
            // same way the full path below would set it, so disabled chaining keeps trapping the event.
            e.Handled = !scp.IsScrollChainingEnabled;
            return;
        }

        var y = scp.Offset.Y;

        var scrollable = scp.Child as ILogicalScrollable;
        var isLogical = scrollable?.IsLogicalScrollEnabled == true;
        if (scp.Extent.Height > scp.Viewport.Height)
        {
            double height = isLogical ? scrollable!.ScrollSize.Height : ScrollStepSize;
            y -= delta * height;
            y = Math.Clamp(y, 0, scp.Extent.Height - scp.Viewport.Height);
        }

        var newOffset = ScrollSnapping.SnapOffset(scp, new Vector(scp.Offset.X, y), delta, true, Orientation.Vertical);
        var step = ScrollChangeSize == ChangeSize.Line
            ? Math.Abs(newOffset.Y - scp.Offset.Y)
            : AssociatedObject!.Bounds.Height;

        _scroller.Scroll(AssociatedObject!, delta > 0 ? -step : step);

        e.Handled = !scp.IsScrollChainingEnabled || newOffset != scp.Offset;
    }
}
