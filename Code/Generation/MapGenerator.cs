using System;
using Sandbox.Triangulation;

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

    private Dictionary<Vector2, Chunk> _chunks;
    // The cell grid size divisor of the total grid shares a relationship with a viable opening generation size
    // of tile chunks.
    private int _chunksToInitialiseWith => Settings.MaxDimension / Settings.ChunkGridSize;

    // When a map generator is invoked, it should immediately begin the generation.
    // @TODO: if a save file exists, we should pass it in and use that in place of raw generation
    // @TODO: this should be preceeded with a game menu to invoke the generation and save/load specific generations from disk.
    protected override void OnStart()
    {
        Voronoi = Scene.GetAllComponents<VoronoiFactory>().FirstOrDefault();
    }

    // @TODO: The save file should determine the List<Vector2> of places to do
    // default chunk revealing from, since 0,0 always gets its initial 32 chunk generation.
    // Revealing saved chunks should be a lot faster than generating new ones, so this shouldn't be as
    // expensive to run.
    // @TODO: Rather than default to 0,0 for initial generation, get a starting position sent to 
    // Generate so that alternate start locations on a map can be given.
    public void Generate()
    {
	    Log.Info($"Randomising player-readonly configuration properties");
	    // create helpers - future steps of generation will always need to share these
	    Rng = new Prng(Settings.SeedText);
	    Noise = new OpenSimplexNoise(Rng);
	    RandomAngle = Rng.NextRangeDouble(0, Math.Tau);
	    CosA = Math.Cos(RandomAngle);
	    SinA = Math.Sin(RandomAngle);
	    OffsetX = Rng.NextRange(10000, 90000);
	    OffsetY = Rng.NextRange(10000, 90000);
	    
        Log.Info($"Starting level generation with seed  {Settings.SeedText}...");
        Voronoi.Generate();
        
        // clear existing chunks explicitly since Generate always starts from the beginning with the seed.
        _chunks = new Dictionary<Vector2, Chunk>();
        UpdateChunkRadius( 0, 0 , 1);
        // UpdateChunkRadius( 0, 0, _chunksToInitialiseWith ) // @TODO: fuck around with this
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
}
