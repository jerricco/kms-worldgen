namespace Sandbox.Triangulation;

public interface IVoronoiSite
{
	int Id { get; }
	Vector2 Position { get; }
	int PlateId { get; }
	bool IsOceanic { get; }
	double BaseElevation { get; }
}
