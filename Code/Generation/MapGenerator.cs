using System;

namespace Sandbox.Generation;

public sealed class MapGenerator : Component
{
    [Property] public GenerationSettings Settings { get; set; }
    [Property] public double RandomAngle { get; set; }
    [Property] public int OffsetX { get; set; }
    [Property] public int OffsetY { get; set; }
    [Property] public double CosA { get; set; }
    [Property] public double SinA { get; set; }

    public Prng Rng { get; set; }
    public OpenSimplexNoise Noise { get; set; }
    public VoronoiFactory Voronoi { get; set; }
    
    [Property, ReadOnly] private bool _drawDelaunay = false;

    private Dictionary<Vector2, Chunk> _chunks;

    // When a map generator is invoked, it should immediately begin the generation.
    // @TODO: if a save file exists, we should pass it in and use that in place of raw generation
    // @TODO: this should be preceeded with a game menu to invoke the generation and save/load specific generations from disk.
    protected override void OnStart()
    {
        if (Settings == null) {
            throw new InvalidOperationException("Critical GenerationSettings object could not be loaded");
        }

        // create helpers
        Rng = new Prng(Settings.SeedText);
        Noise = new OpenSimplexNoise(Rng);
        RandomAngle = Rng.NextRangeDouble(0, Math.Tau);
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

    // @TODO: The save file should determine the List<Vector2> of places to do
    // default chunk revealing from, since 0,0 always gets its initial 32 chunk generation.
    // Revealing saved chunks should be a lot faster than generating new ones, so this shouldn't be as
    // expensive to run.
    // @TODO: Rather than default to 0,0 for initial generation, get a starting position sent to 
    // Generate so that alternate start locations on a map can be given.
    public void Generate()
    {
        Log.Info($"Starting level generation with seed  {Settings.SeedText}...");
        Voronoi = new VoronoiFactory(Settings, this);
        Voronoi.Generate();
        
        // clear existing chunks explicitly since Generate always starts from the beginning with the seed.
        /*_chunks = new Dictionary<Vector2, Chunk>();
        UpdateChunkRadius( 0, 0 , 1);*/
    }
    
    // Generate a number of individual chunks in a radius around the given point.
    public void UpdateChunkRadius( int centerX, int centerY, int revealRadius = 4 )
    {
	    Log.Info($"Generating {revealRadius * revealRadius} chunks around {centerX},{centerY}"  );
	    
	    int centerChunkX = centerX / Settings.ChunkGridSize;
	    int centerChunkY = centerY / Settings.ChunkGridSize;
	    
	    for ( int xOffset = -revealRadius; xOffset < revealRadius; xOffset++ )
	    {
		    for ( int yOffset = -revealRadius; yOffset < revealRadius; yOffset++ )
		    {
			    int targetX = centerChunkX + xOffset;
			    int targetY = centerChunkY + yOffset;
			    
			    // this clamp determines the square around the centerX,centerY given
			    if (targetX < -revealRadius 
			        || targetX > revealRadius 
			        || targetY < -revealRadius 
			        || targetY > revealRadius) 
				    continue;
			    
			    Vector2 chunkKey = new Vector2( targetX, targetY );
			    // @TODO: handle turning off & back on for performance if needed
			    Chunk chunk =  _chunks.GetValueOrDefault(chunkKey); 
			    
			    if ( chunk == null )
				    _chunks[chunkKey] = new Chunk(targetX, targetY, Settings, this);
		    }
	    }
	    
        // @TODO trace chunk extents to send to the camera so it can't zoom out more than the extent + a buffer
	    int minX = centerX - revealRadius * Settings.ChunkGridSize;
	    int minY = centerY - revealRadius * Settings.ChunkGridSize;
	    int maxX = centerX + revealRadius * Settings.ChunkGridSize;
	    int maxY = centerY + revealRadius * Settings.ChunkGridSize;
	    
	    Log.Info($"{centerX},{centerY} has revealed chunks from {minX},{minY} to {maxX},{maxY}"  );
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
	    if (Voronoi == null || Voronoi.DelaunayMesh == null || Voronoi.DelaunayMesh.Triangles.Length == 0 )
	    {
		    return; // stop rendering immediately if the mesh count doesn't exist
	    }
	    Log.Info("Trying to draw delaunay");

	    /*foreach ( Triangulation.Triangle triangle in Voronoi.DelaunayMesh )
	    {*/
		    // $TODO: fix w/ Delaunator
		    /*Vector3 a3D = new Vector3( triangle.A.x, triangle.A.y, 0 );
		    Vector3 b3D = new Vector3( triangle.B.x, triangle.B.y, 0 );
		    Vector3 c3D = new Vector3( triangle.C.x, triangle.C.y, 0 );
		    
		    DebugOverlay.Line( a3D, b3D, Color.Magenta, 0f );
		    DebugOverlay.Line( b3D, c3D, Color.Magenta, 0f );
		    DebugOverlay.Line( c3D, a3D, Color.Magenta, 0f );*/
	    /*}*/
    }

    [Button( "Draw Voronoi Cells " )]
    public void ToggleDrawDelaunay()
    {
	    _drawDelaunay = !_drawDelaunay;
    }
}
