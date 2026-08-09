namespace Sandbox.Triangulation
{
	public struct DelaunayNeighbors : IDelaunayNeighbors
	{
		public VoronoiResult Candidate0 { get; set; }
		public VoronoiResult Candidate1 { get; set; }
		public VoronoiResult Candidate2 { get; set; }
		public int Count { get; set; }

		public DelaunayNeighbors( VoronoiResult r0, VoronoiResult r1, VoronoiResult r2, int count )
		{
			Candidate0 = r0;
			Candidate1 = r1;
			Candidate2 = r2;
			Count = count;
		}
	}	
}
