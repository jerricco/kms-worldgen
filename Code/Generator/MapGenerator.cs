using System;
using Sandbox.Triangulation;
using Sandbox.Generation;
using Sandbox.Ecology;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace Sandbox.Generator;

[Category("Procedural Generation")]
public sealed class MapGenerator : Component
{
	[Property] public GenerationSettings Settings { get; set; }
	[Property, ReadOnly] public VoronoiFactory Voronoi { get; set; }
	[Property, ReadOnly] public RevealRadius GameRevealer { get; set; }
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
    private int _totalQueueChunks = 0;
    private int _processedChunks = 0;
    private float _chunkProcessStartTime;
    private ConcurrentQueue<Chunk> _pendingVisualUpdates = new();
    
    // Stores a list of chunks with their GLOBAL Vector2 coordinate as a key
    private Dictionary<Vector2, Chunk> _chunks;
    
    // @TODO: if a save file exists, we should pass it in and use that in place of raw generation
    protected override void OnStart()
    {
        Voronoi = Scene.GetAllComponents<VoronoiFactory>().FirstOrDefault();
        GameRevealer = new RevealRadius( Settings.ChunkGridSize );
    }
    
    protected override void OnUpdate()
    {
	    // Establish a maximum amount of chunks we are allowed to visually build per frame
	    // Adjust this number (e.g., 1, 2, or 3) depending on how heavy your mesh building is
	    int chunksProcessedThisFrame = 0;
	    int maxChunksPerFrame = 2; 

	    while ( _pendingVisualUpdates.TryDequeue( out Chunk chunk ) )
	    {
		    // @TODO: chunk rendering
		    // BuildChunkVisualMesh( chunk ); 
		    Log.Info( $"Chunk [{chunk.ChunkX},{chunk.ChunkY}] will render now" ); // @DEBUG
        
		    chunksProcessedThisFrame++;

		    // Stop processing for this frame if we hit our budget, 
		    // letting CSwapChainBase present the frame successfully!
		    if ( chunksProcessedThisFrame >= maxChunksPerFrame )
		    {
			    break; 
		    }
	    }
    }

    // @TODO: Use a dynamic centerpoint instead of 0,0 hardcoded.
    public void Generate()
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
        
        // start initial world chunk generation
        UpdateChunkRadius(new Vector2(0,0), _initialRadius);
    }

    // @TODO: enhance this to be able to track the progress of all the chunks it adds so it can chain
    // more executions once it's done. Think concentric circles generating in sequence so that it shows a minimum area
    // to get the player playing while further out chunks continue expanding.
    public void UpdateChunkRadius( Vector2 position, int radius = 4 )
    {
	    _chunkProcessStartTime = RealTime.Now; // start timer
	    GetChunkGenerationTasks(new Vector2(0,0), _initialRadius);
    }

    public void GetChunkGenerationTasks(Vector2 position, int radius = 4)
    {
	    Log.Info($"Finding chunks in circle {radius} chunks wide around {position.x},{position.y}");
	    var chunkQueue = GameRevealer.EnumerateChunksInside( position, radius );
	    
	    // yields to the loop whenever a chunk is found in the world that needs to generate.
	    // @TODO: update camera max pan area to the largest extent of all the chunks generated
	    foreach ( Vector2 globalPos in chunkQueue )
	    {
		    var chunkPos = new Vector2( globalPos.x / Settings.ChunkGridSize, globalPos.y / Settings.ChunkGridSize );
		    // get or create a chunk
		    if ( _chunks.TryGetValue( chunkPos, out var chunk ) )
		    {
			    // don't regenerate or interrupt valid chunks
			    if ( chunk.Generated || chunk.Generating )
			    {
				    var notice = chunk.Generating ? "generating" : "already generated";
				    Log.Warning( $"Chunk at {globalPos.x},{globalPos.y} {notice}!" );
				    continue;
			    }
		    }
		    else
		    {
			    chunk = new Chunk((int)chunkPos.x, (int)chunkPos.y, Settings.ChunkGridSize);
		    }

		    _totalQueueChunks++;
		    _chunks[globalPos] = chunk; // put it in the box
		    Log.Info( $"Queued chunk [{globalPos.x},{globalPos.y}]" );
			
		    // start streaming immediately - fuck the police
		    _ = StreamChunkGeneration(chunk);
	    }
    }

    private async Task StreamChunkGeneration(Chunk chunk)
    {
	    try
	    {
		    await GameTask.RunInThreadAsync( () =>
		    {
			    chunk.Generate( Settings.HalfWidth, Settings.HalfHeight, this );
		    } );
		    
		    _processedChunks++;
		    Log.Info( $"({_processedChunks}/{_totalQueueChunks}) Chunk at {chunk.ChunkX},{chunk.ChunkY} after " +
		              $"{(RealTime.Now - _chunkProcessStartTime):F2}s! Starting render..." );
		    
		    // queue the current chunk for rendering in the next frame
		    _pendingVisualUpdates.Enqueue( chunk );
	    }
	    catch ( System.Exception ex )
	    {
		    _processedChunks++;
		    Log.Error( $"Chunk generation failed! Reason: {ex.Message}" );
	    }
	    
	    if ( _processedChunks >= _totalQueueChunks )
	    {
		    // empty queue if complete
		    Log.Info($"Chunk generation batch (size: {_totalQueueChunks}) complete!. Took {(RealTime.Now - _chunkProcessStartTime):F2} s");
		    _processedChunks = 0;
		    _totalQueueChunks = 0;
	    }
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
