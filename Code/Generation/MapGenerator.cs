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

    [Property, Hide] private Sfc32 RNG;
    [Property, Hide] private OpenSimplexNoise Noise;

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
        RNG = new Sfc32(Seed);
        Noise = new OpenSimplexNoise(RNG);
        // @TODO: Create a NextRangeDouble which accepts float or double for this.
        // this casting seems to be rounding the radians to whole numbers.
        RandomAngle = RNG.NextRange(0, (int)Math.PI * 2); // uh..is this cast ok to do?
        CosA = Math.Cos(RandomAngle);
        SinA = Math.Sin(RandomAngle);
        OffsetX = RNG.NextRange(10000, 90000);
        OffsetY = RNG.NextRange(10000, 90000);
    }

    protected override void OnUpdate()
	{

	}

    public void Generate()
    {
        Log.Info("Starting Level Generation...");
        Voronoi = new VoronoiFactory(Settings);
        // for (int x = 0; x < Settings.WorldWidth; x++)
        // {
        //     for (int y = 0; Y < Settings.WorldHeight; y++)
        //     {

        //     }

        // }
    }
}
