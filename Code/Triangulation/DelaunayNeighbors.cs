namespace Sandbox.Triangulation;

public struct DelaunayNeighbors(
	VoronoiResult r0,
	VoronoiResult r1,
	VoronoiResult r2,
	int count
)
	: IDelaunayNeighbors
{
	public VoronoiResult Candidate0 { get; set; } = r0;
	public VoronoiResult Candidate1 { get; set; } = r1;
	public VoronoiResult Candidate2 { get; set; } = r2;
	public int Count { get; set; } = count;
}
