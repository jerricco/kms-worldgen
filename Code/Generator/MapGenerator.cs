using System;
using Sandbox.Triangulation;
using Sandbox.Generation;
using Sandbox.Ecology;
using System.Threading.Tasks;

namespace Sandbox.Generator;

[Category("Procedural Generation")]
public sealed class MapGenerator : Component
{
	[Property] public GenerationSettings Settings { get; set; }
	[Property, ReadOnly] public VoronoiFactory Voronoi { get; set; }
    [Property, ReadOnly] public double RandomAngle { get; set; }
    [Property, ReadOnly] public int OffsetX { get; set; }
    [Property, ReadOnly] public int OffsetY { get; set; }
    [Property, ReadOnly] public double CosA { get; set; }
    [Property, ReadOnly] public double SinA { get; set; }

    public Prng Rng { get; set; }
    public OpenSimplexNoise Noise { get; set; }
    
    // The cell grid size divisor of the total grid shares a relationship with a viable opening generation size
    // of tile chunks. Or rather, I should test whether that's going to be relevant here.
    private int _initialRadius = 4; // => Settings.MaxDimension / Settings.ChunkGridSize;
    private bool _isGenerating = false;
    private Dictionary<Vector2, Chunk> _chunks;
    
    // @TODO: if a save file exists, we should pass it in and use that in place of raw generation
    protected override void OnStart()
    {
        Voronoi = Scene.GetAllComponents<VoronoiFactory>().FirstOrDefault();
    }

    // @TODO: Use a dynamic centerpoint instead of 0,0 hardcoded.
    public Task Generate()
    {
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
        Log.Info( "===========================================================" );
        Voronoi.Generate();
        
        // clear existing chunks explicitly since Generate always starts from the beginning with the seed.
        _chunks = new Dictionary<Vector2, Chunk>();
        return UpdateChunkRadius( 0, 0, _initialRadius );
    }

    public async Task GenerateSingleChunk(int chunkX, int chunkY)
    {
	    if ( _isGenerating ) return;
	    _isGenerating = true;
	    
	    float startTime = RealTime.Now; // @DEBUG
	    int targetX = chunkX / Settings.ChunkGridSize;
	    int targetY = chunkY / Settings.ChunkGridSize;
	    Log.Info($"Generating chunk at {targetX},{targetY}" );
	    
	    Vector2 chunkKey = new Vector2( targetX, targetY );
	    var chunk = new Chunk( targetX, targetY, Settings.ChunkGridSize );
	    _chunks[chunkKey] = chunk;
	    
	    Log.Info( $"Allocated chunk. Threading to generate..." );
	    
	    // compute chunk off the main thread.
	    await Task.RunInThreadAsync( () 
		    => chunk.Generate(Settings.HalfWidth, Settings.HalfHeight, this) );
	    
	    _isGenerating = false;
	    Log.Info($"Generated chunk {targetX},{targetY} in {RealTime.Now - startTime}s"  );
    }
    
    // Generate a number of individual chunks in a radius around the given point.
    public Task UpdateChunkRadius( int centerX, int centerY, int revealRadius = 4 )
    {
	    if ( _isGenerating )
	    {
		    // @TODO: parallelise so chunkRadiusGeneration only can't happen if they cross each other's radii
		    Log.Warning( "Generation is already underway! Try again later" );
		    return Task.CompletedTask;
	    }
	    _isGenerating = true;
	    
	    if ( revealRadius < 4 )
		    throw new ArgumentException(
			    "The revealRadius should never be lower than 4 -> generating less chunks should be manual!" );
	    
	    Log.Info($"Generating {revealRadius * revealRadius} chunks around {centerX},{centerY}"  );
	    
	    List<Task> chunksToGenerate = new();
	    int centerChunkX = centerX / Settings.ChunkGridSize;
	    int centerChunkY = centerY / Settings.ChunkGridSize;
	    int negR = -revealRadius / 2; int posR = revealRadius / 2;
	    
	    // chunk allocation
	    for ( int xOffset = negR; xOffset < posR; xOffset++ )
	    {
		    for ( int yOffset = negR; yOffset < posR; yOffset++ )
		    {
			    int targetX = centerChunkX + xOffset;
			    int targetY = centerChunkY + yOffset;
			    
			    Vector2 chunkKey = new Vector2( targetX, targetY );
			    var chunk = new Chunk( targetX, targetY, Settings.ChunkGridSize );
			    _chunks[chunkKey] = chunk;
			    var threadTask = GameTask.RunInThreadAsync(() => 
				    chunk.Generate(Settings.HalfWidth, Settings.HalfHeight, this)
			    );
            
			    chunksToGenerate.Add( threadTask );
		    }
	    }

	    if ( chunksToGenerate.Count == 0 )
	    {
		    Log.Warning("No chunks found to generate! Exiting..." );
		    return Task.CompletedTask;
	    }

	    Log.Info( $"Allocated {chunksToGenerate.Count()} chunks. Threading to generate..." );
	    
	    // compute chunks off the main thread.
	    return Task.WhenAll( chunksToGenerate );

	    // @TODO trace chunk extents to send to the camera so it can't zoom out more than the extent + a buffer
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

    public Vector2 SampleWarpedDomain(float x, float y)
    {
	    // Do we care about double precision here? Probably not.
	    float warpX = (float)Noise.Evaluate((x + 200f) * 0.018f, (y + 200f) * 0.018f) * 45f;
        float warpY = (float)Noise.Evaluate((x - 200f) * 0.018f, (y - 200f) * 0.018f) * 45f;

        float sampleX = (x + OffsetX + warpX) * Settings.MacroScale;
        float sampleY = (y + OffsetY + warpY) * Settings.MacroScale;

        return new Vector2(sampleX, sampleY);
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
