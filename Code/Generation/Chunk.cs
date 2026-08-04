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

	public Chunk(int chunkX, int chunkY, GenerationSettings settings, MapGenerator generator)
	{
		_generator = generator;
		
		ChunkX = chunkX;
		ChunkY = chunkY;
		WorldWidth = settings.WorldWidth;
		WorldHeight = settings.WorldHeight;
		
		_size = settings.ChunkGridSize;

		Elevations = new double[_size];
		RegionIds = new int[_size];
		Humidities = new double[_size];
		Temperatures = new double[_size];
		MaterialIds = new int[_size];
	}

	public void Generate(int chunkX, int chunkY)
	{
		// does nothing for now
	}
	
	// Fast inline index helper mapping local 2D space to 1D space
	public uint LocalIndex( int x, int y ) {
		return (uint)(x * _size + y);
	}
}
