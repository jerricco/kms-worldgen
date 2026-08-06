using System;
using Sandbox.Triangulation;
using Sandbox.Generation;
using Sandbox.Ecology;

namespace Sandbox.Generator;

[Category("Procedural Generation")]
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
    private int _chunksToInitialiseWith = 4; // => Settings.MaxDimension / Settings.ChunkGridSize;

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
	    float startTime = RealTime.Now; // @DEBUG
	    
	    Log.Info("Priming dynamic seeded generation properties");
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
        GenerateSingleChunk(0,0); //@DEBUG
        // UpdateChunkRadius( 0, 0, _chunksToInitialiseWith ); // @TODO: fuck around with this
        
        Log.Info($"World generation complete! Took {RealTime.Now - startTime} s");
    }

    public void GenerateSingleChunk(int chunkX, int chunkY)
    {
	    float startTime = RealTime.Now; // @DEBUG
	    int targetX = chunkX / Settings.ChunkGridSize;
	    int targetY = chunkY / Settings.ChunkGridSize;
	    Log.Info($"Generating chunk at {targetX},{targetY}" );
	    
	    Vector2 chunkKey = new Vector2( targetX, targetY );
	    // @TODO: handle turning off & back on for performance if needed
	    Chunk chunk =  _chunks.GetValueOrDefault(chunkKey); 
			    
	    if ( chunk == null )
		    _chunks[chunkKey] = new Chunk(targetX, targetY, Settings, this);
	    
	    Log.Info($"Chunk {targetX},{targetY} in {RealTime.Now - startTime}s"  );
    }
    
    // Generate a number of individual chunks in a radius around the given point.
    public void UpdateChunkRadius( int centerX, int centerY, int revealRadius = 4 )
    {
	    if ( revealRadius < 4 )
	    {
		    throw new ArgumentException(
			    "The revealRadius should never be lower than 4 -> generating less chunks should be manual!" );
	    }
	    
	    Log.Info($"Generating {revealRadius * revealRadius} chunks around {centerX},{centerY}"  );
	    
	    float startTime = RealTime.Now; // @DEBUG
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
	    
	    Log.Info($"{centerX},{centerY} has revealed chunks from {minX},{minY} to {maxX},{maxY} in {RealTime.Now - startTime}s"  );
    }
    
	/**
     * PASS 1: ELEVATION EVALUATION
     * Determine the seeded elevation value for the tile using the provided
	 * Voronoi field structure along with given OpenSimplexNoise
    */
	public double GetTileElevation( float x, float y, DelaunayNeighbors neighbors )
	{
		// clamp inside world
		float globalX = Math.Max( -Settings.HalfWidth, Math.Min(Settings.HalfWidth, x) );
		float globalY = Math.Max( -Settings.HalfHeight, Math.Min(Settings.HalfHeight, y) );

		double elevation; // init the elevation -> should never reach the end null
		
		// Select from the appropriate site structure
		if ( neighbors.Count == 0 )
			elevation = Settings.AbyssalLevel; // fallback
		else if ( neighbors.Count < 2 )
			elevation = neighbors.Candidate2.Site.BaseElevation;
		else
			elevation = BuildTileTectonicSuperstructure(globalX, globalY, neighbors);

		// add noisy detail
		elevation = BuildTileNoisyGeologicalDetail(elevation);
		
		// return clamped elevation
		return Math.Max( -1.0d, Math.Min( 1.0d, elevation ) );
	}
	
	private double BuildTileTectonicSuperstructure( float x, float y, DelaunayNeighbors neighbors )
	{
		// pick out plate weight candidates
		int plate0Id = neighbors.Candidate0.Site.PlateId;
		float plate0Weight = 0f;

		int plate1Id = neighbors.Candidate1.Site.PlateId;
		float plate1Weight = 0f;

		int plate2Id = -1;
		float plate2Weight = 0f;
		bool hasThirdNeighbor = neighbors.Count >= 3;
		if ( hasThirdNeighbor )
			plate2Id = neighbors.Candidate2.Site.PlateId;

		float totalWeight = 0f;
		double baseInterpolatedElevation = 0d;
		
		// do mathy math
		float d0Max = Math.Max( 1.0f, neighbors.Candidate0.DistanceSq );
		float w1 = 1.0f / ( d0Max * d0Max );
		totalWeight += w1;
		baseInterpolatedElevation += neighbors.Candidate0.Site.BaseElevation * w1;
		plate0Weight += w1;

		float d1Max = Math.Max( 1.0f, neighbors.Candidate1.DistanceSq );
		float w2 = 1.0f / ( d1Max * d1Max );
		totalWeight += w2;
		baseInterpolatedElevation += neighbors.Candidate1.Site.BaseElevation * w2;
		
		// accumulate weights
		if ( plate1Id == plate0Id )
			plate0Weight += w2;
		else
			plate1Weight += w2;
		
		if ( hasThirdNeighbor )
		{
			float d2Max = Math.Max( 1.0f, neighbors.Candidate2.DistanceSq );
			float w3 = 1.0f / ( d2Max * d2Max );
			totalWeight += w3;
			baseInterpolatedElevation += neighbors.Candidate2.Site.BaseElevation * w3;
		
			if ( plate2Id == plate0Id )
				plate0Weight += w3;
			else if ( plate2Id == plate1Id )
				plate1Weight += w3;
			else
				plate2Weight += w3;
		}
		
		// begin final elevation evaluation
		double elevation = baseInterpolatedElevation / totalWeight;
		// Determine how many unique plates actually collected weights
		int uniquePlateCount = 1;
		if ( plate1Weight > 0f ) uniquePlateCount++;
		if ( plate2Weight > 0f ) uniquePlateCount++;

		// Find the two highest weights with branch sorting
		if ( uniquePlateCount > 1 )
		{
			float primaryInfluence;
			float secondaryInfluence;

			if ( plate0Weight >= plate1Weight )
			{
				if ( plate0Weight >= plate2Weight )
				{
					primaryInfluence = plate0Weight;
					secondaryInfluence = Math.Max( plate1Weight, plate2Weight );
				}
				else
				{
					primaryInfluence = plate2Weight;
					secondaryInfluence = plate0Weight;
				}
			}
			else
			{
				if ( plate1Weight >= plate2Weight )
				{
					primaryInfluence = plate1Weight;
					secondaryInfluence = Math.Max( plate0Weight, plate2Weight );
				}
				else
				{
					primaryInfluence = plate2Weight;
					secondaryInfluence = plate1Weight;
				}
			}

			primaryInfluence /= totalWeight;
			secondaryInfluence /= totalWeight;
			
			float boundaryFriction = Math.Min( primaryInfluence, secondaryInfluence ) * 2.0f;
			if ( boundaryFriction > 0.05f && !neighbors.Candidate0.Site.IsOceanic && !neighbors.Candidate1.Site.IsOceanic )
			{
				var boundaryShape = boundaryFriction * boundaryFriction * (3.0 - 2.0 * boundaryFriction);
				var baseMountainHeight = Math.Max(neighbors.Candidate0.Site.BaseElevation, neighbors.Candidate1.Site.BaseElevation );
				var targetSpineHeight = MathX.Lerp( baseMountainHeight, Settings.PeakLevel - 0.02f, boundaryShape * 0.7f );
				elevation = Math.Max( elevation, targetSpineHeight );
			}
		}

		if ( !neighbors.Candidate0.Site.IsOceanic )
		{
			var landCoreFactor = Math.Max( 0.0d, neighbors.Candidate0.Site.BaseElevation - Settings.SeaLevel );
			elevation += landCoreFactor * 0.32;
		}
		
		return Math.Max(-1.0d, Math.Min(1.0d, elevation));
	}
	
	private double BuildTileNoisyGeologicalDetail( double elevation )
	{
		return elevation; // @TODO: Does nothing for now.
	}

	/**
     * PASS 2: GEOLOGICAL EVALUATION
     * Find the geological structure of the space underneath the tile, so we know
	 * how to later fill it with biome data.
    */
	public SubterraneanLayer GetTileGeology( float x, float y, double elevation )
	{
		return new SubterraneanLayer( 0, 0, BasementRockType.Basalt ); // @TODO: Does nothing for now
	}

	/**
     * PASS X: DETERMINE TILE REGION
     * Use the determinate data of a tile to find what region it belongs to.
	 * @NOTE: currently only accepts elevation data.
    */
	public RegionId GetTileRegion( double elevation )
	{
		return RegionId.Unassigned; // @TODO: Does nothing for now.
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
