using System;
using System.Collections.Concurrent;
using Sandbox.GameData;
using Sandbox.Generation;
using Sandbox.Generator;
using Sandbox.Generator.Rendering;
using Sandbox.Triangulation;
using System.Threading.Tasks;

namespace Sandbox.Systems.Map;

public class MapGeneratorSystem : GameObjectSystem<MapGeneratorSystem>
{
	[Property] public GenerationSettings Settings { get; set; }
	
	[Property] public ChunkTheme Theme { get; set; } // @TODO: this is clumsy
	[Property] public Material ElevationHeightmapMaterial { get; set; }
	[Property] public Material VoronoiLineMaterial { get; set; }
	
	// GameObjects
	public GameObject ChunkBucketGo { get; set; }
	public List<GameObject> ChunkRenderers;
	
	// data storage
	// Stores a list of chunks with their GLOBAL Vector2 coordinate as a key
	private Dictionary<Vector2, Chunk> _chunks;
	
	// orchestration
	private int _queuedChunks { get; set; }
	private int _processedChunks;
	private float _currentChunkProcessStartTime { get; set; }
	private ConcurrentQueue<Chunk> _pendingRender { get; set; }
	
	// static helpers
	public Prng Rng { get; set; }
	public OpenSimplexNoise Noise { get; set; }
	public bool SceneReady = false;
	public VoronoiFactory Voronoi;
	public List<VoronoiFactory.CurvedSpine> TectonicSpines;
	
	// seeded generation properties
	[Property, ReadOnly] public double RandomAngle { get; set; }
	[Property, ReadOnly] public int OffsetX { get; set; }
	[Property, ReadOnly] public int OffsetY { get; set; }
	[Property, ReadOnly] public double CosA { get; set; }
	[Property, ReadOnly] public double SinA { get; set; }
	
	// @TODO: Create a way to generate individual chunk(s) on demand regardless if they are already generated or not.
	
	// constructor
	public MapGeneratorSystem( Scene scene ) : base( scene )
	{
		// start scene up
		// @TODO: Scene information should determine whether to start a new generation or load one from disk.
		
		// get resources
		Settings = ResourceLibrary.Get<GenerationSettings>("default_generation.genconf");
		Theme = ResourceLibrary.Get<ChunkTheme>( "default_theme.gentheme" );
		
		// get materials
		ElevationHeightmapMaterial = Material.Load( "materials/tile_unlit.vmat" );
		VoronoiLineMaterial = Material.Load( "materials/opaque_line.vmat" );

		// lifecycle
		Listen( Stage.StartUpdate, 10, TryChunkDequeue, "Chunk Queue Tick" );
		
		InitializeScene(Settings.SeedText);
	}

	/// <summary>
	/// Initialize the MapGeneratorSystem on Scene start.
	/// </summary>
	/// <param name="seed"></param>
	public void InitializeScene( string seed )
	{
		// set private members
		_pendingRender = new ConcurrentQueue<Chunk>();
		_queuedChunks = 0;
		_processedChunks = 0;
		
		SetSeed( seed );
		
		SceneReady = true;
	}
	
	public void SetSeed( string seed )
	{
		Settings.SeedText = seed; // update seed.
		
		// set up helpers
		Rng = new Prng(seed);
		Noise = new OpenSimplexNoise(Rng);
		
		// component configuration
		Rng = new Prng(Settings.SeedText);
		Noise = new OpenSimplexNoise(Rng);
		RandomAngle = Rng.NextRangeDouble(0, Math.Tau);
		CosA = Math.Cos(RandomAngle);
		SinA = Math.Sin(RandomAngle);
		OffsetX = Rng.NextRange(10000, 90000);
		OffsetY = Rng.NextRange(10000, 90000);
		
		_chunks = new Dictionary<Vector2, Chunk>(); // new chunk register started
		ClearRenderers();
		ChunkRenderers = new List<GameObject>();
	}

	/// <summary>
	/// Do raw world generation from given startPos position with given circular chunk radius.
	/// </summary>
	/// <param name="startPos"></param>
	/// <param name="radius"></param>
	public void GenerateWorld( Vector2? startPos = null, int radius = 4, string seed = null)
	{
		if ( seed != null && seed != Settings.SeedText ) // seed has changed! clear out
		{
			Log.Warning( $"Seed was changed {Settings.SeedText}->{seed}...Regenerating..." );
			InitializeScene(seed);
		}
		
		Log.Info($"Starting level generation with seed <color=green>{Settings.SeedText}</color>...");
		Log.Info( "===========================================================" );

		if ( startPos == null )
		{
			Log.Info( "MapGeneratorSystem: Using default start position of 0,0" );
			startPos = new Vector2( 0, 0 );
		}
		
		// use the voronoi component to generate & render its structure
		// @TODO: separate generate and rendering, hide rendering behind debug flag
		Voronoi = Scene.GetAllComponents<VoronoiFactory>().FirstOrDefault();
		if ( Voronoi == null )
		{
			throw new NullReferenceException( "No Voronoi Factory is configured for this scene! Add one." );
		}
		Voronoi.Settings = Settings;
		Voronoi.LineMaterial = VoronoiLineMaterial;
		Voronoi.GenerateAndRender();
		TectonicSpines = Voronoi.TectonicSpines;
        
		// Log.Warning( Voronoi.TectonicSpines );
		// start initial world chunk generation
		GetChunkGenerationTasks(startPos, radius);
	}
	
