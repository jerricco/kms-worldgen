using Sandbox.GameObjectSystems.Map;
using Sandbox.Generation;
using Sandbox.Triangulation;

namespace Sandbox.Generator;

public class Chunk
{
	public Vector2 Position { get; set; }
	public Vector2 GlobalPosition { get; private set; }
	
	public TileData[] Tiles { get; private set; }
	public bool Generating;
	public bool Generated;
	public int Size;
	public float TileSize = 1f; // @TODO: increase physical tile size? Will be a big refactor
	
	
	public Chunk(Vector2 chunkPosition, int size)
	{
		Size = size;
		
		// All positions refer to the chunks top-left corner
		Position = new Vector2( chunkPosition.x, chunkPosition.y );
		GlobalPosition = new Vector2( Position.x * Size * TileSize, Position.y * Size * TileSize );
		
		Tiles = new TileData[size * size];
	}

	public void Generate(int xLimit, int yLimit, List<VoronoiFactory.CurvedSpine> spines)
	{
		Generating = true;
		for ( int x = 0; x < Size; x++ )
		{
			for ( int y = 0; y < Size; y++ )
			{
				GenerateTile( x, y, xLimit, yLimit, spines );
			}
		}
		
		Generating = false;
		Generated = true;
	}

	public void GenerateTile(float x, float y, int xLimit, int yLimit, List<VoronoiFactory.CurvedSpine> spines)
	{
		Vector2 global = new Vector2( GlobalPosition.x + x, GlobalPosition.y + y ); // get global tile location
		
		// ignore parts of the chunk that extend past the world border.
		if ( global.x > xLimit || global.y > yLimit || global.x < -xLimit || global.y < -yLimit )
			return;
		
		// create tile
		TileData tile = new TileData();
		tile.GlobalPosition = global;
				
		// generate tile properties
		tile.GenerateElevation(MapGeneratorSystem.Current.Settings, spines);
		tile.GenerateGeology();
		tile.GenerateRegion();
		
		// load data to chunk
		uint tileIndex = LocalIndex((int)x, (int)y);
		Tiles[tileIndex] = tile;
	}
	
	// Fast inline index helper mapping local 2D space to 1D space
	public uint LocalIndex( int x, int y ) {
		return (uint)(y * Size + x);
	}
}
