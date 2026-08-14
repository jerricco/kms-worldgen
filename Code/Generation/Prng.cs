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
		var seedGen = PrngExtensions.Xmur3(seed);
		this._a = seedGen();
		this._b = seedGen();
		this._c = seedGen();
		this._d = seedGen();
	}

	// Returns a floating-point number between 0 (inclusive) and 1 (exclusive)
	public float Next()
	{
		unchecked
		{
			var t = this._a + this._b;

			this._a = this._b ^ (this._b >> 9);
			this._b = this._c + (this._c << 3);
			this._c = (this._c << 21) | (this._c >> 11);
			this._d = this._d + 1;

			t += this._d;
			this._c += t;

			return (float)(t * (1.0 / 4294967296.0));
		}
	}

	/// <summary>
	/// Returns a random integer between min (inclusive) and max (exclusive).
	/// </summary>
	public int NextRange(int min, int max)
	{
		return (int)Math.Floor(this.Next() * (max - min)) + min;
	}

	/// <summary>
	/// Returns a random float between min (inclusive) and max (exclusive).
	/// </summary>
	public float NextRangeFloat(float min, float max)
	{
		return this.Next() * (max - min) + min;
	}

	/// <summary>
	/// Returns a random double between min (inclusive) and max (exclusive).
	/// </summary>
	public double NextRangeDouble(double min, double max)
	{
		return this.Next() * (max - min) + min;
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
			var h = 1779033703U ^ (uint)str.Length;

			for (var i = 0; i < str.Length; i++)
			{
				var a = (int)(h ^ str[i]);
				var b = (int)3432918353U;
				h = (uint)(a * b);

				h = (h << 13) | (h >>> 19);
			}

			return () =>
			{
				var a1 = (int)(h ^ (h >>> 16));
				var b1 = (int)2246822507U;
				h = (uint)(a1 * b1);

				var a2 = (int)(h ^ (h >>> 13));
				var b2 = (int)3266489909U;
				h = (uint)(a2 * b2);

				h ^= h >>> 16;
				return h;
			};
		}
	}
}
