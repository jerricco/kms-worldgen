using System;

namespace Sandbox.Generation;

[Category("Procedural Generation")]
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

    public enum NoiseType
    {
	    Paramaterless, 
    }

    public struct NoiseKnobs
    {
	    public NoiseType Type { get; set; }
	    // Frequency -> Multiplies the noise coordinates by a value between 1.0 -> 7.0
	    // Inversely, we could call this Wavelength and divide the noise by it.
	    public float Frequency { get; set; }
	    // AmplitudeBase -> Determines the base value to start the amplitude from
	    // which to derive other Amplitudes by the number of octaves.
	    public float AmplitudeBase { get; set; }
	    // Octaves -> How many times to add noise in this pass, where the calculated
	    // Amplitude has an inverse relationship to the Frequency of each successive octave.
	    public int Octaves { get; set; }
	    // Persistence -> The ratio by which to multiply the Amplitude in each Octave step.
	    public float Persistence { get; set; }
	    // Lacunarity -> The multiplier for the frequency in each Octave step.
	    public float Lacunarity { get; set; }
	    // AmplitudeWarp -> warps the divisor for the amplitude each octave pass so that the user
	    // can tweak the distribution of elevation for fractal Brownian motion. Generally we won't
	    // need to touch this.
	    public float AmplitudeWarp { get; set; }
	    // FudgeFactor -> A value (which should be near 1) which multiplies the noise right
	    // before executing its RedistributionFunction and RedistributionValue
	    public float FudgeFactor { get; set; }
	    // RedistributionValue -> The value(s) which can be passed to the function for RedistributionValue.
	    // by default, this gives the exponent for Math.Pow. Must be (by default) between 0.1 and 10.
	    public float RedistributionValue { get; set; }
	    // RedistributionFunction -> The function by which to introduce noise redistribution. Math.Pow is default.
	    // The argument is a lambda by which to evaluate the redistribution.
	    public Func<double, float, double> RedistributionFunction { get; set; }
	    
	    public NoiseKnobs(
		    NoiseType type = NoiseType.Paramaterless, 
		    float frequency = 1f,
		    float amplitudeBase = 1f,
		    int octaves = 1,
		    float persistence = 0.5f,
		    float lacunarity = 2.0f,
		    float amplitudeWarp = 0f,
		    float fudgeFactor = 1.0f,
		    float redistributionValue = 1.0f,
		    Func<double, float, double> redistributionFunction = null
		)
	    {
		    Type = type;
		    Frequency = Math.Clamp( frequency, 1f, 7f );
		    AmplitudeBase = amplitudeBase;
		    Octaves = octaves;
		    Persistence = persistence;
		    Lacunarity = lacunarity;
		    AmplitudeWarp = amplitudeWarp;
		    FudgeFactor = fudgeFactor;
		    RedistributionValue = redistributionValue;
		    
		    RedistributionFunction = redistributionFunction;
		    if ( RedistributionFunction == null )
		    {
			    RedistributionFunction = ( double n, float ex ) =>
			    {
				    float exponent = Math.Clamp( ex, 0.1f, 10f );
				    return Math.Pow( n, exponent );
			    };
		    }
	    }
    }

    public double GetNoiseLandscape( float x, float y, float halfWidth, float halfHeight, NoiseKnobs knobs = new NoiseKnobs() )
    {
	    double noise = 0d; // initialise noise
	    float amplitude = knobs.AmplitudeBase;
	    float frequency = knobs.Frequency;
	    float maxValue = 0f; // The divisor of the resultant noise to bring it into noise landscape range.
	    
	    float nx = (x - halfWidth) / halfWidth;
	    float ny = (y - halfHeight) / halfHeight;
	    
	    // cycle through octaves. With default settings, this should pass ONCE.
	    for ( int octave = 1; octave <= knobs.Octaves; octave++ )
	    {
		    // get domain-warped noise.
		    noise += Evaluate( frequency * nx, frequency * ny ) * amplitude;

		    if ( octave == knobs.Octaves ) continue; // this is our last octave;
		    
		    // update octave triggers if were doing more passes
		    // Generally this maxValue divisor (the sum of all amplitudes) is enough to distribute the elevations the way we like
		    // However, we can give it a modifier pass to create variance if we need.
		    maxValue += amplitude; 
		    amplitude *= knobs.Persistence;
		    frequency *= knobs.Lacunarity;
	    }
		
	    // level out maxValue if we are at 0 exactly - avoids div-by-zero
	    if ( maxValue == 0 ) maxValue = 1;
	    
	    // re-normalise the noise using the sum of all amplitudes (and a warp if needed)
	    noise /= (maxValue + knobs.AmplitudeWarp);

	    // distribute the elevation using a maths function -> by default its Math.Pow.
	    // @TODO: this will probably break if anyone twiddles it. But oh well, works for now.
	    noise *= knobs.FudgeFactor;
	    noise = knobs.RedistributionFunction( noise, knobs.RedistributionValue );
	    
	    return noise;
    }
}
