namespace Sandbox.Extensions;

/// <summary>
/// Provides extension methods for <see cref="Vector2" />.
/// </summary>
public static class Vector2Extensions
{
    /// <summary>
    /// Calculates the shortest distance from the source point to the line segment
    /// defined by points <paramref name="a"/> and <paramref name="b"/>.
    /// </summary>
    /// <param name="source">The point from which to measure the distance.</param>
    /// <param name="a">The first endpoint of the line segment.</param>
    /// <param name="b">The second endpoint of the line segment.</param>
    /// <returns>The shortest distance from <paramref name="source"/> to the line segment.</returns>
    public static float DistanceToSegment(this Vector2 source, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        var ap = source - a;
        var r = Vector2.Dot(ap, ab) / Vector2.Dot(ab, ab);

        switch (r)
        {
            case <= 0.0f:
                return Vector2.Distance(source, a);
            case >= 1.0f:
                return Vector2.Distance(source, b);
            default:
            {
                var closestPoint = a + (r * ab);
                return Vector2.Distance(source, closestPoint);
            }
        }
    }
}
