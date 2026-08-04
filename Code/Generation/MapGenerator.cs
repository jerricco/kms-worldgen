using Sandbox;
using System;

namespace Aeons;

public sealed class MapGenerator : Component
{
    [Property] public GenerationSettings Settings { get; set; }
    [Property] public uint Seed;
    [Property] public double RandomAngle;
    [Property] public int OffsetX;
    [Property] public int OffsetY;
    [Property] public double CosA;
    [Property] public double SinA;

    [Property, Hide] public Sfc32 Rng;
    [Property, Hide] public OpenSimplexNoise Noise;

    [Property, Hide] private VoronoiFactory Voronoi { get; set; }

    // When a map generator is invoked, it should immediately begin the generation.
    // @TODO: if a save file exists, we should pass it in and use that in place of raw generation
    // @TODO: this should be preceeded with a game menu to invoke the generation and save/load specific generations from disk.
    protected override void OnStart()
    {
        if (Settings == null) {
            throw new InvalidOperationException("Critical GenerationSettings object could not be loaded");
        }

        // create helpers
        Seed = Sfc32Extensions.ToSeed(Settings.SeedText);
        Rng = new Sfc32(Seed);
        Noise = new OpenSimplexNoise(Rng);
        RandomAngle = Rng.NextRangeDouble(0, Math.PI * 2d);
        CosA = Math.Cos(RandomAngle);
        SinA = Math.Sin(RandomAngle);
        OffsetX = Rng.NextRange(10000, 90000);
        OffsetY = Rng.NextRange(10000, 90000);
    }

    protected override void OnUpdate()
	{

	}

    public void Generate()
    {
        Log.Info("Starting Level Generation...");
        Voronoi = new VoronoiFactory(Settings, this);
        // for (int x = 0; x < Settings.WorldWidth; x++)
        // {
        //     for (int y = 0; Y < Settings.WorldHeight; y++)
        //     {

        //     }

        // }
    }

    public (double sampleX, double sampleY) SampleWarpedDomain(double x, double y)
    {
        double warpX = Noise.Evaluate((x + 200d) * 0.018d, (y + 200d) * 0.018d) * 45d;
        double warpY = Noise.Evaluate((x - 200d) * 0.018d, (y - 200d) * 0.018d) * 45d;

        double sampleX = (x + OffsetX + warpX) * Settings.MacroScale;
        double sampleY = (y + OffsetY + warpY) * Settings.MacroScale;

        return ( sampleX, sampleY );
    }
}
