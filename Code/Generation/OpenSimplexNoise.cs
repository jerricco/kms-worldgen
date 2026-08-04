using System;

namespace Sandbox.Generation;

public class OpenSimplexNoise
{
    private const double StretchConstant2D = -0.211324865405187; // (1 / Math.Sqrt(2 + 1) - 1) / 2
    private const double SquishConstant2D = 0.366025403784439;  // (Math.Sqrt(2 + 1) - 1) / 2
    private const double NormConstant2D = 47.0;

    private readonly short[] _perm;

    // Gradients for 2D OpenSimplex
    private static readonly sbyte[] Gradients2D = {
         5,  2,    2,  5,   -5,  2,   -2,  5,
         5, -2,    2, -5,   -5, -2,   -2, -5
    };

    public OpenSimplexNoise(Prng rng)
    {
	    _perm = new short[256];
        short[] source = new short[256];
        for (short i = 0; i < 256; i++) source[i] = i;

        for (int i = 255; i >= 0; i--)
        {
            int r = rng.NextRange(0, i + 1);
            _perm[i] = source[r];
            source[r] = source[i];
        }
    }

    public double Evaluate(double x, double y)
    {
        // Place input coordinates onto grid
        double stretchOffset = (x + y) * StretchConstant2D;
        double xs = x + stretchOffset;
        double ys = y + stretchOffset;

        // Floor to get grid coordinates of rhombus super-cell origin
        int xsb = (int)Math.Floor(xs);
        int ysb = (int)Math.Floor(ys);

        // Skew back to relative internal coordinates
        double squishOffset = (xsb + ysb) * SquishConstant2D;
        double xb = xsb + squishOffset;
        double yb = ysb + squishOffset;

        // Positions relative to origin reference point
        double x0 = x - xb;
        double y0 = y - yb;

        // Determine which of the two triangles we're in within the rhombus
        double xins = xs - xsb;
        double yins = ys - ysb;
        double inSum = xins + yins;

        double hash;
        double value = 0;

        // Contribution from first corner (0,0)
        double attn0 = 2 - x0 * x0 - y0 * y0;
        if (attn0 > 0)
        {
            attn0 *= attn0;
            hash = _perm[(_perm[xsb & 0xFF] + ysb) & 0xFF] & 0x0E;
            value += attn0 * attn0 * (Gradients2D[(int)hash] * x0 + Gradients2D[(int)hash + 1] * y0);
        }

        // Contribution from second corner (1,1)
        double x1 = x0 - 1.0 - 2.0 * SquishConstant2D;
        double y1 = y0 - 1.0 - 2.0 * SquishConstant2D;
        double attn1 = 2 - x1 * x1 - y1 * y1;
        if (attn1 > 0)
        {
            attn1 *= attn1;
            hash = _perm[(_perm[(xsb + 1) & 0xFF] + (ysb + 1)) & 0xFF] & 0x0E;
            value += attn1 * attn1 * (Gradients2D[(int)hash] * x1 + Gradients2D[(int)hash + 1] * y1);
        }

        // Contribution from the remaining internal corner points
        if (inSum <= 1)
        {
            // Inside the (0,0)-(1,0)-(0,1) triangle
            if (xins > yins)
            {
                double x2 = x0 - 1.0 - SquishConstant2D;
                double y2 = y0 - SquishConstant2D;
                double attn2 = 2 - x2 * x2 - y2 * y2;
                if (attn2 > 0)
                {
                    attn2 *= attn2;
                    hash = _perm[(_perm[(xsb + 1) & 0xFF] + ysb) & 0xFF] & 0x0E;
                    value += attn2 * attn2 * (Gradients2D[(int)hash] * x2 + Gradients2D[(int)hash + 1] * y2);
                }
            }
            else
            {
                double x2 = x0 - SquishConstant2D;
                double y2 = y0 - 1.0 - SquishConstant2D;
                double attn2 = 2 - x2 * x2 - y2 * y2;
                if (attn2 > 0)
                {
                    attn2 *= attn2;
                    hash = _perm[(_perm[xsb & 0xFF] + (ysb + 1)) & 0xFF] & 0x0E;
                    value += attn2 * attn2 * (Gradients2D[(int)hash] * x2 + Gradients2D[(int)hash + 1] * y2);
                }
            }
        }
        else
        {
            // Inside the (1,1)-(1,0)-(0,1) triangle
            if (xins < yins)
            {
                double x2 = x0 + SquishConstant2D;
                double y2 = y0 - 1.0 + SquishConstant2D;
                double attn2 = 2 - x2 * x2 - y2 * y2;
                if (attn2 > 0)
                {
                    attn2 *= attn2;
                    hash = _perm[(_perm[xsb & 0xFF] + (ysb + 1)) & 0xFF] & 0x0E;
                    value += attn2 * attn2 * (Gradients2D[(int)hash] * x2 + Gradients2D[(int)hash + 1] * y2);
                }
            }
            else
            {
                double x2 = x0 - 1.0 + SquishConstant2D;
                double y2 = y0 + SquishConstant2D;
                double attn2 = 2 - x2 * x2 - y2 * y2;
                if (attn2 > 0)
                {
                    attn2 *= attn2;
                    hash = _perm[(_perm[(xsb + 1) & 0xFF] + ysb) & 0xFF] & 0x0E;
                    value += attn2 * attn2 * (Gradients2D[(int)hash] * x2 + Gradients2D[(int)hash + 1] * y2);
                }
            }
        }

        return value / NormConstant2D;
    }
}
