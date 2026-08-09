using System;
using Sandbox.Triangulation;
using Sandbox.Generation;
using Sandbox.Ecology;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Sandbox.Gameplay;
using Sandbox.Generator.Rendering;

namespace Sandbox.Generator;

[Category("Procedural Generation")]
public sealed class MapGenerator : Component
{
	// assets
	[Property] public GenerationSettings Settings { get; set; }
	[Property] public ChunkTheme Theme { get; set; }
	[Property] public Material ChunkMaterial { get; set; }
	
	// gameobjects & components
	public GameObject VoronoiFactoryGo { get; set; }
	public VoronoiFactory Voronoi { get; set; }
	public TileInteractionManager TileInteraction { get; set; }
	
	public GameObject ChunkBucketGo { get; set; }
	private Dictionary<(float x,float y), GameObject> _chunkRenderers;
	
	[Property, ReadOnly] public double RandomAngle { get; set; }
    [Property, ReadOnly] public int OffsetX { get; set; }
    [Property, ReadOnly] public int OffsetY { get; set; }
    [Property, ReadOnly] public double CosA { get; set; }
    [Property, ReadOnly] public double SinA { get; set; }

    public Prng Rng { get; set; }
    public OpenSimplexNoise Noise { get; set; }
    
    private int _initialRadius = 32; // => Settings.MaxDimension / Settings.ChunkGridSize;
    private int _totalQueueChunks = 0;
    private int _processedChunks = 0;
    private float _chunkProcessStartTime;
    private ConcurrentQueue<Chunk> _pendingVisualUpdates = new();
    
    // Stores a list of chunks with their GLOBAL Vector2 coordinate as a key
    private Dictionary<Vector2, Chunk> _chunks;
    
    // @TODO: if a save file exists, we should pass it in and use that in place of raw generation
    protected override void OnStart()
    {
	    Log.Info("MapGenerator: Getting assets");
	    // load assets & defaults
	    Settings = Settings ?? ResourceLibrary.Get<GenerationSettings>("default_generation.genconf");
	    Theme = Theme ?? ResourceLibrary.Get<ChunkTheme>( "default_theme.gentheme" );
	    ChunkMaterial = ChunkMaterial ?? Material.Load( "materials/tile_unlit.vmat" );
	    
	    Log.Info("MapGenerator: Priming dynamic seeded generation properties");
	    // component configuration
	    Rng = new Prng(Settings.SeedText);
	    Noise = new OpenSimplexNoise(Rng);
	    RandomAngle = Rng.NextRangeDouble(0, Math.Tau);
	    CosA = Math.Cos(RandomAngle);
	    SinA = Math.Sin(RandomAngle);
	    OffsetX = Rng.NextRange(10000, 90000);
	    OffsetY = Rng.NextRange(10000, 90000);
	    
	    Log.Info("MapGenerator: Creating dependencies");
	    // Create/Retrieve child components
	    VoronoiFactoryGo = new GameObject( true, $"VoronoiFactory_{Guid.NewGuid()}" );
	    VoronoiFactoryGo.SetParent( GameObject );
        
	    Voronoi = VoronoiFactoryGo.GetOrAddComponent<VoronoiFactory>();
	    Voronoi.Settings = Settings;
	    Voronoi.LineMaterial = ChunkMaterial;
	    Voronoi.Rng = Rng;
        
	    ChunkBucketGo = new GameObject(true, $"ChunkBucket_{Guid.NewGuid()}" ); // holds our generated chunks
	    ChunkBucketGo.SetParent( GameObject );
	    _chunkRenderers = new();
	    
        TileInteraction = GameObject.AddComponent<TileInteractionManager>();
    }
    
    protected override void OnUpdate()
    {
	    if ( Settings == null || Theme == null ) return;
	    // Establish a maximum amount of chunks we are allowed to visually build per frame
	    // Adjust this number (e.g., 1, 2, or 3) depending on how heavy your mesh building is
	    int chunksProcessedThisFrame = 0;
	    int maxChunksPerFrame = 2; 

	    while ( _pendingVisualUpdates.TryDequeue( out Chunk chunk ) )
	    {
		    // Log.Info( $"Chunk_{chunk.Position.x}_{chunk.Position.y} will render now" ); // @DEBUG

		    var chunkRenderGo = new GameObject( true, $"Chunk_{chunk.Position.x}_{chunk.Position.y}" );
		    chunkRenderGo.SetParent( ChunkBucketGo ); // attach a rendering component to this generator's ChunkBucketGo
		    var renderer = chunkRenderGo.Components.Create<ChunkRenderer>();
		    renderer.Settings = Settings;
		    renderer.Theme = Theme;
		    renderer.ChunkMaterial = ChunkMaterial;
		    // add to the chunk renderers so we can alter or destroy later
		    _chunkRenderers[(chunk.Position.x, chunk.Position.y)] = chunkRenderGo;
		    
		    renderer.RegenerateMesh(chunk); 
		    chunksProcessedThisFrame++;

		    // Stop processing for this frame if we hit our budget, 
		    // letting CSwapChainBase present the frame successfully!
		    if ( chunksProcessedThisFrame >= maxChunksPerFrame )
		    {
			    break; 
		    }
	    }
    }

