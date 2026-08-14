using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Sandbox.GameData;
using Sandbox.Generation;
using Sandbox.Generator;
using Sandbox.Generator.Rendering;

namespace Sandbox.GameObjectSystems.Map;

public class MapGeneratorSystem : GameObjectSystem<MapGeneratorSystem>
{
    private float CurrentChunkProcessStartTime { get; set; }
	private readonly ConcurrentQueue<Chunk> _chunksPendingRender = new();
    private VoronoiFactory? _voronoiFactory;

	/// <summary>
	/// Stores a list of chunks with their GLOBAL Vector2 coordinate as a key
	/// </summary>
	private readonly Dictionary<Vector2, Chunk> _chunks = new();

	// @TODO: Create a way to generate individual chunk(s) on demand regardless if they are already generated or not.

	#region Static Helpers

	public bool SceneReady { get; private set; }
	public Prng? Rng { get; private set; }
	public OpenSimplexNoise? Noise { get; private set; }

	#endregion

	#region Game Objects

    public GameObject? ChunkBucketGo { get; set; }

    #endregion

    #region Editor Properties

	[Property]
	public GenerationSettings Settings { get; set; }

	[Property]
	public ChunkTheme Theme { get; set; } // @TODO: this is clumsy

	[Property]
	public Material ElevationHeightmapMaterial { get; set; }

	[Property]
	public Material VoronoiLineMaterial { get; set; }

	// seeded generation properties
	[Property, ReadOnly]
	public double RandomAngle { get; private set; }

	[Property, ReadOnly]
	public int OffsetX { get; private set; }

	[Property, ReadOnly]
	public int OffsetY { get; private set; }

	[Property, ReadOnly]
	public double CosA { get; private set; }

	[Property, ReadOnly]
	public double SinA { get; private set; }

    #endregion

	public MapGeneratorSystem(Scene scene) : base(scene)
	{
		// start scene up
		// @TODO: Scene information should determine whether to start a new generation or load one from disk.

		// get resources
		this.Settings = ResourceLibrary.Get<GenerationSettings?>("default_generation.genconf") ?? new GenerationSettings();
		this.Theme = ResourceLibrary.Get<ChunkTheme>("default_theme.gentheme");

		// get materials
		this.ElevationHeightmapMaterial = Material.Load("materials/tile_unlit.vmat");
		this.VoronoiLineMaterial = Material.Load("materials/opaque_line.vmat");

		// lifecycle
		this.Listen(Stage.StartUpdate, 10, this.TryChunkDequeue, "Chunk Queue Tick");

		this.InitializeScene(this.Settings.SeedText);
	}

	/// <summary>
	/// Initialise the MapGeneratorSystem on Scene start.
	/// </summary>
	/// <param name="seed"></param>
	public void InitializeScene(string seed)
	{
		this._chunksPendingRender.Clear();
		this.SetSeed(seed);
		this.SceneReady = true;
	}

	/// <summary>
	/// Do raw world generation from given startPos position with given circular chunk radius.
	/// </summary>
	/// <param name="startPos">The starting world position.</param>
	/// <param name="radius">The given chunk radius.</param>
	/// <param name="seed">The generator seed.</param>
	public async Task GenerateWorldAsync(Vector2? startPos = null, int radius = 4, string? seed = null)
	{
		if (radius > 32)
		{
			throw new ArgumentException("Radius can be no larger than 32 chunks!", nameof(radius));
		}

		if (seed != null && seed != this.Settings.SeedText)// seed has changed! clear out
		{
			Log.Warning($"Seed was changed {this.Settings.SeedText}->{seed}...Regenerating...");
			this.InitializeScene(seed);
		}

		Log.Info($"Starting level generation with seed <color=green>{this.Settings.SeedText}</color>...");
		Log.Info("===========================================================");

		if (startPos == null)
		{
			Log.Info("MapGeneratorSystem: Using default start position of 0,0");
			startPos = new Vector2(0, 0);
		}

		// use the voronoi component to generate & render its structure
        this._voronoiFactory = this.Scene.GetAllComponents<VoronoiFactory>().FirstOrDefault();
        if (this._voronoiFactory == null)
        {
            throw new InvalidOperationException("No VoronoiFactory component found in the scene!");
        }

		// @TODO: separate generate and rendering, hide rendering behind debug flag
        this._voronoiFactory.GenerateAndRender();

		// start initial world chunk generation
		await this.GetChunkGenerationTasksAsync(startPos, radius);
	}

