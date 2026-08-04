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

	public Chunk(int chunkX, int chunkY, GenerationSettings settings)
	{
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
}
