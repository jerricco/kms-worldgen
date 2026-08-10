using System;
using System.Collections.Concurrent;
using Sandbox.GameData;
using Sandbox.Generation;
using Sandbox.Generator;
using Sandbox.Generator.Rendering;
using Sandbox.Triangulation;
using System.Threading.Tasks;

namespace Sandbox.GameObjectSystems.Map;

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
	private int _processedChunks { get; set; }
	private float _currentChunkProcessStartTime { get; set; }
	private ConcurrentQueue<Chunk> _pendingRender { get; set; }
	
	// static helpers
	public Prng Rng { get; set; }
	public OpenSimplexNoise Noise { get; set; }
	public bool SceneReady = false;
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
		VoronoiLineMaterial = Material.Load( "materials/tile_unlit.vmat" );

		// lifecycle
		Listen( Stage.StartUpdate, 10, TryChunkDequeue, "Chunk Queue Tick" );
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
		
		_chunks = new Dictionary<Vector2, Chunk>(); // new chunk register started
		
		Settings.SeedText = seed; // update seed.
		
		// set up helpers
		Rng = new Prng(seed);
		Noise = new OpenSimplexNoise(Rng);
		
		Log.Info("MapGenerator: Priming dynamic seeded generation properties");
		// component configuration
		Rng = new Prng(Settings.SeedText);
		Noise = new OpenSimplexNoise(Rng);
		RandomAngle = Rng.NextRangeDouble(0, Math.Tau);
		CosA = Math.Cos(RandomAngle);
		SinA = Math.Sin(RandomAngle);
		OffsetX = Rng.NextRange(10000, 90000);
		OffsetY = Rng.NextRange(10000, 90000);
		
		// will be filled manually later with GOs
		ChunkRenderers = new List<GameObject>();
		SceneReady = true;
	}

	/// <summary>
	/// Do raw world generation from given startPos position with given circular chunk radius.
	/// </summary>
	/// <param name="startPos"></param>
	/// <param name="radius"></param>
	public void GenerateWorld( Vector2? startPos = null, int radius = 4, VoronoiFactory voronoi = null)
	{
		if (voronoi == null) throw new NullReferenceException( "No VoronoiFactory component found in the scene!");
		
		Log.Info($"Starting level generation with seed  {Settings.SeedText}...");
		Log.Info( "===========================================================" );

		if ( startPos == null )
		{
			Log.Info( "MapGeneratorSystem: Using default start position of 0,0" );
			startPos = new Vector2( 0, 0 );
		}
		
		// use the voronoi component to generate & render its structure
		// @TODO: separate generate and rendering, hide rendering behind debug flag
		voronoi.GenerateAndRender();
		TectonicSpines = voronoi.TectonicSpines;
        
		// start initial world chunk generation
		GetChunkGenerationTasks(startPos, radius);
	}
	
	/// <summary>
	/// Create new chunks inside the given radius and schedule them inside the _pendingRender queue
	/// </summary>
	/// <param name="position"></param>
	/// <param name="radius"></param>
	public void GetChunkGenerationTasks(Vector2? position, int radius = 4)
	{
		if (position == null) throw new ArgumentNullException("No position given for chunk generation queue!");
		
		Log.Info($"Finding chunks in circle {radius} chunks wide around {position?.x},{position?.y}");
		_currentChunkProcessStartTime = RealTime.Now; // start timer
		
		// get the chunks inside the bounding radius
		// @TODO store completed entire Settings.CellGridSize for skipping when every chunk in it is generated.
		var chunkQueue = EnumerateChunkSpaceInside( position, radius );

		// yields to the loop whenever a chunk is found in the world that needs to generate.
		// @TODO: update camera max pan area to the largest extent of all the chunks generated
		foreach ( Vector2 chunkPos in chunkQueue )
		{
			// get or create a chunk
			if ( _chunks.TryGetValue( chunkPos, out var chunk ) )
			{
				// don't regenerate or interrupt valid chunks
				if ( chunk.Generated || chunk.Generating )
				{
					var notice = chunk.Generating ? "generating" : "already generated";
					Log.Warning( $"Chunk at (chunkspace){chunkPos.x},{chunkPos.y} {notice}!" );
					continue;
				}
			}
			else
			{
				chunk = new Chunk(chunkPos, Settings.ChunkGridSize);
				chunk.Generating = true; // immediately flag it so we never reprocess this chunk until it's finished
			}

			_queuedChunks++;
			_chunks[chunkPos] = chunk; // put it in the box
			Log.Info( $"Queued chunk [(chunkspace){chunkPos.x},{chunkPos.y}]" );

			// start streaming immediately - fuck the police
			_ = StreamChunkGeneration(chunk);
		}
	}
	
	/// <summary>
	/// Stream chunk generation Tasks and their own thread and then queue them to be rendered.
	/// </summary>
	/// <param name="chunk"></param>
	/// <returns></returns>
	private async Task StreamChunkGeneration(Chunk chunk)
	{
		try
		{
			await GameTask.RunInThreadAsync( () =>
			{
				chunk.Generate( Settings.HalfWidth, Settings.HalfHeight, TectonicSpines);
			} );
		    
			_processedChunks++;
			Log.Info( $"({_processedChunks}/{_queuedChunks}) Chunk at {chunk.Position.x},{chunk.Position.y} after " +
			          $"{(RealTime.Now - _currentChunkProcessStartTime):F2}s!" );
		    
			// queue the current chunk for rendering in the next frame
			_pendingRender.Enqueue( chunk );
		}
		catch ( System.Exception ex )
		{
			_processedChunks++;
			Log.Error( $"Chunk generation failed! Reason: {ex.Message}" );
		}
	    
		if ( _processedChunks >= _queuedChunks )
		{
			// empty queue if complete
			Log.Info($"Chunk generation batch (size: {_queuedChunks}) complete!. Took {(RealTime.Now - _currentChunkProcessStartTime):F2} s");
			_processedChunks = 0;
			_queuedChunks = 0;
		}
	}
	
	/// <summary>
	/// Attempt to dequeue and render any chunks which are done generating
	/// </summary>
	public void TryChunkDequeue()
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
		// clean helpers
		Noise = null;
		Rng = null;
		// destroy all chunk renderers
		if ( ChunkRenderers != null )
		{
			foreach ( var rendererGo in ChunkRenderers )
			{
				if (rendererGo.IsValid) rendererGo.DestroyImmediate();
			}	
		}
		
		
		// destroy bucket
		if (ChunkBucketGo != null && ChunkBucketGo.IsValid) ChunkBucketGo.DestroyImmediate();
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
	
	/// <summary>
    /// Scans the circular field and yields the top-left global space coordinate of every 
    /// grid-aligned chunk that fits completely inside the radius. This function searches grid space
    /// </summary>
    private IEnumerable<Vector2> EnumerateChunkSpaceInside( Vector2? center, int radius = 4 )
    {
	    int squareRadius = radius * radius;
	    // calculate boundaries purely in CHUNK INDEX coordinates
	    int minChunkX = (int)center?.x - radius;
	    int maxChunkX = (int)center?.x + radius;
	    int minChunkY = (int)center?.y - radius;
	    int maxChunkY = (int)center?.y + radius;
	    
	    // clamp the boundaries to the edge of the world (as a divisor of the chunk size)
	    minChunkX = Math.Clamp(minChunkX, -Settings.ChunksX, Settings.ChunksX);
	    maxChunkX = Math.Clamp(maxChunkX, -Settings.ChunksX, Settings.ChunksX);
	    
	    minChunkY = Math.Clamp(minChunkY, -Settings.ChunksY, Settings.ChunksY);
	    maxChunkY = Math.Clamp(maxChunkY, -Settings.ChunksY, Settings.ChunksY);

	    if ( minChunkX == maxChunkX || minChunkY == maxChunkY ) yield break;

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
		        float? dxCenter = center?.x;
		        float? dyCenter = center?.y;

		        bool topLeftIn = ((left - dxCenter) * (left - dxCenter)) 
									+ ((top - dyCenter) * (top - dyCenter)) <= squareRadius;
		        bool topRightIn = ((right - dxCenter) * (right - dxCenter)) 
									+ ((top - dyCenter) * (top - dyCenter)) <= squareRadius;
		        bool bottomLeftIn = ((left - dxCenter) * (left - dxCenter)) 
		                            + ((bottom - dyCenter) * (bottom - dyCenter)) <= squareRadius;
		        bool bottomRightIn = ((right - dxCenter) * (right - dxCenter)) 
									+ ((bottom - dyCenter) * (bottom - dyCenter)) <= squareRadius;

		        // if all four corners fit inside the index radius yield
		        if ( topLeftIn && topRightIn && bottomLeftIn && bottomRightIn )
		        {
			        yield return new Vector2( cx, cy );
		        }
	        }
        }
    }
}
