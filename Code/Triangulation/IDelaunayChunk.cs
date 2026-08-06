namespace Sandbox.Triangulation
{
	public interface IDelaunayChunk
	{
		BBox ChunkBounds { get; }
		List<int> TriangleIndices { get; }
	}
}