	// @TODO store completed entire Settings.CellGridSize for skipping when every chunk in it is generated.
	/// <summary>
	/// Create new chunks inside the given radius and schedule them inside the _pendingRender queue
	/// </summary>
	/// <param name="position"></param>
	/// <param name="radius"></param>
	public void GetChunkGenerationTasks(Vector2? position, int radius = 4)
	{
		if (position == null) throw new ArgumentNullException("position", "No position given for chunk generation queue!");
		
		Log.Info($"Finding chunks in circle {radius} chunks wide around {position?.x},{position?.y}");
		_currentChunkProcessStartTime = RealTime.Now; // start timer
		var chunkQueue = GetChunkQueue( position, radius );
		var chunksToProcess = new List<Chunk>(chunkQueue.Length);
		
		// get the chunks inside the bounding radius
		Log.Info( $"Found {chunkQueue.Length} chunks, processing..." );
		for (int i = 0; i < chunkQueue.Length; i++) 
		{
			Vector2 chunkPos = chunkQueue[i];
			if ( _chunks.TryGetValue( chunkPos, out var chunk ) )
			{
				if ( chunk.Generated || chunk.Generating ) continue;
			}
			else
			{
				chunk = new Chunk(chunkPos, Settings.ChunkGridSize);
				_chunks[chunkPos] = chunk;  // put it in box
			}

			chunk.Generating = true; // immediately flag it so we never reprocess this chunk until it's finished
			chunksToProcess.Add(chunk);
		}

		if ( chunksToProcess.Count == 0 )
		{
			Log.Warning( "No chunks to count! Exiting..." );
			return;
		}
		
		Log.Info( $"We have processableCHunks of a size {chunksToProcess.Count} chunks." );

		_queuedChunks = chunksToProcess.Count;
		_processedChunks = 0;
		// start streaming immediately - fuck the police
		Log.Info( $"Amortising batch of {_queuedChunks} chunks." );
		_ = ProcessAmortisedChunkBatch(chunksToProcess);
	}
	
	/// <summary>
	/// Spawn a background thread and process chunks in amortised batches, pausing every few to yield to the game.
	/// </summary>
	/// <param name="chunksToProcess"></param>
	/// <returns></returns>
	private async Task ProcessAmortisedChunkBatch(List<Chunk> chunksToProcess)
	{
		var halfWidth = Settings.HalfWidth;
		var halfHeight = Settings.HalfHeight;
		var spines = TectonicSpines;
		
		await GameTask.RunInThreadAsync(() =>
		{
			for (int i = 0; i < chunksToProcess.Count; i++)
			{
				Chunk chunk = chunksToProcess[i];
				try
				{
					// Generate the chunk data completely off-thread
					chunk.Generate(halfWidth, halfHeight, spines);
					_pendingRender.Enqueue(chunk);
					_processedChunks++;
				}
				catch (Exception ex)
				{
					_processedChunks++;
					Log.Error($"Chunk error: {ex.Message}");
				}

				// every 3 chunks, pause for 1 millisecond.
				// this frees up the CPU thread pool and keeps S&box main loop perfectly fluid.
				if (i % 3 == 0) Task.Delay(1).Wait();
			}
		});
		
		Log.Info($"Batch complete in {(RealTime.Now - _currentChunkProcessStartTime):F2}s");
		_processedChunks = 0;
		_queuedChunks = 0;
	}
	
	/// <summary>
	/// Attempt to dequeue and render any chunks which are done generating
	/// </summary>
	private void TryChunkDequeue()
	{
		if ( !SceneReady ) return;
		// Establish a maximum amount of chunks we are allowed to visually build per frame
		// Adjust this number (e.g., 1, 2, or 3) depending on how heavy your mesh building is
		int chunksProcessedThisFrame = 0;
		int maxChunksPerFrame = 2; 

		while ( _pendingRender.TryDequeue( out Chunk chunk ) )
		{
			// Check our container GO exists and if not, create it
			if ( ChunkBucketGo == null || !ChunkBucketGo.IsValid )
			{
				// @TODO: Batch/amortize chunk buckets
				ChunkBucketGo = new GameObject(true, "ChunkBucket" ); // holds our generated chunks
				ChunkBucketGo.SetParent( Scene );
			}
			
			// Log.Info( $"Chunk_{chunk.Position.x}_{chunk.Position.y} will render now" ); // @DEBUG

			var chunkRenderGo = new GameObject( true, $"Chunk_{chunk.Position.x}_{chunk.Position.y}" );
			chunkRenderGo.SetParent( ChunkBucketGo ); // attach a rendering component to this generator's ChunkBucketGo
			chunkRenderGo.Tags.Add( "chunk" );
		    
			var renderer = chunkRenderGo.Components.Create<ChunkRenderer>();
			renderer.Settings = Settings;
			renderer.Theme = Theme;
			renderer.ChunkMaterial = ElevationHeightmapMaterial;
			// add to the chunk renderers so we can alter or destroy later
			ChunkRenderers.Add( chunkRenderGo );
		    
			renderer.RegenerateMesh(chunk); 
			chunksProcessedThisFrame++;

			// Stop processing for this frame if we hit our budget
			if ( chunksProcessedThisFrame >= maxChunksPerFrame )
			{
				break; 
			}
		}
	}

