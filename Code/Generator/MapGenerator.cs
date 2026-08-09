using System;
using Sandbox.Triangulation;
using Sandbox.Generation;
using Sandbox.Ecology;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Sandbox.Generator.Rendering;

namespace Sandbox.Generator;

[Category("Procedural Generation")]
public sealed class MapGenerator : Component
{
	[Property] public GenerationSettings Settings { get; set; }
	[Property] public ChunkTheme Theme { get; set; }
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
    private int _initialRadius = 16; // => Settings.MaxDimension / Settings.ChunkGridSize;
    private int _totalQueueChunks = 0;
    private int _processedChunks = 0;
    private float _chunkProcessStartTime;
    private ConcurrentQueue<Chunk> _pendingVisualUpdates = new();
    private Dictionary<(float x,float y), GameObject> _chunkRenderers = new();
    [Property] public Material ChunkMaterial { get; set; }
    
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
	    if ( Settings == null || Theme == null ) return;
	    // Establish a maximum amount of chunks we are allowed to visually build per frame
	    // Adjust this number (e.g., 1, 2, or 3) depending on how heavy your mesh building is
	    int chunksProcessedThisFrame = 0;
	    int maxChunksPerFrame = 2; 

	    while ( _pendingVisualUpdates.TryDequeue( out Chunk chunk ) )
	    {
		    Log.Info( $"Chunk_{chunk.Position.x}_{chunk.Position.y} will render now" ); // @DEBUG

		    var chunkRenderGo = new GameObject( true, $"Chunk_{chunk.Position.x}_{chunk.Position.y}" );
		    chunkRenderGo.SetParent( GameObject ); // attach a rendering component to this generator's GameObject
		    var renderer = chunkRenderGo.Components.Create<ChunkRenderer>();
		    renderer.Settings = Settings; // Ensure it gets its value first
		    renderer.Theme = Theme; // Give a default theme before the component overwrites it
		    renderer.RegenerateMesh(chunk, ChunkMaterial, Settings, Theme); 
		    // add to the chunk renderers so we can alter or destroy later
		    _chunkRenderers[(chunk.Position.x, chunk.Position.y)] = chunkRenderGo;
		    
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
	    foreach ( var (_, go) in _chunkRenderers )
	    {
		    go.Destroy();
	    }
    }

    public Chunk GetChunkAt( int chunkX, int chunkY )
    {
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
    // @DEBUG - Clickable editor button for on-demand map regeneration; Generate is self-cleaning
    [Button( "Regenerate Map" )]
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
        Voronoi.GenerateAndRender();
        
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
	    GetChunkGenerationTasks(position, radius);
    }

    private void GetChunkGenerationTasks(Vector2 position, int radius = 4)
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
			    chunk = new Chunk(chunkPos, Settings.ChunkGridSize);
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
