namespace Sandbox.Generation;

public class Chunk
{
	public int ChunkX;
	public int ChunkY;

	public int WorldWidth;
	public int WorldHeight;
	
	public int TileCount => _size * _size;
	
	public double[] Elevations;
	public int[] RegionIds;
	public double[] Humidities;
	public double[] Temperatures;
	public int[] MaterialIds;
	
	private int _size;
	private MapGenerator _generator;
	private GenerationSettings _settings;

	public Chunk(int chunkX, int chunkY, GenerationSettings settings, MapGenerator generator)
	{
		_generator = generator;
		_settings = settings;
		
		ChunkX = chunkX;
		ChunkY = chunkY;
		WorldWidth = settings.WorldWidth;
		WorldHeight = settings.WorldHeight;
		
		_size = settings.ChunkGridSize;

		Elevations = new double[TileCount];
		RegionIds = new int[TileCount];
		Humidities = new double[TileCount];
		Temperatures = new double[TileCount];
		MaterialIds = new int[TileCount];
		
		Generate();
	}

	public void Generate()
	{
		for ( int x = 0; x < _size; x++ )
		{
			for ( int y = 0; y < _size; y++ )
			{
				int globalX = ChunkX * _size + x;
				int globalY = ChunkY *  _size + y;
				
				// check out of bounds, in case we only need a partial chunk.
				if ( globalX > WorldWidth || globalY > WorldHeight || globalX < -WorldWidth ||
				     globalY < -WorldHeight ) continue;
				
				Tile tile = new Tile(globalX, globalY, _settings, _generator);
				
				// store rapid retrieve tile data against the chunk
				uint tileIndex = LocalIndex(x, y);
				Elevations[tileIndex] = tile.Elevation;
				RegionIds[tileIndex] = (int)tile.RegionId;
				Humidities[tileIndex] = tile.Humidity;
				Temperatures[tileIndex] = tile.Temperature;
				MaterialIds[tileIndex] = tile.MaterialId;
			}
		}
	}
	
	// Fast inline index helper mapping local 2D space to 1D space
	public uint LocalIndex( int x, int y ) {
		return (uint)(x * _size + y);
	}
}