	// @TODO store completed entire Settings.CellGridSize for skipping when every chunk in it is generated.
	/// <summary>
	/// Create new chunks inside the given radius and schedule them inside the _pendingRender queue
	/// </summary>
	/// <param name="position"></param>
	/// <param name="radius"></param>
	public async Task GetChunkGenerationTasksAsync(Vector2? position, int radius = 4)
	{
		if (position == null)
		{
			throw new ArgumentNullException(nameof(position), "No position given for chunk generation queue!");
		}

		Log.Info($"Finding chunks in circle {radius} chunks wide around {position.Value.x},{position.Value.y}");
		this.CurrentChunkProcessStartTime = RealTime.Now;
		var chunkQueue = this.GetChunkQueue(position, radius);

		// get the chunks inside the bounding radius
		Log.Info($"Found {chunkQueue.Length} chunks, processing...");
		foreach (var chunkPos in chunkQueue)
        {
            if (this._chunks.TryGetValue(chunkPos, out var chunk))
            {
                if (chunk.Generated || chunk.Generating)
                {
                    continue;
                }
            }
            else
            {
                chunk = new Chunk(chunkPos, this.Settings.ChunkGridSize);
                this._chunks[chunkPos] = chunk; // put it in box
            }

            // immediately flag it so we never reprocess this chunk until it's finished
            chunk.Generating = true;
        }

		// start streaming immediately - fuck the police
        var chunksToProcess = this._chunks.Where(c => c.Value.Generating).Select(c => c.Value);
		await this.ProcessAmortisedChunkBatchAsync([..chunksToProcess]);
	}

    /// <summary>
    /// Sets the generation seed.
    /// </summary>
    /// <param name="seed">The seed value.</param>
    private void SetSeed(string seed)
    {
        this.Settings.SeedText = seed;

        // set up helpers
        this.Rng = new Prng(seed);
        this.Noise = new OpenSimplexNoise(this.Rng);

        // component configuration
        this.Rng = new Prng(this.Settings.SeedText);
        this.Noise = new OpenSimplexNoise(this.Rng);
        this.RandomAngle = this.Rng.NextRangeDouble(0, Math.Tau);
        this.CosA = Math.Cos(this.RandomAngle);
        this.SinA = Math.Sin(this.RandomAngle);
        this.OffsetX = this.Rng.NextRange(10000, 90000);
        this.OffsetY = this.Rng.NextRange(10000, 90000);

        this._chunks.Clear();
        this.ClearRenderers();
    }

	/// <summary>
	/// Spawn a background thread and process chunks in amortised batches, pausing every few to yield to the game.
	/// </summary>
	/// <param name="chunksToProcess"></param>
	/// <returns></returns>
	private async Task ProcessAmortisedChunkBatchAsync(Chunk[] chunksToProcess)
	{
		if (chunksToProcess.Length == 0) return;
        if (this._voronoiFactory == null) throw new InvalidOperationException("No VoronoiFactory component found in the scene!");

		Log.Info($"Amortising batch of {chunksToProcess.Length} chunks.");

		var halfWidth = this.Settings.HalfWidth;
		var halfHeight = this.Settings.HalfHeight;

		await GameTask.RunInThreadAsync(
			async () =>
			{
				for (var i = 0; i < chunksToProcess.Length; i++)
				{
					var chunk = chunksToProcess[i];
					try
					{
						// Generate the chunk data completely off-thread
						chunk.Generate(halfWidth, halfHeight, this._voronoiFactory.TectonicSpines);
						this._chunksPendingRender.Enqueue(chunk);
					}
					catch (Exception ex)
					{
						Log.Error($"Chunk error: {ex.Message}");
					}

                    if (i % 3 == 0) await GameTask.Yield();
				}
			}
		);

		Log.Info($"Batch complete in {RealTime.Now - this.CurrentChunkProcessStartTime:F2}s");
	}

	/// <summary>
	/// Attempt to dequeue and render any chunks which are done generating
	/// </summary>
	private void TryChunkDequeue()
    {
        if (!this.SceneReady) return;

		// Check our container GO exists and if not, create it
		if (!this._chunksPendingRender.IsEmpty && this.ChunkBucketGo is not { IsValid: true })
		{
			this.ChunkBucketGo = new GameObject(true, "ChunkBucket"); // holds our generated chunks
			this.ChunkBucketGo.SetParent(this.Scene);
        }

		// Establish a maximum amount of chunks we are allowed to visually build per frame
		// Adjust this number (e.g., 1, 2, or 3) depending on how heavy your mesh building is
		var chunksProcessedThisFrame = 0;
		const int maxChunksPerFrame = 8;

		while (this._chunksPendingRender.TryDequeue(out var chunk))
		{
			// Log.Info( $"Chunk_{chunk.Position.x}_{chunk.Position.y} will render now" ); // @DEBUG
			var chunkRenderGo = new GameObject(true, $"Chunk_{chunk.Position.x}_{chunk.Position.y}");
			chunkRenderGo.Tags.Add("chunk");

			// attach a rendering component to this generator's ChunkBucketGo
			chunkRenderGo.SetParent(this.ChunkBucketGo);

			var renderer = chunkRenderGo.Components.Create<ChunkRenderer>();
			renderer.Settings = this.Settings;
			renderer.Theme = this.Theme;
			renderer.ChunkMaterial = this.ElevationHeightmapMaterial;

			renderer.RegenerateMesh(chunk);
			chunksProcessedThisFrame++;

			// Stop processing for this frame if we hit our budget
			if (chunksProcessedThisFrame >= maxChunksPerFrame)
			{
				break;
			}
		}
	}

