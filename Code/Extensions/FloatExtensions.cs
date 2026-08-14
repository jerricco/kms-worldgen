namespace Sandbox.Extensions;

using System;

/// <summary>
/// Provides extension methods for <see cref="float" />.
/// </summary>
public static class FloatExtensions
{
    /// <summary>
    /// Applies smooth Hermite interpolation to a value between two edges,
    /// mapping the input range to the range [0, 1] with zero slope at both ends.
    /// </summary>
    /// <param name="edge0">The lower edge of the input range.</param>
    /// <param name="edge1">The upper edge of the input range.</param>
    /// <param name="source">The input value to interpolate.</param>
    /// <returns>A smoothly interpolated value between 0 and 1.</returns>
    public static float SmoothStep(this float source, float edge0, float edge1)
    {
        // Clamp and normalise x between 0.0 and 1.0
        var t = Math.Clamp((source - edge0) / (edge1 - edge0), 0.0f, 1.0f);

        // Evaluate the cubic Hermite polynomial
        return t * t * (3.0f - (2.0f * t));
    }

    /// <summary>
    /// Smoothly blends two values using a smooth minimum, with the lower value
    /// dominating the result while providing a continuous transition between them.
    /// </summary>
    /// <param name="first">The first value to blend.</param>
    /// <param name="second">The second value to blend.</param>
    /// <param name="factor">The smoothing factor controlling the width of the blend region.</param>
    /// <returns>A smoothly blended value approximating the minimum of <paramref name="first"/> and <paramref name="second"/>.</returns>
    public static float SmoothMin(this float first, float second, float factor)
    {
        var h = Math.Clamp(0.5f + (0.5f * (second - first) / factor), 0.0f, 1.0f);
        return MathX.Lerp(second, first, h) - (factor * h * (1.0f - h));
    }
}
