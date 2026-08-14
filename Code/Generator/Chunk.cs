using Sandbox.Systems.Map;
using Sandbox.Generation;
using Sandbox.Triangulation;

namespace Sandbox.Generator;

public class Chunk
{
	public bool Generated { get; set; }
	public bool Generating { get; set; }
	public int Size { get; set; }
	public float TileSize { get; set; } = 1f;// @TODO: increase physical tile size? Will be a big refactor
	
	public Vector2 Position { get; set; }
	public Vector2 GlobalPosition { get; }
	public TileData[] Tiles { get; }

	public Chunk(Vector2 chunkPosition, int size)
	{
		this.Size = size;

		// All positions refer to the chunks top-left corner
		this.Position = new Vector2(chunkPosition.x, chunkPosition.y);
		this.GlobalPosition = new Vector2(this.Position.x * this.Size * this.TileSize, this.Position.y * this.Size * this.TileSize);

		this.Tiles = new TileData[size * size];
	}

	public void Generate(int xLimit, int yLimit, List<VoronoiFactory.CurvedSpine> spines)
	{
		this.Generating = true;

		for (var x = 0; x < this.Size; x++)
		{
			for (var y = 0; y < this.Size; y++)
			{
				this.GenerateTile(x, y, xLimit, yLimit, spines);
			}
		}

		this.Generating = false;
		this.Generated = true;
	}

	public void GenerateTile(float x, float y, int xLimit, int yLimit, List<VoronoiFactory.CurvedSpine> spines)
	{
		var global = new Vector2(this.GlobalPosition.x + x, this.GlobalPosition.y + y);// get global tile location

		// ignore parts of the chunk that extend past the world border.
		if (global.x > xLimit || global.y > yLimit || global.x < -xLimit || global.y < -yLimit)
		{
			return;
		}

		// create tile
		var tile = new TileData
		{
			GlobalPosition = global,
		};

		// generate tile properties
		tile.GenerateElevation(MapGeneratorSystem.Current.Settings, spines);
		tile.GenerateGeology();
		tile.GenerateRegion();

		// load data to chunk
		var tileIndex = this.LocalIndex((int)x, (int)y);
		this.Tiles[tileIndex] = tile;
	}

	// Fast inline index helper mapping local 2D space to 1D space
	public uint LocalIndex(int x, int y)
	{
		return (uint)(y * this.Size + x);
	}
}
