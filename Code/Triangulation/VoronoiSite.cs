namespace Sandbox.Triangulation
{
	public record VoronoiSite : IVoronoiSite
	{
		public int Id { get; set; }
		public Vector2 Position { get; set; }
		public int PlateId { get; set; }
		public bool IsOceanic { get; set; }
		public double BaseElevation { get; set; }
		
		public VoronoiSite(int id, Vector2 position, int plateId, bool isOceanic, double baseElevation) {
			Id = id;
			Position = position;
			PlateId = plateId;
			IsOceanic = isOceanic;
			BaseElevation = baseElevation;
		}
	}
}


