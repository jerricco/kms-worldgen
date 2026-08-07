using Sandbox.Ecology;
using Sandbox.Generation;
using Sandbox.Triangulation;

namespace Sandbox.Generator;

public class Chunk
{
	public int ChunkX;
	public int ChunkY;
	public TileData[] Tiles { get; private set; }
	
	private int _size;
	
	public Chunk(int chunkX, int chunkY, int size)
	{
		_size = size;
		ChunkX = chunkX;
		ChunkY = chunkY;
		Tiles = new TileData[size * size];
	}

	public void Generate(int xLimit, int yLimit, MapGenerator generator)
	{
		float startTime = RealTime.Now; // @DEBUG
		// Instead of processing rows sequentially on a single thread, Parallel.For 
		// automatically distributes rows across all available CPU cores.
		// @NOTE: I can try this, but disabling the whitelist will disallow me from publishing to S&Box. hmm....
		for ( int x = 0; x < _size; x++ )
		{
			for ( int y = 0; y < _size; y++ )
			{
				GenerateTile( x, y, xLimit, yLimit, generator );
			}
		};
		
		Log.Info($"Generating chunk at {ChunkX},{ChunkY}. Took {RealTime.Now - startTime} s");
	}

	public void GenerateTile(float x, float y, int xLimit, int yLimit, MapGenerator generator)
	{
		// get local vector coords
		Vector2 global = new Vector2( ChunkX * _size + x, ChunkY * _size + y );
		
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
		return (uint)(y * _size + x);
	}
}