	/// <summary>
	/// Cleans up the artifacts of this GameObjectSystem so that it can be reinitialised in the Scene.
	/// </summary>
	public void Cleanup()
	{
		// clear voroni structure
        if (this._voronoiFactory?.IsValid == true) this._voronoiFactory.ClearData();

		// destroy all chunk renderers
		this.ClearRenderers();

		// destroy bucket
		if (this.ChunkBucketGo?.IsValid == true) this.ChunkBucketGo.Destroy();

        // set the scene as not ready
		this.SceneReady = false;
	}

	private void ClearRenderers()
	{
		if (this.ChunkBucketGo?.IsValid != true) return;

		foreach (var rendererGo in this.ChunkBucketGo.Children.Where(rendererGo => rendererGo.IsValid))
			rendererGo.Destroy();

		this.ChunkBucketGo.Children.Clear();
	}

	/// <summary>
	/// Retrieve a chunk from chunk space
	/// </summary>
	/// <param name="chunkX"></param>
	/// <param name="chunkY"></param>
	/// <returns>Chunk Instance</returns>
	public Chunk? GetChunkAt(int chunkX, int chunkY)
	{
		var chunkPos = new Vector2(chunkX, chunkY);
		return this._chunks.GetValueOrDefault(chunkPos);
	}

	private Vector2[] GetChunkQueue(Vector2? center, int radius = 4)
	{
		var cxCenter = center?.x ?? 0f;
		var cyCenter = center?.y ?? 0f;
		var squareRadius = radius * radius;

		// clamp chunk boundaries
		var minChunkY = Math.Clamp((int)Math.Floor(cyCenter - radius), -this.Settings.ChunksY + 1, this.Settings.ChunksY - 1);
		var maxChunkY = Math.Clamp((int)Math.Ceiling(cyCenter + radius), -this.Settings.ChunksY + 1, this.Settings.ChunksY - 1);

        if (minChunkY >= maxChunkY) return [];

		// count the valid chunks so we can size the array
		var totalCount = 0;

		// Scan line by line along the Y axis to find the valid X boundaries
		for (var cy = minChunkY; cy <= maxChunkY; cy++)
		{
			// For a chunk at 'cy' to be fully inside, both its top and bottom Y bounds must pass.
			var yBound1 = cy - cyCenter;
			var yBound2 = cy + 1 - cyCenter;
			var yDistSq = Math.Max(yBound1 * yBound1, yBound2 * yBound2);

			// If the vertical distance alone exceeds the radius, this entire horizontal row is invalid
            if (yDistSq >= squareRadius) continue;

			// Calculate the maximum horizontal distance allowed for this row
			var maxDistX = MathF.Sqrt(squareRadius - yDistSq);

			// Find the precise start and end chunk indices that fit completely inside the horizontal chord
			var minX = (int)Math.Ceiling(cxCenter - maxDistX);
			var maxX = (int)Math.Floor(cxCenter + maxDistX) - 1;

			// Clamp to world bounds
			minX = Math.Clamp(minX, -this.Settings.ChunksX + 1, this.Settings.ChunksX - 1);
			maxX = Math.Clamp(maxX, -this.Settings.ChunksX + 1, this.Settings.ChunksX - 1);

			if (minX <= maxX)
			{
				totalCount += maxX - minX + 1;
			}
		}

		if (totalCount == 0) return [];

		// allocate array and populate it directly via indices
		var results = new Vector2[totalCount];
		var index = 0;

		for (var cy = minChunkY; cy <= maxChunkY; cy++)
		{
			var yBound1 = cy - cyCenter;
			var yBound2 = cy + 1 - cyCenter;
			var yDistSq = Math.Max(yBound1 * yBound1, yBound2 * yBound2);

            if (yDistSq >= squareRadius) continue;

			var maxDistX = MathF.Sqrt(squareRadius - yDistSq);
			var minX = Math.Clamp((int)Math.Ceiling(cxCenter - maxDistX), -this.Settings.ChunksX + 1, this.Settings.ChunksX - 1);
			var maxX = Math.Clamp((int)Math.Floor(cxCenter + maxDistX) - 1, -this.Settings.ChunksX + 1, this.Settings.ChunksX - 1);

			// Fill the pre-allocated array linearly
			for (var cx = minX; cx <= maxX; cx++)
			{
				results[index++] = new Vector2(cx, cy);
			}
		}

		return results;
	}
}