	/// <summary>
	/// Cleans up the artifacts of this GameObjectSystem so that it can be reinitialized in the Scene.
	/// </summary>
	public void Cleanup()
	{
		// clear voronoi structure
		if (Voronoi != null && Voronoi.IsValid) Voronoi.ClearData();
		
		// destroy all chunk renderers
		ClearRenderers();
		
		// destroy bucket
		if (ChunkBucketGo != null && ChunkBucketGo.IsValid) ChunkBucketGo.DestroyImmediate();
	}

	private void ClearRenderers()
	{
		if ( ChunkRenderers == null ) return;
		
		foreach ( var rendererGo in ChunkRenderers )
		{
			if (rendererGo.IsValid) rendererGo.DestroyImmediate();
		}
	}
	
	/// <summary>
	/// Retrieve a chunk from chunk space
	/// </summary>
	/// <param name="chunkX"></param>
	/// <param name="chunkY"></param>
	/// <returns>Chunk Instance</returns>
	public Chunk GetChunkAt( int chunkX, int chunkY )
	{
		if ( _chunks == null ) return null;
	    
		Vector2 chunkPos = new Vector2(chunkX, chunkY);
		if ( _chunks.TryGetValue( chunkPos, out var chunk ) )
		{
			return chunk;
		}
		else
		{
			return null;
		}
	}

	private Vector2[] GetChunkQueue( Vector2? center, int radius = 4 )
	{
	    float cxCenter = center?.x ?? 0f;
	    float cyCenter = center?.y ?? 0f;
	    int squareRadius = radius * radius;

	    // clamp chunk boundaries
	    int minChunkY = Math.Clamp((int)Math.Floor(cyCenter - radius), -Settings.ChunksY +1, Settings.ChunksY -1);
	    int maxChunkY = Math.Clamp((int)Math.Ceiling(cyCenter + radius), -Settings.ChunksY +1, Settings.ChunksY -1);
	    
	    if (minChunkY >= maxChunkY) return Array.Empty<Vector2>();

	    // count the valid chunks so we can size the array
	    int totalCount = 0;
	    
	    // Scan line by line along the Y axis to find the valid X boundaries
	    for (int cy = minChunkY; cy <= maxChunkY; cy++)
	    {
	        // For a chunk at 'cy' to be fully inside, both its top and bottom Y bounds must pass.
	        float yBound1 = cy - cyCenter;
	        float yBound2 = (cy + 1) - cyCenter;
	        float yDistSq = Math.Max(yBound1 * yBound1, yBound2 * yBound2);
	        
	        // If the vertical distance alone exceeds the radius, this entire horizontal row is invalid
	        if (yDistSq >= squareRadius) continue;

	        // Calculate the maximum horizontal distance allowed for this row
	        float maxDistX = MathF.Sqrt(squareRadius - yDistSq);
	        
	        // Find the precise start and end chunk indices that fit completely inside the horizontal chord
	        int minX = (int)Math.Ceiling(cxCenter - maxDistX);
	        int maxX = (int)Math.Floor(cxCenter + maxDistX) - 1;

	        // Clamp to world bounds
	        minX = Math.Clamp(minX, -Settings.ChunksX + 1, Settings.ChunksX -1);
	        maxX = Math.Clamp(maxX, -Settings.ChunksX + 1, Settings.ChunksX -1);

	        if (minX <= maxX)
	        {
	            totalCount += (maxX - minX + 1);
	        }
	    }

	    if (totalCount == 0) return Array.Empty<Vector2>();

	    // allocate array and populate it directly via indices
	    Vector2[] results = new Vector2[totalCount];
	    int index = 0;

	    for (int cy = minChunkY; cy <= maxChunkY; cy++)
	    {
	        float yBound1 = cy - cyCenter;
	        float yBound2 = (cy + 1) - cyCenter;
	        float yDistSq = Math.Max(yBound1 * yBound1, yBound2 * yBound2);
	        
	        if (yDistSq >= squareRadius) continue;

	        float maxDistX = MathF.Sqrt(squareRadius - yDistSq);
	        int minX = Math.Clamp((int)Math.Ceiling(cxCenter - maxDistX), -Settings.ChunksX +1, Settings.ChunksX -1);
	        int maxX = Math.Clamp((int)Math.Floor(cxCenter + maxDistX) - 1, -Settings.ChunksX +1, Settings.ChunksX -1);

	        // Fill the pre-allocated array linearly
	        for (int cx = minX; cx <= maxX; cx++)
	        {
	            results[index++] = new Vector2(cx, cy);
	        }
	    }

	    return results;
	}
}
