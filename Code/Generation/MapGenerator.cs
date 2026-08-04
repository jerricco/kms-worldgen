using System;

namespace Sandbox.Generation;

public sealed class MapGenerator : Component
{
    [Property] public GenerationSettings Settings { get; set; }
    [Property] public uint Seed;
    [Property] public double RandomAngle;
    [Property] public int OffsetX;
    [Property] public int OffsetY;
    [Property] public double CosA;
    [Property] public double SinA;

    public Sfc32 Rng;
    public OpenSimplexNoise Noise;

    private VoronoiFactory Voronoi { get; set; }
    [Property, ReadOnly] private bool _drawDelaunay = false;

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
	    if ( _drawDelaunay )
	    {
		    DrawDelaunay();
	    }
}

    public void Generate()
    {
        Log.Info($"Starting level generation with seed  {Settings.SeedText}...");
        Voronoi = new VoronoiFactory(Settings, this);
        Voronoi.Generate();
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
    
    // @DEBUG - Clickable editor button for on-demand map regeneration.
    [Button( "Regenerate Map" )]
    public void ForceRegenerate()
    {
	    OnStart(); // Re-execute boostrap calculations
	    // Remaining aretifacts are self-cleaning
	    Generate();

    }
    
    // @DEBUG - Use these methods to show the current Delaunay triangulation on-screen.
    private void DrawDelaunay()
    {
	    if (Voronoi == null || Voronoi.DelaunayMesh == null || Voronoi.DelaunayMesh.Count == 0 )
	    {
		    return; // stop rendering immediately if the mesh count doesn't exist
	    }

	    foreach ( Delaunay.Triangle triangle in Voronoi.DelaunayMesh )
	    {
		    Vector3 a3D = new Vector3( triangle.A.x, triangle.A.y, 0 );
		    Vector3 b3D = new Vector3( triangle.B.x, triangle.B.y, 0 );
		    Vector3 c3D = new Vector3( triangle.C.x, triangle.C.y, 0 );
		    
		    DebugOverlay.Line( a3D, b3D, Color.Magenta, 0f );
		    DebugOverlay.Line( b3D, c3D, Color.Magenta, 0f );
		    DebugOverlay.Line( c3D, a3D, Color.Magenta, 0f );
	    }
    }

    [Button( "Draw Voronoi Cells " )]
    public void ToggleDrawDelaunay()
    {
	    _drawDelaunay = !_drawDelaunay;
    }
}
