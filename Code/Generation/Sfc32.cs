using System;

namespace Sandbox.Generation;

public class Sfc32
{
    // The internal 4-word state required by the SFC32 algorithm
    private uint _a;
    private uint _b;
    private uint _c;
    private uint _d;

    /// <summary>
    /// Initializes the generator using a single integer seed.
    /// </summary>
    public Sfc32(uint seed)
    {
        // SFC32 requires 4 initial state values. 
        // We use a simple mix to distribute your single integer seed across all 4.
        _a = seed;
        _b = seed ^ 0x9E3779B9;
        _c = (seed << 16) | (seed >> 16);
        _d = 1;

        // Run the generator 12 times to "warm it up" and mix the initial state.
        for (int i = 0; i < 12; i++)
        {
            NextUInt();
        }
    }

    /// <summary>
    /// Generates a raw pseudo-random unsigned 32-bit integer.
    /// This is the core mathematical SFC32 algorithm.
    /// </summary>
    public uint NextUInt()
    {
        uint tmp = _a + _b + _d;
        _d = _d + 1;
        _a = _b ^ (_b >> 9);
        _b = _c + (_c << 3);
        _c = ((_c << 21) | (_c >> 11)) + tmp;
        return tmp;
    }

    /// <summary>
    /// Returns a random float between 0.0 (inclusive) and 1.0 (exclusive).
    /// </summary>
    public float NextFloat()
    {
        // Divide by uint.MaxValue + 1 to scale the number into a 0.0 to 1.0 range
        return (float)NextUInt() / 4294967296f;
    }

    /// <summary>
    /// Returns a random integer between min (inclusive) and max (exclusive).
    /// </summary>
    public int NextRange(int min, int max)
    {
        if (min >= max) return min;

        uint range = (uint)(max - min);
        return (int)(min + (NextUInt() % range));
    }

    /// <summary>
    /// Returns a random float between min (inclusive) and max (exclusive).
    /// </summary>
    public float NextRangeFloat(float min, float max)
    {
        if (min >= max) return min;

        float range = (float)(max - min);
        return (float)(min + (NextFloat() % range));
    }
    
    
    /// <summary>
    /// Returns a random double between min (inclusive) and max (exclusive).
    /// </summary>
    public double NextRangeDouble(double min, double max)
    {
	    if (min >= max) return min;

	    double range = max - min;
	    return min + NextFloat() % range;
    }
}

public static class Sfc32Extensions 
{
    /// <summary>
    /// Converts any text string into a pseudo-random, deterministic uint 
    /// using the FNV-1a hashing algorithm.
    /// </summary>
    public static uint ToSeed(string text)
    {
        // If the string is empty or null, return a fallback default seed
        if (string.IsNullOrEmpty(text)) return 1337;

        // FNV-1a Constants for 32-bit numbers
        uint hash = 2166136261;
        uint prime = 16777619;

        // Loop through every letter in your text string
        foreach (char c in text)
        {
            // XOR the low 8-bits of the character into the hash
            hash ^= (byte)c;
            // Multiply by the magical FNV prime number to scramble the bits
            hash *= prime;
        }

        return hash;
    }
}
