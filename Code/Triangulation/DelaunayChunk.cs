namespace Sandbox.Triangulation;

public struct DelaunayChunk : IDelaunayChunk
{
	public BBox ChunkBounds { get; set; }
	public List<int> TriangleIndices { get; set; }

	public DelaunayChunk(BBox chunkBounds, List<int> triangleIndices)
	{
		this.ChunkBounds = chunkBounds;
		this.TriangleIndices = triangleIndices;
	}
}
