namespace Sandbox.Triangulation
{
	public interface IDelaunayNeighbors
	{
		VoronoiResult Candidate0 { get; }
		VoronoiResult Candidate1 { get; }
		VoronoiResult Candidate2 { get; }
		int Count { get; }
	}	
}
