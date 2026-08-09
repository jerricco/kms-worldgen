using System;

namespace Sandbox.Generation;

[Category("Procedural Generation")]
public class Prng
{
    // The internal 4-word state required by the SFC32 algorithm
    private uint _a;
    private uint _b;
    private uint _c;
    private uint _d;

    /// <summary>
    /// Initializes the generator using a single integer seed.
    /// </summary>
    public Prng(string seed)
    {
	    Func<uint> seedGen = PrngExtensions.Xmur3( seed );
	    _a = seedGen();
	    _b = seedGen();
	    _c = seedGen();
	    _d = seedGen();
    }

    // Returns a floating-point number between 0 (inclusive) and 1 (exclusive)
    public float Next()
    {
	    unchecked
	    {
		    uint t = _a + _b;
            
		    _a = _b ^ (_b >> 9);
		    _b = _c + (_c << 3);
		    _c = (_c << 21) | (_c >> 11);
		    _d = _d + 1;
            
		    t += _d;
		    _c += t;
            
		    return (float)(t * (1.0 / 4294967296.0));
	    }
    } 

    /// <summary>
    /// Returns a random integer between min (inclusive) and max (exclusive).
    /// </summary>
    public int NextRange(int min, int max)
    {
	    return (int)Math.Floor( Next() * (max - min) ) + min;
    }

    /// <summary>
    /// Returns a random float between min (inclusive) and max (exclusive).
    /// </summary>
    public float NextRangeFloat(float min, float max)
    {
	    return Next() * (max - min) + min;
    }
    
    /// <summary>
    /// Returns a random double between min (inclusive) and max (exclusive).
    /// </summary>
    public double NextRangeDouble(double min, double max)
    {
	    return Next() * (max - min) + min;
    }
}

public static class PrngExtensions 
{
    /// <summary>
    /// Converts any text string into a pseudo-random, deterministic uint 
    /// using the XMUR3 hashing algorithm.
    /// </summary>
    public static Func<uint> Xmur3(string str)
    {
	    unchecked
	    {
		    uint h = 1779033703U ^ (uint)str.Length;
        
		    for (int i = 0; i < str.Length; i++)
		    {
			    int a = (int)(h ^ str[i]);
			    int b = (int)3432918353U;
			    h = (uint)(a * b);
            
			    h = (h << 13) | (h >>> 19);
		    }
        
		    return () =>
		    {
			    int a1 = (int)(h ^ (h >>> 16));
			    int b1 = (int)2246822507U;
			    h = (uint)(a1 * b1);
            
			    int a2 = (int)(h ^ (h >>> 13));
			    int b2 = (int)3266489909U;
			    h = (uint)(a2 * b2);
            
			    h ^= h >>> 16;
			    return h;
		    };
	    }
    }
}
