using Sandbox.Ecology;
using Sandbox.Generation;
using Sandbox.Triangulation;

namespace Sandbox.Generator;

public class Chunk
{
	public int ChunkX;
	public int ChunkY;
	public TileData[] Tiles { get; private set; }
	public bool Generating;
	public bool Generated;
	public int Size;
	
	
	public Chunk(int chunkX, int chunkY, int size)
	{
		Size = size;
		ChunkX = chunkX;
		ChunkY = chunkY;
		Tiles = new TileData[size * size];
	}

	public Chunk Generate(int xLimit, int yLimit, MapGenerator generator)
	{
		Generating = true;
		for ( int x = 0; x < Size; x++ )
			for ( int y = 0; y < Size; y++ )
				GenerateTile( x, y, xLimit, yLimit, generator );

		Generating = false;
		Generated = true;

		return this;
	}

	public void GenerateTile(float x, float y, int xLimit, int yLimit, MapGenerator generator)
	{
		// get local vector coords
		Vector2 global = new Vector2( ChunkX * Size + x, ChunkY * Size + y );
		
		// ignore parts of the chunk that extend past the world border.
		if ( global.x > xLimit || global.y > yLimit || global.x < -xLimit || global.y < -yLimit )
			return;
		
		// generate tile properties
		DelaunayNeighbors neighbors = generator.Voronoi.GetVoronoiSiteCandidates( global.x, global.y );
		double elevation            = generator.GetTileElevation( global.x, global.y, neighbors );
		RegionId regionId           = generator.GetTileRegion( elevation );
		double humidity             = 0d;
		double temperature          = 0d;
		int materialId              = 0;
		SubterraneanLayer geology   = generator.GetTileGeology( global.x, global.y, elevation );
		// load data to chunk
		uint tileIndex = LocalIndex(0, 0);
		Tiles[tileIndex] = new TileData(elevation, humidity, temperature, materialId, regionId, geology, neighbors );
	}
	
	// Fast inline index helper mapping local 2D space to 1D space
	public uint LocalIndex( int x, int y ) {
		return (uint)(y * Size + x);
	}
}
