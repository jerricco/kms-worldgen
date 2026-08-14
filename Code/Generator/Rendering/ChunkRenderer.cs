using Sandbox.GameData;

namespace Sandbox.Generator.Rendering;

public class ChunkRenderer : Component
{
	[Property] public GenerationSettings Settings { get; set; }
	[Property] public ChunkTheme Theme { get; set; }
	[Property] public Material ChunkMaterial { get; set; }
	[Property] public Color DefaultTileColor { get; set; } = Color.Magenta;

    private ModelRenderer _modelRenderer;

	protected override void OnStart()
	{
		this.Settings = this.Settings ?? ResourceLibrary.Get<GenerationSettings>("default_generation.genconf");
		this.Theme = this.Theme ?? ResourceLibrary.Get<ChunkTheme>("default_theme.gentheme");
		this.ChunkMaterial = this.ChunkMaterial ?? Material.Load("materials/tile_unlit.vmat");
	}

	protected override void OnDestroy()
	{
        if (this._modelRenderer.IsValid)
        {
            this._modelRenderer.Destroy();
        }
	}

	// @TODO: Create multiple sceneObjects which each have a different visualisation of the tiles.
	// This should eventually become the gameview layer system

	/// <summary>
	/// Call this method whenever the chunk data is ready or changes.
	/// </summary>
	public void RegenerateMesh(Chunk chunk)
	{
		if (chunk.Tiles.Length < 1)
		{
			Log.Error($"Chunk_{chunk.Position.x}_{chunk.Position.y}: Not enough tiles for rendering! Exiting...");
			return;
		}

		this._modelRenderer = this.GameObject.GetOrAddComponent<ModelRenderer>();

		var worldX = chunk.Position.x * chunk.Size;
		var worldY = chunk.Position.y * chunk.Size;
		// Put the chunk into it's place in the world
		this.WorldPosition = new Vector3(worldX, worldY, 0f);

		var vertices = new List<Vertex>(chunk.Size * chunk.Size * 4);
		var indices = new List<int>(chunk.Size * chunk.Size * 6);
		var vertexIndex = 0;
		var visited = new bool[chunk.Size * chunk.Size];

		for (var y = 0; y < chunk.Size; y++)
		{
			for (var x = 0; x < chunk.Size; x++)
			{
				var tileIndex = chunk.LocalIndex(x, y);
				if (visited[tileIndex])
				{
					continue;
				}

				var tile = chunk.Tiles[tileIndex];
				var tileColor = this.GetElevationColour(tile.Elevation);

				// get max width of all identical adjacent tiles
				var width = 1;
				while (x + width < chunk.Size)
				{
					var nextXIndex = chunk.LocalIndex(x + width, y);
					if (visited[nextXIndex])
					{
						break;
					}

					var nextTile = chunk.Tiles[nextXIndex];
					if (this.GetElevationColour(nextTile.Elevation) != tileColor)
					{
						break;
					}

					width++;
				}

				// get max height of all identical adjacent tiles
				var height = 1;
				while (y + height < chunk.Size)
				{
					var rowMatches = true;
					for (var rX = 0; rX < width; rX++)
					{
						var nextYIndex = chunk.LocalIndex(x + rX, y + height);
						if (visited[nextYIndex])
						{
							rowMatches = false;
							break;
						}

						var nextTile = chunk.Tiles[nextYIndex];
						if (this.GetElevationColour(nextTile.Elevation) != tileColor)
						{
							rowMatches = false;
							break;
						}
					}

					if (!rowMatches)
					{
						break;
					}

					height++;
				}

				// mark visited tiles
				for (var h = 0; h < height; h++)
				{
					for (var w = 0; w < width; w++)
					{
						var visitedIndex = chunk.LocalIndex(x + w, y + h);
						visited[visitedIndex] = true;
					}
				}

				// Calculate real edge stretches
				float endX = x + width;
				float endY = y + height;

				// Build a simple flat quad for each tile
				vertices.Add(
					new Vertex
					{
						Position = new Vector3(x, y, 0f),
						Normal = Vector3.Up,
						Tangent = new Vector4(Vector3.Right, 1f),
						TexCoord0 = new Vector2(0f, 0f),
						Color = tileColor,
					}
				);

				vertices.Add(
					new Vertex
					{
						Position = new Vector3(endX, y, 0f),
						Normal = Vector3.Up,
						Tangent = new Vector4(Vector3.Right, 1f),
						TexCoord0 = new Vector2(1f, 0f),
						Color = tileColor,
					}
				);

				vertices.Add(
					new Vertex
					{
						Position = new Vector3(endX, endY, 0f),
						Normal = Vector3.Up,
						Tangent = new Vector4(Vector3.Right, 1f),
						TexCoord0 = new Vector2(1f, 1f),
						Color = tileColor,
					}
				);

				vertices.Add(
					new Vertex
					{
						Position = new Vector3(x, endY, 0f),
						Normal = Vector3.Up,
						Tangent = new Vector4(Vector3.Right, 1f),
						TexCoord0 = new Vector2(0f, 1f),
						Color = tileColor,
					}
				);

				// Triangle 1
				indices.Add(vertexIndex);
				indices.Add(vertexIndex + 1);
				indices.Add(vertexIndex + 2);

				// Triangle 2
				indices.Add(vertexIndex);
				indices.Add(vertexIndex + 2);
				indices.Add(vertexIndex + 3);

				vertexIndex += 4;
			}
		}

		// Create and build the S&Box Mesh object
		var mesh = new Mesh(this.ChunkMaterial);
		mesh.CreateVertexBuffer(vertices.Count, vertices.ToArray());
		mesh.CreateIndexBuffer(indices.Count, indices.ToArray());

		// create tall bounding box. though the chunks are currently 2D, they should contain
		// the vertical height of everything in them.
		var minBounds = new Vector3(0f, 0f, -256f);
		var maxBounds = new Vector3(chunk.Size, chunk.Size, 256f);
		mesh.Bounds = new BBox(minBounds, maxBounds);

		// Package the mesh into a Model and assign it to the renderer
		var model = Model.Builder
			.AddMesh(mesh)
			.Create();

		this._modelRenderer.Model = model;
		this._modelRenderer.Enabled = true;


	}

	private Color GetElevationColour(double elevation)
	{
		// If out of elevation bounds, express as the default Color
		if (elevation < -1.0D || elevation > 1.0D)
		{
			return this.DefaultTileColor;
		}

		// @TODO: fiddle here more
		if (elevation == this.Settings.AbyssalLevel)
		{
			return this.Theme.Void;
		}

		if (elevation < this.Settings.TrenchLevel)
		{
			return this.Theme.CrustFloor;
		}

		if (elevation < this.Settings.DeepOceanLevel)
		{
			return this.Theme.AbyssalOcean;
		}

		if (elevation < this.Settings.OceanLevel)
		{
			return this.Theme.DeepOcean;
		}

		if (elevation < this.Settings.SeaLevel)
		{
			return this.Theme.Ocean;
		}

		if (elevation < this.Settings.BeachLevel)
		{
			return this.Theme.Beach;
		}

		if (elevation < this.Settings.PlainLevel)
		{
			return this.Theme.Plain;
		}

		if (elevation < this.Settings.HillLevel)
		{
			return this.Theme.Hill;
		}

		if (elevation < this.Settings.MountainLevel)
		{
			return this.Theme.Mountain;
		}

		return this.Theme.Peak;
	}
}
