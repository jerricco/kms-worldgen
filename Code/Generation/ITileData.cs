using Sandbox.Ecology;
using Sandbox.Triangulation;

namespace Sandbox.Generation;

public interface ITileData
{
	double Elevation { get; }
	RegionId RegionId { get; }
	double Humidity { get; }
	double Temperature { get; }
	int MaterialId { get; }
	SubterraneanLayer Geology { get; }
	DelaunayNeighbors NeighborSites { get; }
}
