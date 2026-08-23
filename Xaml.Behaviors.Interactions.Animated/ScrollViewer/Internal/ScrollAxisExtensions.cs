using Avalonia;
using Avalonia.Layout;

namespace Xaml.Behaviors.Interactions.Animated.Internal;

/// <summary>
/// Lets the shared scrolling code work with one axis at a time instead of duplicating
/// every calculation for X and Y.
/// </summary>
internal static class ScrollAxisExtensions
{
    public static double Get(this Vector vector, Orientation orientation)
        => orientation == Orientation.Vertical ? vector.Y : vector.X;

    public static Vector With(this Vector vector, Orientation orientation, double value)
        => orientation == Orientation.Vertical ? vector.WithY(value) : vector.WithX(value);

    public static double Get(this Size size, Orientation orientation)
        => orientation == Orientation.Vertical ? size.Height : size.Width;
}
