namespace Sandbox.Triangulation;

public class DelaunayNeighbors : IDelaunayNeighbors
{
	public VoronoiResult Candidate0 { get; set; }
	public VoronoiResult Candidate1 { get; set; }
	public VoronoiResult Candidate2 { get; set; }
	public int Count { get; set; }

	public DelaunayNeighbors(VoronoiResult r0, VoronoiResult r1, VoronoiResult r2, int count)
	{
		this.Candidate0 = r0;
		this.Candidate1 = r1;
		this.Candidate2 = r2;
		this.Count = count;
	}
}
