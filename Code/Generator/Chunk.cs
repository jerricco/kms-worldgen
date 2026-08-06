using Sandbox.Ecology;
using Sandbox.Generation;
using Sandbox.Triangulation;
using System.Threading.Tasks;

namespace Sandbox.Generator;

public class Chunk
{
	public int ChunkX;
	public int ChunkY;

	public int WorldWidth;
	public int WorldHeight;
	
	public int TileCount => _size * _size;
	
	public TileData[] Tiles { get; private set; }
	
	private int _size;
	private MapGenerator _generator;
	
	

	public Chunk(int chunkX, int chunkY, GenerationSettings settings, MapGenerator generator)
	{
		_generator = generator;
		ChunkX = chunkX;
		ChunkY = chunkY;
		WorldWidth = settings.WorldWidth;
		WorldHeight = settings.WorldHeight;
		
		_size = settings.ChunkGridSize;

		Tiles = new TileData[TileCount];
		Generate();
	}

	public void Generate()
	{
		float startTime = RealTime.Now; // @DEBUG
		// Instead of processing rows sequentially on a single thread, Parallel.For 
		// automatically distributes rows across all available CPU cores.
		// @NOTE: I can try this, but disabling the whitelist will disallow me from publishing to S&Box. hmm....
		// Parallel.For( 0, _size, x =>
		for ( int x = 0; x < _size; x++ )
		{
			for ( int y = 0; y < _size; y++ )
			{
				GenerateTile( x, y );
			}
		}
		// } );
		
		Log.Info($"Generating chunk at {ChunkX},{ChunkY}. Took {RealTime.Now - startTime} s");
	}

	public void GenerateTile(float x, float y)
	{
		// get local vector coords
		Vector2 global = new Vector2( ChunkX * _size + x, ChunkY * _size + y );
		
		if ( global.x > WorldWidth || global.y > WorldHeight || global.x < -WorldWidth ||
		     global.y < -WorldHeight ) return;
		
		// generate tile properties
		DelaunayNeighbors neighbors = _generator.Voronoi.GetVoronoiSiteCandidates( global.x, global.y );
		double elevation            = _generator.GetTileElevation( global.x, global.y, neighbors );
		RegionId regionId           = _generator.GetTileRegion( elevation );
		double humidity             = 0d;
		double temperature          = 0d;
		int materialId              = 0;
		SubterraneanLayer geology   = _generator.GetTileGeology( global.x, global.y, elevation );
		// load data to chunk
		uint tileIndex = LocalIndex(0, 0);
		Tiles[tileIndex] = new TileData(elevation, humidity, temperature, materialId, regionId, geology, neighbors );
	}
	
	// Fast inline index helper mapping local 2D space to 1D space
	public uint LocalIndex( int x, int y ) {
		return (uint)(y * _size + x);
	}
}
