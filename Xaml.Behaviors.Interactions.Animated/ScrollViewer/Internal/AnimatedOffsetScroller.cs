using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Layout;
using System;
using System.Threading.Tasks;

namespace Xaml.Behaviors.Interactions.Animated.Internal;

/// <summary>
/// Eases the offset of a scroll viewer along one axis. Wheel notches arriving during an animation
/// extend the current one instead of restarting it, so fast scrolling stays smooth.
/// </summary>
internal sealed class AnimatedOffsetScroller(Orientation orientation)
{
    private const double AnimationDuration = 170; // animation duration in milliseconds
    private const int FrameDelay = 10;            // offset update interval in milliseconds

    private static readonly SineEaseOut Easing = new();

    private ScrollViewer? _target;
    private bool _isAnimating;
    private double _startOffset;
    private double _targetOffset;
    private DateTime _animationStartTime;

    public void Scroll(ScrollViewer target, double delta)
    {
        var maxOffset = Math.Max(0, target.Extent.Get(orientation) - target.Bounds.Size.Get(orientation));
        var now = DateTime.Now;

        if (_isAnimating && ReferenceEquals(_target, target))
        {
            // Move the start point to where the running animation is right now, then extend the target.
            var progress = Math.Min((now - _animationStartTime).TotalMilliseconds / AnimationDuration, 1.0);
            _startOffset += Easing.Ease(progress) * (_targetOffset - _startOffset);
            _targetOffset = Math.Clamp(_targetOffset + delta, 0, maxOffset);
            _animationStartTime = now;
            return;
        }

        _target = target;
        _startOffset = target.Offset.Get(orientation);
        _targetOffset = Math.Clamp(_startOffset + delta, 0, maxOffset);
        _animationStartTime = now;
        _isAnimating = true;

        _ = Animate();
    }

    /// <summary>Stops the animation, so a detached behavior does not keep writing offsets.</summary>
    public void Stop()
    {
        _isAnimating = false;
        _target = null;
    }

    private async Task Animate()
    {
        while (_isAnimating && _target is { } target)
        {
            var elapsed = (DateTime.Now - _animationStartTime).TotalMilliseconds;

            if (elapsed >= AnimationDuration)
            {
                target.Offset = target.Offset.With(orientation, _targetOffset);
                _isAnimating = false;
                break;
            }

            var eased = Easing.Ease(elapsed / AnimationDuration);
            target.Offset = target.Offset.With(orientation, _startOffset + eased * (_targetOffset - _startOffset));

            await Task.Delay(FrameDelay);
        }
    }
}
