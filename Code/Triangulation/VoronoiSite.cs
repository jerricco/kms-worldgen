namespace Sandbox.Triangulation;

public record VoronoiSite : IVoronoiSite
{
	public VoronoiSite(int id, Vector2 position, int plateId, bool isOceanic, double baseElevation)
	{
		this.Id = id;
		this.Position = position;
		this.PlateId = plateId;
		this.IsOceanic = isOceanic;
		this.BaseElevation = baseElevation;
	}
	public int Id { get; set; }
	public Vector2 Position { get; set; }
	public int PlateId { get; set; }
	public bool IsOceanic { get; set; }
	public double BaseElevation { get; set; }
}