    protected override void OnDestroy()
    {
	    // kill dependencies
	    TileInteraction.Destroy();
	    
	    // Destroy all chunkrenderers
	    foreach ( var (_, go) in _chunkRenderers )
	    {
		    go.Destroy();
	    }
	    
	    // then our bucket
	    ChunkBucketGo.Destroy();
	    // kill the voronoi
	    Voronoi.Destroy();
	    VoronoiFactoryGo.Destroy();
    }

    public Chunk GetChunkAt( int chunkX, int chunkY )
    {
	    if ( _chunks == null ) return null;
	    
	    Vector2 chunkPos = new Vector2(chunkX * Settings.ChunkGridSize, chunkY * Settings.ChunkGridSize);
	    if ( _chunks.TryGetValue( chunkPos, out var chunk ) )
	    {
		    return chunk;
	    }
	    else
	    {
		    return null;
	    }
    }

    // @TODO: Use a dynamic centerpoint instead of 0,0 hardcoded.
    public void Generate()
    {
        Log.Info($"Starting level generation with seed  {Settings.SeedText}...");
        Log.Info( "===========================================================" );
        Voronoi.GenerateAndRender();
        
        // clear existing chunks explicitly since Generate always starts from the beginning with the seed.
        _chunks = new Dictionary<Vector2, Chunk>();
        
        // start initial world chunk generation
        GetChunkGenerationTasks(new Vector2(0,0), _initialRadius);
    }

    private void GetChunkGenerationTasks(Vector2 position, int radius = 4)
    {
	    Log.Info($"Finding chunks in circle {radius} chunks wide around {position.x},{position.y}");
	    _chunkProcessStartTime = RealTime.Now; // start timer
	    var chunkQueue = EnumerateChunksInside( position, radius );

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
			    chunk = new Chunk(chunkPos, Settings.ChunkGridSize);
		    }

		    _totalQueueChunks++;
		    _chunks[globalPos] = chunk; // put it in the box
		    Log.Info( $"Queued chunk [{globalPos.x},{globalPos.y}]" );

		    // start streaming immediately - fuck the police
		    _ = StreamChunkGeneration(chunk);
	    }
    }
    
    /// <summary>
    /// Scans the circular field and yields the top-left global space coordinate of every 
    /// grid-aligned chunk that fits completely inside the radius. This function searches grid space
    /// </summary>
    private IEnumerable<Vector2> EnumerateChunksInside( Vector2 center, int radius = 4 )
    {
	    int squareRadius = radius * radius;
	    // calculate boundaries purely in CHUNK INDEX coordinates
	    int minChunkX = (int)center.x - radius;
	    int maxChunkX = (int)center.x + radius;
	    int minChunkY = (int)center.y - radius;
	    int maxChunkY = (int)center.y + radius;

        Log.Warning( $"Chunk box created around {minChunkX},{minChunkY} to {maxChunkX},{maxChunkY} in chunk space" );
        
        // loop through chunk indices
        for ( int cx = minChunkX; cx <= maxChunkX; cx++ )
        {
	        for ( int cy = minChunkY; cy <= maxChunkY; cy++ )
	        {
		        // Define the 4 corners of this chunk in chunk space
		        float left = cx;
		        float right = cx + 1;
		        float top = cy;
		        float bottom = cy - 1;

		        // strict containment check (Compare chunk index distances directly to index radius)
		        float dxCenter = center.x;
		        float dyCenter = center.y;

		        bool topLeftIn = ((left - dxCenter) * (left - dxCenter)) 
									+ ((top - dyCenter) * (top - dyCenter)) <= squareRadius;
		        bool topRightIn = ((right - dxCenter) * (right - dxCenter)) 
									+ ((top - dyCenter) * (top - dyCenter)) <= squareRadius;
		        bool bottomLeftIn = ((left - dxCenter) * (left - dxCenter)) 
		                            + ((bottom - dyCenter) * (bottom - dyCenter)) <= squareRadius;
		        bool bottomRightIn = ((right - dxCenter) * (right - dxCenter)) 
									+ ((bottom - dyCenter) * (bottom - dyCenter)) <= squareRadius;

		        // if all four corners fit inside the index radius, convert to global world coordinates and yield
		        if ( topLeftIn && topRightIn && bottomLeftIn && bottomRightIn )
		        {
			        float globalX = cx * Settings.ChunkGridSize;
			        float globalY = cy * Settings.ChunkGridSize;
			        yield return new Vector2( globalX, globalY );
		        }
	        }
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
		    Log.Info( $"({_processedChunks}/{_totalQueueChunks}) Chunk at {chunk.Position.x},{chunk.Position.y} after " +
		              $"{(RealTime.Now - _chunkProcessStartTime):F2}s!" );
		    
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

    public Vector2 SampleWarpedDomain(float x, float y)
    {
	    // Do we care about double precision here? Probably not.
	    float warpX = (float)Noise.Evaluate((x + 200f) * 0.018f, (y + 200f) * 0.018f) * 45f;
        float warpY = (float)Noise.Evaluate((x - 200f) * 0.018f, (y - 200f) * 0.018f) * 45f;

        float sampleX = (x + OffsetX + warpX) * Settings.MacroScale;
        float sampleY = (y + OffsetY + warpY) * Settings.MacroScale;

        return new Vector2(sampleX, sampleY);
    }
}
